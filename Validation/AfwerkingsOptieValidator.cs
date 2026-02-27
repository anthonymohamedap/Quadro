using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Validation;

public sealed class AfwerkingsOptieValidator : ICrudValidator<AfwerkingsOptie>
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public AfwerkingsOptieValidator(IDbContextFactory<AppDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public Task<ValidationResult> ValidateCreateAsync(AfwerkingsOptie entity)
        => ValidateCommonAsync(entity, isUpdate: false);

    public Task<ValidationResult> ValidateUpdateAsync(AfwerkingsOptie entity)
        => ValidateCommonAsync(entity, isUpdate: true);

    public async Task<ValidationResult> ValidateDeleteAsync(AfwerkingsOptie entity)
    {
        var r = new ValidationResult();

        if (entity is null)
        {
            r.Error("Optie", "Geen optie geselecteerd.");
            return r;
        }

        if (entity.Id <= 0)
            r.Error(nameof(entity.Id), "Ongeldige afwerkingsoptie (Id ontbreekt).");

        // (optioneel) hier kan je later FK checks doen (bv. optie gebruikt in offertes)
        await Task.CompletedTask;
        return r;
    }

    private async Task<ValidationResult> ValidateCommonAsync(AfwerkingsOptie o, bool isUpdate)
    {
        var r = new ValidationResult();

        if (o is null)
        {
            r.Error("Optie", "Geen optie geselecteerd.");
            return r;
        }

        if (isUpdate && o.Id <= 0)
            r.Error(nameof(o.Id), "Ongeldige afwerkingsoptie (Id ontbreekt).");

        if (o.AfwerkingsGroepId <= 0)
            r.Error(nameof(o.AfwerkingsGroepId), "Afwerkingsgroep is verplicht.");

        if (string.IsNullOrWhiteSpace(o.Naam))
            r.Error(nameof(o.Naam), "Naam is verplicht.");
        else if (o.Naam.Trim().Length > 50)
            r.Error(nameof(o.Naam), "Naam is te lang (max 50).");

        if (!IsValidVolgnummer(o.Volgnummer))
            r.Error(nameof(o.Volgnummer), "Volgnummer moet 1-9 of A-K zijn.");

        // bedragen: nooit negatief
        if (o.KostprijsPerM2 < 0)
            r.Error(nameof(o.KostprijsPerM2), "Kostprijs per m² mag niet negatief zijn.");

        if (o.VasteKost < 0)
            r.Error(nameof(o.VasteKost), "Vaste kost mag niet negatief zijn.");

        if (o.AfvalPercentage < 0 || o.AfvalPercentage > 100)
            r.Error(nameof(o.AfvalPercentage), "Afvalpercentage moet tussen 0 en 100 liggen.");

        if (o.WinstMarge < 0)
            r.Error(nameof(o.WinstMarge), "Winstmarge mag niet negatief zijn.");

        if (o.WerkMinuten < 0 || o.WerkMinuten > 1440)
            r.Error(nameof(o.WerkMinuten), "Werkminuten moet tussen 0 en 1440 liggen.");

        // leverancier check
        if (o.LeverancierId is not null && o.LeverancierId <= 0)
            r.Error(nameof(o.LeverancierId), "LeverancierId is ongeldig.");

        // warnings (niet blokkeren)
        if (o.KostprijsPerM2 == 0)
            r.Warn(nameof(o.KostprijsPerM2), "Kostprijs per m² is 0. Is dat zeker?");

        if (o.WinstMarge == 0)
            r.Warn(nameof(o.WinstMarge), "Winstmarge is 0. Is dat de bedoeling?");

        if (o.AfvalPercentage == 0)
            r.Warn(nameof(o.AfvalPercentage), "Afvalpercentage is 0. Is dat de bedoeling?");

        // Uniek volgnummer binnen groep (aanrader)
        await using var db = await _dbFactory.CreateDbContextAsync();
        var volg = o.Volgnummer;

        var exists = await db.AfwerkingsOpties.AnyAsync(a =>
            a.AfwerkingsGroepId == o.AfwerkingsGroepId &&
            a.Volgnummer == volg &&
            (!isUpdate || a.Id != o.Id));

        if (exists)
            r.Error(nameof(o.Volgnummer), "Dit volgnummer bestaat al binnen deze groep.");

        return r;
    }
    private static bool IsValidVolgnummer(char c)
    {
        c = char.ToUpperInvariant(c);
        return (c >= '1' && c <= '9') || (c >= 'A' && c <= 'K');
    }

    // Voor sorteren / volgende nummer bepalen (1..9 => 1..9, A..K => 10..20)
    public static int VolgnummerOrder(char c)
    {
        c = char.ToUpperInvariant(c);

        if (c >= '1' && c <= '9')
            return c - '0'; // '1' => 1

        if (c >= 'A' && c <= 'K')
            return 9 + (c - 'A' + 1); // A => 10

        return int.MaxValue; // ongeldig
    }

    // Optioneel: "volgend" teken bepalen (bv voor CreateNieuweOptieAsync)
    public static char? NextVolgnummer(char current)
    {
        current = char.ToUpperInvariant(current);

        if (current >= '1' && current <= '8') return (char)(current + 1);
        if (current == '9') return 'A';
        if (current >= 'A' && current <= 'J') return (char)(current + 1);
        return null; // na K: geen volgende
    }
}