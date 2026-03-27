using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    using Microsoft.EntityFrameworkCore;
    using QuadroApp.Data;
    using QuadroApp.Model.DB;
    using QuadroApp.Service.Interfaces;
    using QuadroApp.Validation;

    public sealed class AfwerkingenService : IAfwerkingenService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;

        public AfwerkingenService(IDbContextFactory<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<AfwerkingsGroep>> GetGroepenAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AfwerkingsGroepen
                .AsNoTracking()
                .OrderBy(g => g.Code)
                .ToListAsync();
        }

        public async Task<List<Leverancier>> GetLeveranciersAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Leveranciers
                .AsNoTracking()
                .OrderBy(l => l.Naam)
                .ToListAsync();
        }

        public async Task<List<AfwerkingsOptie>> GetOptiesAsync(int? groepId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.AfwerkingsOpties
                .AsNoTracking()
                .Include(o => o.Leverancier)
                .Include(o => o.AfwerkingsGroep)
                .AsQueryable();

            if (groepId.HasValue)
                q = q.Where(o => o.AfwerkingsGroepId == groepId.Value);

            return await q
                .OrderBy(o => o.Volgnummer)
                .ThenBy(o => o.Kleur)
                .ThenBy(o => o.Naam)
                .ToListAsync();
        }

        public async Task SaveOptieAsync(AfwerkingsOptie optie)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var bestaand = await db.AfwerkingsOpties.FirstOrDefaultAsync(x => x.Id == optie.Id);
            if (bestaand is null)
                throw new System.InvalidOperationException("Afwerkingsoptie niet gevonden.");

            bestaand.AfwerkingsGroepId = optie.AfwerkingsGroepId;
            bestaand.Naam = optie.Naam.Trim();
            bestaand.Volgnummer = char.ToUpperInvariant(optie.Volgnummer);
            bestaand.Kleur = NormalizeKleur(optie.Kleur);
            bestaand.KostprijsPerM2 = optie.KostprijsPerM2;
            bestaand.WinstMarge = optie.WinstMarge;
            bestaand.AfvalPercentage = optie.AfvalPercentage;
            bestaand.VasteKost = optie.VasteKost;
            bestaand.WerkMinuten = optie.WerkMinuten;
            bestaand.LeverancierId = optie.LeverancierId;

            var familieleden = await db.AfwerkingsOpties
                .Where(x => x.AfwerkingsGroepId == bestaand.AfwerkingsGroepId
                    && x.Volgnummer == bestaand.Volgnummer
                    && x.Id != bestaand.Id)
                .ToListAsync();

            foreach (var familielid in familieleden)
            {
                familielid.KostprijsPerM2 = bestaand.KostprijsPerM2;
                familielid.WinstMarge = bestaand.WinstMarge;
                familielid.AfvalPercentage = bestaand.AfvalPercentage;
                familielid.VasteKost = bestaand.VasteKost;
                familielid.WerkMinuten = bestaand.WerkMinuten;
            }

            await db.SaveChangesAsync();
        }

        public async Task<AfwerkingsOptie> CreateNieuweOptieAsync(int groepId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var maxChar = await db.AfwerkingsOpties
    .Where(o => o.AfwerkingsGroepId == groepId)
    .Select(o => (char?)o.Volgnummer)
    .ToListAsync();

            char next = '1';

            if (maxChar.Count > 0)
            {
                var last = maxChar
                    .OrderBy(c => AfwerkingsOptieValidator.VolgnummerOrder(c ?? '?'))
                    .LastOrDefault();

                var n = AfwerkingsOptieValidator.NextVolgnummer(last ?? '0');
                next = n ?? 'K'; // of: throw / blokkeren als je na K niet verder wil
            }

            var nieuw = new AfwerkingsOptie
            {
                AfwerkingsGroepId = groepId,
                Naam = "Nieuwe optie",
                Volgnummer = next,
                Kleur = "Standaard",
                WinstMarge = 0.25m,
                AfvalPercentage = 0m
            };



            db.AfwerkingsOpties.Add(nieuw);
            await db.SaveChangesAsync();

            return nieuw;
        }


        public async Task DeleteOptieAsync(AfwerkingsOptie optie)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.AfwerkingsOpties.Remove(optie);
            await db.SaveChangesAsync();
        }

        private static string NormalizeKleur(string? kleur)
            => string.IsNullOrWhiteSpace(kleur) ? "Standaard" : kleur.Trim();
    }

}
