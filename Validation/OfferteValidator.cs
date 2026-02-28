using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Validation;

public interface IOfferteValidator : ICrudValidator<Offerte>
{
    Task<ValidationResult> ValidateForPricingAsync(Offerte entity);
    Task<ValidationResult> ValidateForConfirmAsync(Offerte entity);
}

public sealed class OfferteValidator : IOfferteValidator
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public OfferteValidator(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public Task<ValidationResult> ValidateCreateAsync(Offerte entity) =>
        ValidateCommonAsync(entity, mode: "create");

    public Task<ValidationResult> ValidateUpdateAsync(Offerte entity) =>
        ValidateCommonAsync(entity, mode: "update");

    public async Task<ValidationResult> ValidateDeleteAsync(Offerte entity)
    {
        var vr = new ValidationResult();

        if (entity.Id <= 0)
        {
            vr.Error(nameof(Offerte.Id), "Ongeldig offerte-id.");
            return vr;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var snapshot = await db.Offertes
            .AsNoTracking()
            .Include(o => o.WerkBon)
            .FirstOrDefaultAsync(o => o.Id == entity.Id);

        if (snapshot is null)
        {
            vr.Error("Offerte", "Offerte bestaat niet (mogelijk al verwijderd).");
            return vr;
        }

        if (snapshot.Status != OfferteStatus.Concept)
            vr.Error(nameof(Offerte.Status), "Je kan enkel offertes met status 'Nieuw' verwijderen.");

        if (snapshot.WerkBon is not null)
            vr.Error(nameof(Offerte.WerkBon), "Deze offerte heeft al een werkbon en kan niet meer verwijderd worden.");

        return vr;
    }

    public Task<ValidationResult> ValidateForPricingAsync(Offerte entity) =>
        ValidateCommonAsync(entity, mode: "pricing");

    public Task<ValidationResult> ValidateForConfirmAsync(Offerte entity) =>
        ValidateCommonAsync(entity, mode: "confirm");

    private async Task<ValidationResult> ValidateCommonAsync(Offerte entity, string mode)
    {
        var vr = new ValidationResult();

        // ---- basis (structureel + business) ----
        if (entity is null)
        {
            vr.Error("Offerte", "Geen offerte geladen.");
            return vr;
        }

        // Datum: default DateTime is rommel → fixen of blokkeren
        if (entity.Datum == default)
            vr.Warn(nameof(Offerte.Datum), "Datum was leeg en wordt best op vandaag gezet.");

        // Regels
        if (entity.Regels is null || !entity.Regels.Any())
        {
            vr.Error(nameof(Offerte.Regels), "Voeg minstens één regel toe.");
            return vr;
        }

        // Klant
        if (mode is "pricing" or "confirm")
        {
            if (!entity.KlantId.HasValue)
                vr.Error(nameof(Offerte.KlantId), "Selecteer een klant.");
        }
        else
        {
            // bij save mag draft zonder klant, maar geef warning
            if (!entity.KlantId.HasValue)
                vr.Warn(nameof(Offerte.KlantId), "Geen klant geselecteerd (draft).");
        }

        // Per regel
        var idx = 0;
        foreach (var r in entity.Regels)
        {
            idx++;
            var prefix = $"Regel {idx}";

            if (r.AantalStuks < 1)
                vr.Error($"{prefix}.{nameof(OfferteRegel.AantalStuks)}", "Aantal stuks moet minstens 1 zijn.");

            if (r.BreedteCm <= 0 || r.HoogteCm <= 0)
                vr.Error($"{prefix}.Afmetingen", "Breedte en hoogte moeten groter zijn dan 0.");

            // TypeLijst: voor pricing/confirm verplicht. Voor save: warning.
            var tlId = r.TypeLijstId;
            if (!tlId.HasValue)
            {
                if (mode is "pricing" or "confirm")
                    vr.Error($"{prefix}.{nameof(OfferteRegel.TypeLijstId)}", "Selecteer een TypeLijst.");
                else
                    vr.Warn($"{prefix}.{nameof(OfferteRegel.TypeLijstId)}", "Geen TypeLijst geselecteerd.");
            }

            // Inleg: als 1 ingevuld is, moeten beide logisch zijn
            if (r.InlegBreedteCm.HasValue ^ r.InlegHoogteCm.HasValue)
                vr.Warn($"{prefix}.Inleg", "Vul zowel inleg-breedte als inleg-hoogte in, of laat beide leeg.");

            if (r.InlegBreedteCm is not null && r.InlegBreedteCm <= 0)
                vr.Error($"{prefix}.InlegBreedteCm", "Inlegbreedte moet groter zijn dan 0.");

            if (r.InlegHoogteCm is not null && r.InlegHoogteCm <= 0)
                vr.Error($"{prefix}.InlegHoogteCm", "Inleghoogte moet groter zijn dan 0.");

            // Afgesproken prijs
            if (r.AfgesprokenPrijsExcl is not null && r.AfgesprokenPrijsExcl < 0)
                vr.Error($"{prefix}.{nameof(OfferteRegel.AfgesprokenPrijsExcl)}", "Afgesproken prijs mag niet negatief zijn.");

            // Extra's
            if (r.ExtraWerkMinuten < 0)
                vr.Error($"{prefix}.{nameof(OfferteRegel.ExtraWerkMinuten)}", "Extra werkminuten mag niet negatief zijn.");

            if (r.ExtraPrijs < 0)
                vr.Error($"{prefix}.{nameof(OfferteRegel.ExtraPrijs)}", "Extra prijs mag niet negatief zijn.");

            if (r.Korting < 0)
                vr.Warn($"{prefix}.{nameof(OfferteRegel.Korting)}", "Korting is negatief (bedoel je toeslag?).");

            // LegacyCode (optioneel) – als ingevuld moet 6 tekens zijn
            if (!string.IsNullOrWhiteSpace(r.LegacyCode) && r.LegacyCode.Trim().Length != 6)
                vr.Error($"{prefix}.{nameof(OfferteRegel.LegacyCode)}", "Legacy-code moet exact 6 tekens zijn.");
        }

        // ---- DB checks (bestaat klant/typeLijst) ----
        // Alleen doen als er nog geen errors zijn op basis (optioneel, maar sneller UX)
        await using var db = await _dbFactory.CreateDbContextAsync();

        if (entity.KlantId.HasValue)
        {
            var klantOk = await db.Klanten.AsNoTracking().AnyAsync(k => k.Id == entity.KlantId.Value);
            if (!klantOk) vr.Error(nameof(Offerte.KlantId), "Geselecteerde klant bestaat niet (meer).");
        }

        // TypeLijst existence check voor regels waar TypeLijstId is ingevuld
        var typeIds = entity.Regels.Where(r => r.TypeLijstId.HasValue).Select(r => r.TypeLijstId!.Value).Distinct().ToList();
        if (typeIds.Count > 0)
        {
            var existing = await db.TypeLijsten.AsNoTracking()
                .Where(t => typeIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync();

            var missing = typeIds.Except(existing).ToList();
            if (missing.Count > 0)
                vr.Error("TypeLijst", $"TypeLijst bestaat niet (meer): {string.Join(", ", missing)}");
        }

        return vr;
    }
}
