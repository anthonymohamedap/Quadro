using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Validation;

public sealed class TypeLijstValidator : ICrudValidator<TypeLijst>
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    public TypeLijstValidator(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public Task<ValidationResult> ValidateCreateAsync(TypeLijst t) => ValidateCommonAsync(t, isUpdate: false);
    public Task<ValidationResult> ValidateUpdateAsync(TypeLijst t) => ValidateCommonAsync(t, isUpdate: true);

    public async Task<ValidationResult> ValidateDeleteAsync(TypeLijst t)
    {
        var r = new ValidationResult();
        if (t.Id <= 0) r.Error(nameof(t.Id), "Ongeldige lijst (Id ontbreekt).");
        return r;
    }

    private async Task<ValidationResult> ValidateCommonAsync(TypeLijst t, bool isUpdate)
    {
        var r = new ValidationResult();

        if (isUpdate && t.Id <= 0)
            r.Error(nameof(t.Id), "Ongeldige lijst (Id ontbreekt).");

        if (string.IsNullOrWhiteSpace(t.Artikelnummer))
            r.Error(nameof(t.Artikelnummer), "Artikelnummer is verplicht.");
        else if (t.Artikelnummer.Length > 20)
            r.Error(nameof(t.Artikelnummer), "Artikelnummer mag max 20 tekens zijn.");

        if (t.LeverancierId <= 0)
            r.Error(nameof(t.LeverancierId), "Leverancier is verplicht.");

        if (t.BreedteCm <= 0)
            r.Error(nameof(t.BreedteCm), "BreedteCm moet groter zijn dan 0.");

        if (t.PrijsPerMeter < 0) r.Error(nameof(t.PrijsPerMeter), "PrijsPerMeter mag niet negatief zijn.");
        if (t.VasteKost < 0) r.Error(nameof(t.VasteKost), "VasteKost mag niet negatief zijn.");
        if (t.AfvalPercentage < 0 || t.AfvalPercentage > 100)
            r.Error(nameof(t.AfvalPercentage), "AfvalPercentage moet tussen 0 en 100 liggen.");
        if (t.WinstMargeFactor < 0)
            r.Error(nameof(t.WinstMargeFactor), "WinstMargeFactor mag niet negatief zijn.");
        if (t.WerkMinuten < 0)
            r.Error(nameof(t.WerkMinuten), "WerkMinuten mag niet negatief zijn.");
        if (t.VoorraadMeter < 0) r.Warn(nameof(t.VoorraadMeter), "VoorraadMeter is negatief (controleer).");
        if (t.MinimumVoorraad < 0) r.Warn(nameof(t.MinimumVoorraad), "MinimumVoorraad is negatief (controleer).");

        // Uniek artikelnummer check (optioneel maar sterk)
        if (!string.IsNullOrWhiteSpace(t.Artikelnummer))
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.TypeLijsten.AnyAsync(x =>
                x.Artikelnummer == t.Artikelnummer &&
                (!isUpdate || x.Id != t.Id));

            if (exists)
                r.Error(nameof(t.Artikelnummer), "Artikelnummer bestaat al.");
        }

        return r;
    }
}