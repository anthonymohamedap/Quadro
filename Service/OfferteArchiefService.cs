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
    public class OfferteArchiefService : IOfferteArchiefService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<OfferteArchiefService> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public OfferteArchiefService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<OfferteArchiefService> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger  = logger  ?? throw new ArgumentNullException(nameof(logger));
        }

        // ─── ArchiveerAsync ──────────────────────────────────────────────────

        public async Task<OfferteArchief> ArchiveerAsync(int offerteId, string? reden = null)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var offerte = await db.Offertes
                .Include(o => o.Klant)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.TypeLijst)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.Glas)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.PassePartout1)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.PassePartout2)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.DiepteKern)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.Opkleven)
                .Include(o => o.Regels)
                    .ThenInclude(r => r.Rug)
                .Include(o => o.WerkBon)
                    .ThenInclude(w => w!.Taken)
                        .ThenInclude(t => t.OfferteRegel)
                .FirstOrDefaultAsync(o => o.Id == offerteId)
                ?? throw new InvalidOperationException($"Offerte {offerteId} niet gevonden.");

            // ── Bouw snapshot ────────────────────────────────────────────────
            var snap = BouwSnapshot(offerte);
            var snapJson = JsonSerializer.Serialize(snap, _json);

            var klant = offerte.Klant;
            var archief = new OfferteArchief
            {
                OrigineleOfferteId = offerte.Id,
                KlantNaam          = klant is not null
                                       ? $"{klant.Voornaam} {klant.Achternaam}".Trim()
                                       : "(geen klant)",
                KlantId            = klant?.Id,
                OfferteDatum       = offerte.Datum,
                Jaar               = offerte.Datum.Year,
                StatusOpMoment     = offerte.Status.ToString(),
                TotaalInclBtw      = offerte.TotaalInclBtw,
                HadWerkBon         = offerte.WerkBon is not null,
                GearchiveerdOp     = DateTime.UtcNow,
                Reden              = reden,
                Snapshot           = snapJson,
                IsHersteld         = false
            };

            db.OfferteArchieven.Add(archief);
            await db.SaveChangesAsync(); // archief opslaan vóór verwijdering

            // ── Ontkoppel Facturen die naar deze offerte / werkbon wijzen ────────
            // Factuur.OfferteId en WerkBonId zijn nullable met Restrict-delete;
            // ze moeten op null gezet worden vóór we de offerte verwijderen.
            var werkBonId = offerte.WerkBon?.Id;
            var facturen = await db.Facturen
                .Where(f => f.OfferteId == offerte.Id ||
                            (werkBonId != null && f.WerkBonId == werkBonId))
                .ToListAsync();

            foreach (var f in facturen)
            {
                if (f.OfferteId == offerte.Id)
                    f.OfferteId = null;
                if (werkBonId != null && f.WerkBonId == werkBonId)
                    f.WerkBonId = null;
            }

            if (facturen.Count > 0)
                await db.SaveChangesAsync();

            // ── Verwijder originele offerte (cascade verwijdert regels/werkbon/taken) ──
            db.Offertes.Remove(offerte);
            await db.SaveChangesAsync();

            await tx.CommitAsync();

            _logger.LogInformation(
                "Offerte {OfferteId} gearchiveerd als archief-entry {ArchiefId}.",
                offerteId, archief.Id);

            return archief;
        }

        // ─── HerstellenAsync ─────────────────────────────────────────────────

        public async Task<int> HerstellenAsync(int archiefId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var archief = await db.OfferteArchieven.FirstOrDefaultAsync(a => a.Id == archiefId)
                ?? throw new InvalidOperationException($"Archief {archiefId} niet gevonden.");

            if (archief.IsHersteld)
                throw new InvalidOperationException(
                    $"Archief {archiefId} is al hersteld naar offerte {archief.HersteldNaarOfferteId}.");

            var snap = JsonSerializer.Deserialize<OfferteArchiefSnapshot>(archief.Snapshot, _json)
                ?? throw new InvalidOperationException("Snapshot kon niet worden gelezen.");

            // ── Nieuwe offerte ────────────────────────────────────────────────
            var nieuweOfferte = new Offerte
            {
                KlantId            = archief.KlantId,
                Datum              = DateTime.Today,
                Status             = OfferteStatus.Concept,
                Opmerking          = $"[Hersteld uit archief #{archiefId} – orig. #{archief.OrigineleOfferteId}]"
                                     + (snap.Offerte.Opmerking is { Length: > 0 } op ? " " + op : ""),
                SubtotaalExBtw     = snap.Offerte.SubtotaalExBtw,
                BtwBedrag          = snap.Offerte.BtwBedrag,
                TotaalInclBtw      = snap.Offerte.TotaalInclBtw,
                KortingPct         = snap.Offerte.KortingPct,
                MeerPrijsIncl      = snap.Offerte.MeerPrijsIncl,
                VoorschotBedrag    = snap.Offerte.VoorschotBedrag,
                IsVoorschotBetaald = false,
                GeplandeDatum      = snap.Offerte.GeplandeDatum,
                DeadlineDatum      = snap.Offerte.DeadlineDatum,
                GeschatteMinuten   = snap.Offerte.GeschatteMinuten
            };

            db.Offertes.Add(nieuweOfferte);
            await db.SaveChangesAsync();

            // ── Regels herstellen ─────────────────────────────────────────────
            foreach (var r in snap.Regels)
            {
                db.OfferteRegels.Add(new OfferteRegel
                {
                    OfferteId           = nieuweOfferte.Id,
                    Titel               = r.Titel,
                    Opmerking           = r.Opmerking,
                    AantalStuks         = r.AantalStuks,
                    BreedteCm           = r.BreedteCm,
                    HoogteCm            = r.HoogteCm,
                    InlegBreedteCm      = r.InlegBreedteCm,
                    InlegHoogteCm       = r.InlegHoogteCm,
                    TypeLijstId         = r.TypeLijstId,
                    GlasId              = r.GlasId,
                    PassePartout1Id     = r.PassePartout1Id,
                    PassePartout2Id     = r.PassePartout2Id,
                    DiepteKernId        = r.DiepteKernId,
                    OpklevenId          = r.OpklevenId,
                    RugId               = r.RugId,
                    ExtraWerkMinuten    = r.ExtraWerkMinuten,
                    ExtraPrijs          = r.ExtraPrijs,
                    Korting             = r.Korting,
                    AfgesprokenPrijsExcl= r.AfgesprokenPrijsExcl,
                    LegacyCode          = r.LegacyCode,
                    TotaalExcl          = r.TotaalExcl,
                    SubtotaalExBtw      = r.SubtotaalExBtw,
                    BtwBedrag           = r.BtwBedrag,
                    TotaalInclBtw       = r.TotaalInclBtw
                });
            }

            archief.IsHersteld = true;
            archief.HersteldNaarOfferteId = nieuweOfferte.Id;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation(
                "Archief {ArchiefId} hersteld als nieuwe offerte {NieuweOfferteId}.",
                archiefId, nieuweOfferte.Id);

            return nieuweOfferte.Id;
        }

        // ─── GetAlleAsync ─────────────────────────────────────────────────────

        public async Task<List<OfferteArchief>> GetAlleAsync(int jaar = 0)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var q = db.OfferteArchieven.AsNoTracking().AsQueryable();

            if (jaar > 0)
                q = q.Where(a => a.Jaar == jaar);

            return await q
                .OrderByDescending(a => a.GearchiveerdOp)
                .ToListAsync();
        }

        // ─── GetJarenAsync ────────────────────────────────────────────────────

        public async Task<List<int>> GetJarenAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.OfferteArchieven
                .AsNoTracking()
                .Select(a => a.Jaar)
                .Distinct()
                .OrderByDescending(j => j)
                .ToListAsync();
        }

        // ─── GetSnapshotAsync ─────────────────────────────────────────────────

        public async Task<OfferteArchiefSnapshot?> GetSnapshotAsync(int archiefId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            var a = await db.OfferteArchieven
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == archiefId);

            return a is null
                ? null
                : JsonSerializer.Deserialize<OfferteArchiefSnapshot>(a.Snapshot, _json);
        }

        // ─── VerwijderenAsync ─────────────────────────────────────────────────

        public async Task VerwijderenAsync(int archiefId)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var archief = await db.OfferteArchieven.FirstOrDefaultAsync(a => a.Id == archiefId)
                ?? throw new InvalidOperationException($"Archief {archiefId} niet gevonden.");

            db.OfferteArchieven.Remove(archief);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "Archief-entry {ArchiefId} permanent verwijderd.", archiefId);
        }

        // ─── Privé snapshot builder ────────────────────────────────────────────

        private static OfferteArchiefSnapshot BouwSnapshot(Offerte offerte)
        {
            var snap = new OfferteArchiefSnapshot
            {
                AangemaaktOp = DateTime.UtcNow,
                SchemaVersie = 1,

                Offerte = new SnapshotOfferte
                {
                    Id                 = offerte.Id,
                    Datum              = offerte.Datum,
                    Status             = offerte.Status.ToString(),
                    Opmerking          = offerte.Opmerking,
                    SubtotaalExBtw     = offerte.SubtotaalExBtw,
                    BtwBedrag          = offerte.BtwBedrag,
                    TotaalInclBtw      = offerte.TotaalInclBtw,
                    KortingPct         = offerte.KortingPct,
                    MeerPrijsIncl      = offerte.MeerPrijsIncl,
                    IsVoorschotBetaald = offerte.IsVoorschotBetaald,
                    VoorschotBedrag    = offerte.VoorschotBedrag,
                    GeplandeDatum      = offerte.GeplandeDatum,
                    DeadlineDatum      = offerte.DeadlineDatum,
                    GeschatteMinuten   = offerte.GeschatteMinuten
                }
            };

            // Klant
            if (offerte.Klant is { } k)
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

            // Regels
            foreach (var r in offerte.Regels)
                snap.Regels.Add(new SnapshotOfferteRegel
                {
                    Id                    = r.Id,
                    Titel                 = r.Titel,
                    Opmerking             = r.Opmerking,
                    AantalStuks           = r.AantalStuks,
                    BreedteCm             = r.BreedteCm,
                    HoogteCm              = r.HoogteCm,
                    InlegBreedteCm        = r.InlegBreedteCm,
                    InlegHoogteCm         = r.InlegHoogteCm,
                    TypeLijstId           = r.TypeLijstId,
                    TypeLijstNaam         = r.TypeLijst?.Artikelnummer,
                    TypeLijstArtikelnummer= r.TypeLijst?.Artikelnummer,
                    GlasId                = r.GlasId,
                    GlasNaam              = r.Glas?.Naam,
                    PassePartout1Id       = r.PassePartout1Id,
                    PassePartout1Naam     = r.PassePartout1?.Naam,
                    PassePartout2Id       = r.PassePartout2Id,
                    PassePartout2Naam     = r.PassePartout2?.Naam,
                    DiepteKernId          = r.DiepteKernId,
                    DiepteKernNaam        = r.DiepteKern?.Naam,
                    OpklevenId            = r.OpklevenId,
                    OpklevenNaam          = r.Opkleven?.Naam,
                    RugId                 = r.RugId,
                    RugNaam               = r.Rug?.Naam,
                    TotaalExcl            = r.TotaalExcl,
                    SubtotaalExBtw        = r.SubtotaalExBtw,
                    BtwBedrag             = r.BtwBedrag,
                    TotaalInclBtw         = r.TotaalInclBtw,
                    ExtraPrijs            = r.ExtraPrijs,
                    Korting               = r.Korting,
                    AfgesprokenPrijsExcl  = r.AfgesprokenPrijsExcl,
                    ExtraWerkMinuten      = r.ExtraWerkMinuten,
                    LegacyCode            = r.LegacyCode
                });

            // WerkBon + Taken
            if (offerte.WerkBon is { } w)
            {
                snap.WerkBon = new SnapshotWerkBon
                {
                    Id                       = w.Id,
                    Status                   = w.Status.ToString(),
                    TotaalPrijsIncl          = w.TotaalPrijsIncl,
                    AangemaaktOp             = w.AangemaaktOp,
                    AfhaalDatum              = w.AfhaalDatum,
                    StockReservationProcessed= w.StockReservationProcessed
                };

                foreach (var t in w.Taken)
                    snap.Taken.Add(new SnapshotWerkTaak
                    {
                        Id                     = t.Id,
                        Omschrijving           = t.Omschrijving,
                        GeplandVan             = t.GeplandVan,
                        GeplandTot             = t.GeplandTot,
                        DuurMinuten            = t.DuurMinuten,
                        Resource               = t.Resource,
                        WeekNotitie            = t.WeekNotitie,
                        IsBesteld              = t.IsBesteld,
                        BestelDatum            = t.BestelDatum,
                        IsOpVoorraad           = t.IsOpVoorraad,
                        VoorraadStatus         = t.VoorraadStatus.ToString(),
                        BenodigdeMeter         = t.BenodigdeMeter,
                        OfferteRegelId         = t.OfferteRegelId,
                        OfferteRegelTitel      = t.OfferteRegel?.Titel,
                        LeverancierBestelLijnId= t.LeverancierBestelLijnId
                    });
            }

            return snap;
        }
    }
}
