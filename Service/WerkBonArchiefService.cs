using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Snapshot;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class WerkBonArchiefService : IWerkBonArchiefService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<WerkBonArchiefService> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public WerkBonArchiefService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WerkBonArchiefService> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ─────────────────────────────────────────────────────────────────
        //  ArchiveerAsync
        // ─────────────────────────────────────────────────────────────────

        public async Task<WerkBonArchief> ArchiveerAsync(int werkBonId, string? annuleringsReden = null)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Klant)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.TypeLijst)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.Glas)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.PassePartout1)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.PassePartout2)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.DiepteKern)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.Opkleven)
                .Include(w => w.Offerte)
                    .ThenInclude(o => o.Regels)
                        .ThenInclude(r => r.Rug)
                .Include(w => w.Taken)
                    .ThenInclude(t => t.OfferteRegel)
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == werkBonId);

            if (werkBon is null)
                throw new InvalidOperationException($"WerkBon {werkBonId} niet gevonden.");

            var offerte = werkBon.Offerte
                ?? throw new InvalidOperationException($"WerkBon {werkBonId} heeft geen gekoppelde offerte.");

            // ── Bouw snapshot ───────────────────────────────────────────
            var snapshot = BouwSnapshot(werkBon, offerte);
            var snapshotJson = JsonSerializer.Serialize(snapshot, _jsonOpts);

            // ── Gedenormaliseerde velden ────────────────────────────────
            var klant = offerte.Klant;
            var archief = new WerkBonArchief
            {
                OrigineleWerkBonId  = werkBon.Id,
                OfferteId           = offerte.Id,
                KlantNaam           = klant is not null
                                        ? $"{klant.Voornaam} {klant.Achternaam}".Trim()
                                        : "(geen klant)",
                KlantId             = klant?.Id,
                OfferteDatum        = offerte.Datum,
                OfferteStatusOpMoment  = offerte.Status.ToString(),
                WerkBonStatusOpMoment  = werkBon.Status.ToString(),
                TotaalPrijsIncl     = werkBon.TotaalPrijsIncl,
                GearchiveerdOp      = DateTime.UtcNow,
                AnnuleringsReden    = annuleringsReden,
                Snapshot            = snapshotJson,
                IsHersteld          = false,
                HersteldNaarOfferteId = null
            };

            db.WerkBonArchieven.Add(archief);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "WerkBon {WerkBonId} gearchiveerd als archief-entry {ArchiefId} (offerte {OfferteId}).",
                werkBonId, archief.Id, offerte.Id);

            return archief;
        }

        // ─────────────────────────────────────────────────────────────────
        //  HerstellenAsync — kloon offerte + regels vanuit snapshot
        // ─────────────────────────────────────────────────────────────────

        public async Task<int> HerstellenAsync(int archiefId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var archief = await db.WerkBonArchieven.FirstOrDefaultAsync(a => a.Id == archiefId)
                ?? throw new InvalidOperationException($"Archief-entry {archiefId} niet gevonden.");

            if (archief.IsHersteld)
                throw new InvalidOperationException(
                    $"Archief-entry {archiefId} is al eerder hersteld naar offerte {archief.HersteldNaarOfferteId}.");

            var snapshot = JsonSerializer.Deserialize<WerkBonArchiefSnapshot>(archief.Snapshot, _jsonOpts)
                ?? throw new InvalidOperationException("Snapshot kon niet gedeserialiseerd worden.");

            // ── Nieuwe offerte aanmaken op basis van snapshot ──────────
            var nieuweOfferte = new Offerte
            {
                KlantId         = archief.KlantId,
                Datum           = DateTime.Today,
                Status          = OfferteStatus.Concept,
                Opmerking       = $"[HERSTELD uit archief #{archiefId} – orig. offerte #{archief.OfferteId}] " +
                                  snapshot.Offerte.Opmerking,
                SubtotaalExBtw  = snapshot.Offerte.SubtotaalExBtw,
                BtwBedrag       = snapshot.Offerte.BtwBedrag,
                TotaalInclBtw   = snapshot.Offerte.TotaalInclBtw,
                KortingPct      = snapshot.Offerte.KortingPct,
                MeerPrijsIncl   = snapshot.Offerte.MeerPrijsIncl,
                VoorschotBedrag = snapshot.Offerte.VoorschotBedrag,
                IsVoorschotBetaald = false   // opnieuw beginnen
            };

            db.Offertes.Add(nieuweOfferte);
            await db.SaveChangesAsync(); // ID nodig voor regels

            // ── Regels kopi ëren ────────────────────────────────────────
            foreach (var r in snapshot.Regels)
            {
                var regel = new OfferteRegel
                {
                    OfferteId          = nieuweOfferte.Id,
                    Titel              = r.Titel,
                    Opmerking          = r.Opmerking,
                    AantalStuks        = r.AantalStuks,
                    BreedteCm          = r.BreedteCm,
                    HoogteCm           = r.HoogteCm,
                    InlegBreedteCm     = r.InlegBreedteCm,
                    InlegHoogteCm      = r.InlegHoogteCm,
                    TypeLijstId        = r.TypeLijstId,
                    GlasId             = r.GlasId,
                    PassePartout1Id    = r.PassePartout1Id,
                    PassePartout2Id    = r.PassePartout2Id,
                    DiepteKernId       = r.DiepteKernId,
                    OpklevenId         = r.OpklevenId,
                    RugId              = r.RugId,
                    ExtraWerkMinuten   = r.ExtraWerkMinuten,
                    ExtraPrijs         = r.ExtraPrijs,
                    Korting            = r.Korting,
                    AfgesprokenPrijsExcl = r.AfgesprokenPrijsExcl,
                    LegacyCode         = r.LegacyCode,
                    TotaalExcl         = r.TotaalExcl,
                    SubtotaalExBtw     = r.SubtotaalExBtw,
                    BtwBedrag          = r.BtwBedrag,
                    TotaalInclBtw      = r.TotaalInclBtw
                };
                db.OfferteRegels.Add(regel);
            }

            // ── Archief markeren als hersteld ──────────────────────────
            archief.IsHersteld = true;
            archief.HersteldNaarOfferteId = nieuweOfferte.Id;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Archief {ArchiefId} hersteld als nieuwe offerte {NieuweOfferteId}.",
                archiefId, nieuweOfferte.Id);

            return nieuweOfferte.Id;
        }

        // ─────────────────────────────────────────────────────────────────
        //  GetAlleAsync
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<WerkBonArchief>> GetAlleAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.WerkBonArchieven
                .AsNoTracking()
                .OrderByDescending(a => a.GearchiveerdOp)
                .ToListAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  GetSnapshotAsync
        // ─────────────────────────────────────────────────────────────────

        public async Task<WerkBonArchiefSnapshot?> GetSnapshotAsync(int archiefId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var archief = await db.WerkBonArchieven
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == archiefId);

            if (archief is null) return null;

            return JsonSerializer.Deserialize<WerkBonArchiefSnapshot>(archief.Snapshot, _jsonOpts);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Privé helper — snapshot bouwen
        // ─────────────────────────────────────────────────────────────────

        private static WerkBonArchiefSnapshot BouwSnapshot(WerkBon werkBon, Offerte offerte)
        {
            var snap = new WerkBonArchiefSnapshot
            {
                AangemaaktOp = DateTime.UtcNow,
                SchemaVersie = 1,

                Offerte = new SnapshotOfferte
                {
                    Id                = offerte.Id,
                    Datum             = offerte.Datum,
                    Status            = offerte.Status.ToString(),
                    Opmerking         = offerte.Opmerking,
                    SubtotaalExBtw    = offerte.SubtotaalExBtw,
                    BtwBedrag         = offerte.BtwBedrag,
                    TotaalInclBtw     = offerte.TotaalInclBtw,
                    KortingPct        = offerte.KortingPct,
                    MeerPrijsIncl     = offerte.MeerPrijsIncl,
                    IsVoorschotBetaald = offerte.IsVoorschotBetaald,
                    VoorschotBedrag   = offerte.VoorschotBedrag,
                    GeplandeDatum     = offerte.GeplandeDatum,
                    DeadlineDatum     = offerte.DeadlineDatum,
                    GeschatteMinuten  = offerte.GeschatteMinuten
                },

                WerkBon = new SnapshotWerkBon
                {
                    Id                      = werkBon.Id,
                    Status                  = werkBon.Status.ToString(),
                    TotaalPrijsIncl         = werkBon.TotaalPrijsIncl,
                    AangemaaktOp            = werkBon.AangemaaktOp,
                    AfhaalDatum             = werkBon.AfhaalDatum,
                    StockReservationProcessed = werkBon.StockReservationProcessed
                }
            };

            // Klant
            if (offerte.Klant is { } k)
            {
                snap.Klant = new SnapshotKlant
                {
                    Id         = k.Id,
                    Voornaam   = k.Voornaam,
                    Achternaam = k.Achternaam,
                    Email      = k.Email,
                    Telefoon   = k.Telefoon,
                    Straat     = k.Straat,
                    Nummer     = k.Nummer,
                    Postcode   = k.Postcode,
                    Gemeente   = k.Gemeente,
                    BtwNummer  = k.BtwNummer,
                    Opmerking  = k.Opmerking
                };
            }

            // Regels
            foreach (var r in offerte.Regels)
            {
                snap.Regels.Add(new SnapshotOfferteRegel
                {
                    Id                   = r.Id,
                    Titel                = r.Titel,
                    Opmerking            = r.Opmerking,
                    AantalStuks          = r.AantalStuks,
                    BreedteCm            = r.BreedteCm,
                    HoogteCm             = r.HoogteCm,
                    InlegBreedteCm       = r.InlegBreedteCm,
                    InlegHoogteCm        = r.InlegHoogteCm,
                    TypeLijstId          = r.TypeLijstId,
                    TypeLijstNaam        = r.TypeLijst?.Artikelnummer,  // TypeLijst gebruikt Artikelnummer als naam
                    TypeLijstArtikelnummer = r.TypeLijst?.Artikelnummer,
                    GlasId               = r.GlasId,
                    GlasNaam             = r.Glas?.Naam,
                    PassePartout1Id      = r.PassePartout1Id,
                    PassePartout1Naam    = r.PassePartout1?.Naam,
                    PassePartout2Id      = r.PassePartout2Id,
                    PassePartout2Naam    = r.PassePartout2?.Naam,
                    DiepteKernId         = r.DiepteKernId,
                    DiepteKernNaam       = r.DiepteKern?.Naam,
                    OpklevenId           = r.OpklevenId,
                    OpklevenNaam         = r.Opkleven?.Naam,
                    RugId                = r.RugId,
                    RugNaam              = r.Rug?.Naam,
                    TotaalExcl           = r.TotaalExcl,
                    SubtotaalExBtw       = r.SubtotaalExBtw,
                    BtwBedrag            = r.BtwBedrag,
                    TotaalInclBtw        = r.TotaalInclBtw,
                    ExtraPrijs           = r.ExtraPrijs,
                    Korting              = r.Korting,
                    AfgesprokenPrijsExcl = r.AfgesprokenPrijsExcl,
                    ExtraWerkMinuten     = r.ExtraWerkMinuten,
                    LegacyCode           = r.LegacyCode
                });
            }

            // Taken
            foreach (var t in werkBon.Taken)
            {
                snap.Taken.Add(new SnapshotWerkTaak
                {
                    Id                   = t.Id,
                    Omschrijving         = t.Omschrijving,
                    GeplandVan           = t.GeplandVan,
                    GeplandTot           = t.GeplandTot,
                    DuurMinuten          = t.DuurMinuten,
                    Resource             = t.Resource,
                    WeekNotitie          = t.WeekNotitie,
                    IsBesteld            = t.IsBesteld,
                    BestelDatum          = t.BestelDatum,
                    IsOpVoorraad         = t.IsOpVoorraad,
                    VoorraadStatus       = t.VoorraadStatus.ToString(),
                    BenodigdeMeter       = t.BenodigdeMeter,
                    OfferteRegelId       = t.OfferteRegelId,
                    OfferteRegelTitel    = t.OfferteRegel?.Titel,
                    LeverancierBestelLijnId = t.LeverancierBestelLijnId
                });
            }

            return snap;
        }
    }
}
