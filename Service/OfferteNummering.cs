using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    /// <summary>
    /// Kent doorlopende, nooit-hergebruikte offertenummers toe.
    ///
    /// Waarom niet gewoon Offerte.Id gebruiken? Bij archiveren wordt de offerte-rij fysiek
    /// verwijderd (zie <see cref="OfferteArchiefService.ArchiveerAsync"/>), waardoor Id als
    /// zichtbaar nummer een gat in de lijst zou laten. OfferteNummer is daarom een apart veld dat
    /// zowel over de actieve Offertes-tabel als de OfferteArchieven-tabel heen doorloopt: het
    /// hoogste ooit uitgereikte nummer (actief of gearchiveerd) plus één.
    ///
    /// Bij herstellen vanuit het archief wordt het oorspronkelijke nummer hergebruikt (niet een
    /// nieuw nummer getrokken) — zie OfferteArchiefService.HerstellenAsync.
    ///
    /// Concurrency: er is bewust geen DB-unique-constraint op dit veld (in tegenstelling tot
    /// Factuur.FactuurNummer, dat wettelijk uniek moet zijn). Twee offertes op exact hetzelfde
    /// moment vanaf twee verschillende pc's aanmaken kan in theorie hetzelfde nummer opleveren;
    /// dat risico is aanvaard omdat offertenummers geen wettelijke/boekhoudkundige eis hebben
    /// (in tegenstelling tot facturen).
    /// </summary>
    public static class OfferteNummering
    {
        public static async Task<int> VolgendeAsync(AppDbContext db)
        {
            var maxActief = await db.Offertes.MaxAsync(o => (int?)o.OfferteNummer) ?? 0;
            var maxArchief = await db.OfferteArchieven.MaxAsync(a => (int?)a.OfferteNummer) ?? 0;
            return Math.Max(maxActief, maxArchief) + 1;
        }
    }
}
