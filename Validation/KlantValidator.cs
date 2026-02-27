using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuadroApp.Validation;

public sealed class KlantValidator : ICrudValidator<Klant>
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public KlantValidator(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public Task<ValidationResult> ValidateCreateAsync(Klant k) => ValidateCommonAsync(k, isUpdate: false);
    public Task<ValidationResult> ValidateUpdateAsync(Klant k) => ValidateCommonAsync(k, isUpdate: true);

    public async Task<ValidationResult> ValidateDeleteAsync(Klant k)
    {
        var r = new ValidationResult();

        if (k.Id <= 0)
            r.Error(nameof(k.Id), "Ongeldige klant.");

        await using var db = await _factory.CreateDbContextAsync();
        var hasOffertes = await db.Offertes.AnyAsync(o => o.KlantId == k.Id);
        if (hasOffertes)
            r.Error("Delete", "Klant kan niet verwijderd worden: er zijn offertes gekoppeld.");

        return r;
    }

    private async Task<ValidationResult> ValidateCommonAsync(Klant k, bool isUpdate)
    {
        var r = new ValidationResult();

        if (isUpdate && k.Id <= 0)
            r.Error(nameof(k.Id), "Ongeldige klant.");

        var voornaam = (k.Voornaam ?? "").Trim();
        var achternaam = (k.Achternaam ?? "").Trim();

        // ✅ Minstens één naam
        if (string.IsNullOrWhiteSpace(voornaam) && string.IsNullOrWhiteSpace(achternaam))
            r.Error("Naam", "Voornaam of achternaam is verplicht.");

        // ✅ Email format + uniek (optioneel)
        if (!string.IsNullOrWhiteSpace(k.Email))
        {
            var email = k.Email.Trim();

            // simpel maar effectief
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                r.Error(nameof(k.Email), "E-mail adres lijkt ongeldig.");

            await using var db = await _factory.CreateDbContextAsync();
            var emailLower = email.ToLowerInvariant();

            var exists = await db.Klanten.AnyAsync(x =>
                x.Email != null &&
                x.Email.ToLower() == emailLower &&
                (!isUpdate || x.Id != k.Id));

            if (exists)
                r.Error(nameof(k.Email), "E-mail bestaat al bij een andere klant.");
        }
        else
        {
            // Email niet verplicht: warning kan je behouden of verwijderen
            // r.Warn(nameof(k.Email), "Geen e-mail ingevuld.");
        }

        // ✅ Telefoon (toelaten: +, spaties, /, -)
        if (!string.IsNullOrWhiteSpace(k.Telefoon))
        {
            var tel = k.Telefoon.Trim();
            if (!Regex.IsMatch(tel, @"^[0-9+\s\/\-\(\)]{6,25}$"))
                r.Error(nameof(k.Telefoon), "Telefoonnummer bevat ongeldige tekens.");
        }

        // ✅ Postcode BE: 4 cijfers (niet verplicht)
        if (!string.IsNullOrWhiteSpace(k.Postcode))
        {
            var pc = k.Postcode.Trim();
            if (!Regex.IsMatch(pc, @"^\d{4}$"))
                r.Error(nameof(k.Postcode), "Postcode moet 4 cijfers zijn.");
        }

        // ✅ Straat/Nummer consistency (warning)
        if (!string.IsNullOrWhiteSpace(k.Straat) && string.IsNullOrWhiteSpace(k.Nummer))
            r.Warn(nameof(k.Nummer), "Nummer ontbreekt bij straat.");

        if (string.IsNullOrWhiteSpace(k.Straat) && !string.IsNullOrWhiteSpace(k.Nummer))
            r.Warn(nameof(k.Straat), "Straat ontbreekt bij nummer.");

        // ✅ BTW-nummer (Belgisch) — niet verplicht, maar als ingevuld: formaat check
        if (!string.IsNullOrWhiteSpace(k.BtwNummer))
        {
            var btw = k.BtwNummer.Trim().Replace(" ", "").Replace(".", "").Replace("-", "");

            // accepteer BE + 10 cijfers of enkel 10 cijfers
            if (btw.StartsWith("BE", StringComparison.OrdinalIgnoreCase))
                btw = btw.Substring(2);

            if (!Regex.IsMatch(btw, @"^\d{10}$"))
                r.Error(nameof(k.BtwNummer), "BTW-nummer moet 10 cijfers zijn (optioneel met 'BE').");
        }

        // ✅ Gemeente (niet verplicht, maar als ingevuld: minimal length)
        if (!string.IsNullOrWhiteSpace(k.Gemeente) && k.Gemeente.Trim().Length < 2)
            r.Error(nameof(k.Gemeente), "Gemeente is te kort.");

        return r;
    }
}