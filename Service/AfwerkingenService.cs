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
                .ThenBy(o => o.Naam)
                .ToListAsync();
        }

        public async Task SaveOptieAsync(AfwerkingsOptie optie)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.Update(optie);
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
    }

}
