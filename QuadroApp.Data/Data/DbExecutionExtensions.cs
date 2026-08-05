using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace QuadroApp.Data
{
    /// <summary>
    /// US-43 — voert een transactionele body uit binnen de EF <c>execution strategy</c>,
    /// zodat retry-on-failure (PostgreSQL) werkt zonder de fout "The configured execution
    /// strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions."
    ///
    /// De <paramref name="body"/> opent zélf zijn transactie (<c>BeginTransactionAsync</c> +
    /// <c>CommitAsync</c>); die HELE transactie is één herhaalbare eenheid. Bij een transient
    /// fout rolt de mislukte poging volledig terug (de transactie is niet gecommit) en draait
    /// de body opnieuw. De change tracker wordt vóór elke poging geleegd, zodat een retry geen
    /// dubbele inserts/voorraadmutaties/factuurnummers oplevert.
    ///
    /// <b>Belangrijk:</b> alle reads én writes moeten BINNEN de body gebeuren (niet ervoor
    /// opgebouwd), en er mogen geen al-gecommitte neveneffecten in de body zitten die bij een
    /// retry dubbel zouden lopen.
    ///
    /// Op SQLite is de strategy niet-herhalend: de body draait exact één keer en het gedrag is
    /// identiek aan voorheen (de <c>ChangeTracker.Clear</c> is dan een no-op op de verse context).
    /// </summary>
    public static class DbExecutionExtensions
    {
        public static async Task ExecuteWithRetryAsync(this AppDbContext db, Func<Task> body)
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await body();
            });
        }

        public static async Task<T> ExecuteWithRetryAsync<T>(this AppDbContext db, Func<Task<T>> body)
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                return await body();
            });
        }
    }
}
