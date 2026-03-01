using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<TypeLijst> TypeLijsten => Set<TypeLijst>();
        public DbSet<AfwerkingsGroep> AfwerkingsGroepen => Set<AfwerkingsGroep>();
        public DbSet<AfwerkingsOptie> AfwerkingsOpties => Set<AfwerkingsOptie>();
        public DbSet<Offerte> Offertes => Set<Offerte>();
        public DbSet<WerkBon> WerkBonnen => Set<WerkBon>();
        public DbSet<WerkTaak> WerkTaken => Set<WerkTaak>();
        public DbSet<OfferteRegel> OfferteRegels { get; set; } = default!;
        public DbSet<Instelling> Instellingen => Set<Instelling>();
        public DbSet<Leverancier> Leveranciers => Set<Leverancier>();

        public DbSet<Klant> Klanten => Set<Klant>();
        public DbSet<ImportSession> ImportSessions => Set<ImportSession>();
        public DbSet<ImportRowLog> ImportRowLogs => Set<ImportRowLog>();
        public DbSet<Factuur> Facturen => Set<Factuur>();
        public DbSet<FactuurLijn> FactuurLijnen => Set<FactuurLijn>();


        public AppDbContext(DbContextOptions<AppDbContext> opties) : base(opties) { }

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // TypeLijst entity
            b.Entity<TypeLijst>(entity =>
            {
                entity.Property(x => x.Artikelnummer).HasMaxLength(20);
                entity.Property(x => x.PrijsPerMeter).HasPrecision(10, 2);
                entity.Property(x => x.WinstMargeFactor).HasPrecision(6, 3);
                entity.Property(x => x.AfvalPercentage).HasPrecision(5, 2);
                entity.Property(x => x.VasteKost).HasPrecision(10, 2);
                entity.Property(x => x.VoorraadMeter).HasPrecision(10, 2);
                entity.Property(x => x.InventarisKost).HasPrecision(10, 2);
                entity.Property(x => x.MinimumVoorraad).HasPrecision(10, 2);

                entity.HasOne(x => x.Leverancier)
                      .WithMany(l => l.TypeLijsten)
                      .HasForeignKey(x => x.LeverancierId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            // AfwerkingsGroep entity
            b.Entity<AfwerkingsGroep>(entity =>
            {
                entity.Property(x => x.Code).HasMaxLength(1);
                entity.Property(x => x.Naam).HasMaxLength(50);
            });

            // AfwerkingsOptie entity
            b.Entity<AfwerkingsOptie>(entity =>
            {
                entity.Property(x => x.KostprijsPerM2).HasPrecision(10, 2);
                entity.Property(x => x.WinstMarge).HasPrecision(6, 3);
                entity.Property(x => x.AfvalPercentage).HasPrecision(5, 2);
                entity.Property(x => x.VasteKost).HasPrecision(10, 2);
                entity.HasIndex(x => new { x.AfwerkingsGroepId, x.Volgnummer }).IsUnique();
                entity.HasOne(x => x.AfwerkingsGroep)
                      .WithMany(g => g.Opties)
                      .HasForeignKey(x => x.AfwerkingsGroepId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Leverancier)
                       .WithMany()
                      .HasForeignKey(x => x.LeverancierId)
                      .OnDelete(DeleteBehavior.SetNull);
            });


            b.Entity<Leverancier>(entity =>
            {
                entity.Property(x => x.Naam)
                    .HasMaxLength(3)
                    .IsRequired();
                entity.HasIndex(x => x.Naam).IsUnique();
            });

            b.Entity<Offerte>(entity =>
            {
                entity.Property(o => o.SubtotaalExBtw).HasColumnType("decimal(18,2)");
                entity.Property(o => o.BtwBedrag).HasColumnType("decimal(18,2)");
                entity.Property(o => o.TotaalInclBtw).HasColumnType("decimal(18,2)");

                entity.Property(o => o.Status)
                      .HasConversion<string>()      // enum als string
                      .HasMaxLength(30);

                // Optioneel extra configuratie voor planning-velden
                entity.Property(o => o.GeplandeDatum);
                entity.Property(o => o.DeadlineDatum);
                entity.Property(o => o.GeschatteMinuten);

                // 1 Offerte ↔ 0..1 WerkBon
                entity.HasOne(o => o.WerkBon)
                      .WithOne(w => w.Offerte)
                      .HasForeignKey<WerkBon>(w => w.OfferteId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ImportSession>(entity =>
            {
                entity.Property(x => x.EntityName).HasMaxLength(100);
                entity.Property(x => x.FileName).HasMaxLength(260);
                entity.Property(x => x.Status).HasMaxLength(50);
                entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            });

            b.Entity<ImportRowLog>(entity =>
            {
                entity.Property(x => x.Key).HasMaxLength(250);
                entity.Property(x => x.Message).HasMaxLength(2000);

                entity.HasOne(x => x.ImportSession)
                    .WithMany(x => x.RowLogs)
                    .HasForeignKey(x => x.ImportSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<Factuur>(entity =>
            {
                entity.Property(x => x.FactuurNummer).HasMaxLength(20).IsRequired();
                entity.Property(x => x.KlantNaam).HasMaxLength(200).IsRequired();
                entity.Property(x => x.KlantAdres).HasMaxLength(250);
                entity.Property(x => x.KlantBtwNummer).HasMaxLength(120);
                entity.Property(x => x.Opmerking).HasMaxLength(2000);
                entity.Property(x => x.ExportPad).HasMaxLength(500);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.TotaalExclBtw).HasPrecision(18, 2);
                entity.Property(x => x.TotaalBtw).HasPrecision(18, 2);
                entity.Property(x => x.TotaalInclBtw).HasPrecision(18, 2);

                entity.HasOne(x => x.WerkBon)
                    .WithOne()
                    .HasForeignKey<Factuur>(x => x.WerkBonId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(x => x.Lijnen)
                    .WithOne(x => x.Factuur)
                    .HasForeignKey(x => x.FactuurId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<FactuurLijn>(entity =>
            {
                entity.Property(x => x.Omschrijving).HasMaxLength(500).IsRequired();
                entity.Property(x => x.Eenheid).HasMaxLength(20);
                entity.Property(x => x.Aantal).HasPrecision(18, 2);
                entity.Property(x => x.PrijsExcl).HasPrecision(18, 2);
                entity.Property(x => x.BtwPct).HasPrecision(5, 2);
                entity.Property(x => x.TotaalExcl).HasPrecision(18, 2);
                entity.Property(x => x.TotaalBtw).HasPrecision(18, 2);
                entity.Property(x => x.TotaalIncl).HasPrecision(18, 2);
            });

            // ───────── WerkBon ─────────
            b.Entity<WerkBon>(entity =>
            {
                entity.Property(w => w.Status)
                      .HasConversion<string>()
                      .HasMaxLength(30);

                entity.Property(w => w.TotaalPrijsIncl).HasPrecision(10, 2);

                // Index op OfferteId heb je al via [Index] attribuut. :contentReference[oaicite:5]{index=5}  
            });

            // ───────── WerkTaak ─────────
            b.Entity<WerkTaak>(entity =>
            {
                entity.Property(t => t.Omschrijving).HasMaxLength(200);
                entity.Property(t => t.Resource).HasMaxLength(80);
                entity.Property(t => t.BenodigdeMeter).HasPrecision(10, 2);
            });
            // OfferteRegel entity
            b.Entity<OfferteRegel>(entity =>
            {
                entity.Property(x => x.BreedteCm).HasPrecision(18, 2);
                entity.Property(x => x.HoogteCm).HasPrecision(18, 2);
                entity.Property(x => x.ExtraPrijs).HasPrecision(18, 2);
                entity.Property(x => x.Korting).HasPrecision(18, 2);
                entity.Property(x => x.SubtotaalExBtw).HasPrecision(18, 2);
                entity.Property(x => x.BtwBedrag).HasPrecision(18, 2);
                entity.Property(x => x.TotaalInclBtw).HasPrecision(18, 2);

                // Parent
                entity.HasOne(r => r.Offerte)
                      .WithMany(o => o.Regels)
                      .HasForeignKey(r => r.OfferteId)
                      .OnDelete(DeleteBehavior.Cascade);

                // TypeLijst (optioneel, geen cascade)
                entity.HasOne(r => r.TypeLijst)
                      .WithMany()
                      .HasForeignKey(r => r.TypeLijstId)
                      .OnDelete(DeleteBehavior.NoAction);

                // 6× AfwerkingsOptie (allemaal NO ACTION om multiple cascade paths te vermijden)
                entity.HasOne(r => r.Glas)
                      .WithMany()
                      .HasForeignKey(r => r.GlasId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.PassePartout1)
                      .WithMany()
                      .HasForeignKey(r => r.PassePartout1Id)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.PassePartout2)
                      .WithMany()
                      .HasForeignKey(r => r.PassePartout2Id)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.DiepteKern)
                      .WithMany()
                      .HasForeignKey(r => r.DiepteKernId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.Opkleven)
                      .WithMany()
                      .HasForeignKey(r => r.OpklevenId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.Rug)
                      .WithMany()
                      .HasForeignKey(r => r.RugId)
                      .OnDelete(DeleteBehavior.NoAction);
            });


        }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            foreach (var e in ChangeTracker.Entries<WerkTaak>())
            {
                if (e.State is EntityState.Added or EntityState.Modified)
                {
                    // Houd model consistent
                    var start = e.Entity.GeplandVan;
                    e.Entity.GeplandTot = start.AddMinutes(Math.Max(1, e.Entity.DuurMinuten));
                }
            }

            foreach (var e in ChangeTracker.Entries<WerkBon>())
            {
                if (e.State == EntityState.Modified)
                    e.Entity.BijgewerktOp = DateTime.UtcNow;
            }

            return base.SaveChangesAsync(ct);
        }


    }
}
