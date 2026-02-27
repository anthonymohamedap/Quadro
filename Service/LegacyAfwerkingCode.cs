using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Services
{
    public static class LegacyAfwerkingCode
    {
        private static char Normalize(char c)
            => c == '0' ? '0' : char.ToUpperInvariant(c);

        private static char? DecodeToVolgnummer(char c)
        {
            c = Normalize(c);

            if (c == '0') return null;

            if (c >= '1' && c <= '9') return c;
            if (c >= 'A' && c <= 'K') return c;

            throw new ArgumentException($"Ongeldig teken in afwerkingcode: '{c}' (toegelaten: 0, 1-9, A-K).");
        }

        public static string Generate(OfferteRegel o)
        {
            // 6 tekens: G P P D O R
            var code = new[]
            {
                Normalize(o.Glas?.Volgnummer ?? '0'),
                Normalize(o.PassePartout1?.Volgnummer ?? '0'),
                Normalize(o.PassePartout2?.Volgnummer ?? '0'),
                Normalize(o.DiepteKern?.Volgnummer ?? '0'),
                Normalize(o.Opkleven?.Volgnummer ?? '0'),
                Normalize(o.Rug?.Volgnummer ?? '0'),
            };

            return new string(code);
        }

        public static async Task ApplyAsync(AppDbContext db, OfferteRegel o, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length < 6)
                throw new ArgumentException("Code vereist 6 tekens (G P P D O R).");

            var volg = code.Take(6).Select(DecodeToVolgnummer).ToArray(); // char?[]

            async Task<int> GroepId(char groepCode)
            {
                var id = await db.AfwerkingsGroepen
                    .Where(x => x.Code == groepCode)
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync();

                return id ?? throw new InvalidOperationException($"AfwerkingsGroep '{groepCode}' niet gevonden in DB.");
            }

            int gId = await GroepId('G');
            int pId = await GroepId('P');
            int dId = await GroepId('D');
            int oId = await GroepId('O');
            int rId = await GroepId('R');

            async Task<AfwerkingsOptie?> Find(int groepId, char? volgnummer)
            {
                if (!volgnummer.HasValue) return null;

                var v = Normalize(volgnummer.Value);

                return await db.AfwerkingsOpties
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a =>
                        a.AfwerkingsGroepId == groepId &&
                        a.Volgnummer == v);
            }

            o.Glas = await Find(gId, volg[0]);
            o.PassePartout1 = await Find(pId, volg[1]);
            o.PassePartout2 = await Find(pId, volg[2]);
            o.DiepteKern = await Find(dId, volg[3]);
            o.Opkleven = await Find(oId, volg[4]);
            o.Rug = await Find(rId, volg[5]);
        }
    }
}