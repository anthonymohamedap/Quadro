using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Linq;

public static class AppServices
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Gemakkelijk toegang tot je DbContext
    public static AppDbContext Db => Services.GetRequiredService<AppDbContext>();

    public static void Init()
    {
        var services = new ServiceCollection();

        // >>> SQLite connectiestring
        var connectionString = "Data Source=quadro.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // hier eventueel later nog andere services registreren

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ✅ volledig leegmaken
        db.Database.EnsureDeleted();

        // ✅ opnieuw aanmaken + migraties toepassen
        db.Database.Migrate();

        SeedBasisData(db);

    }
    private static void SeedBasisData(AppDbContext db)
    {

        if (!db.Leveranciers.Any())
        {
            db.Leveranciers.AddRange(
                new Leverancier { Code = "ICO", Naam = "Iconic Frames" },
                new Leverancier { Code = "HOF", Naam = "Hofman Design" },
                new Leverancier { Code = "FRA", Naam = "FramingArt" },
                new Leverancier { Code = "BOL", Naam = "Bol FrameWorks" }
            );
            db.SaveChanges();
        }

        // IDs ophalen op basis van Code
        int icoId = db.Leveranciers.Single(l => l.Code == "ICO").Id;
        int hofId = db.Leveranciers.Single(l => l.Code == "HOF").Id;
        int fraId = db.Leveranciers.Single(l => l.Code == "FRA").Id;
        int bolId = db.Leveranciers.Single(l => l.Code == "BOL").Id;

        // 2) AfwerkingsGroepen (G / P / D / O / R)
        if (!db.AfwerkingsGroepen.Any())
        {
            db.AfwerkingsGroepen.AddRange(
                new AfwerkingsGroep { Code = 'G', Naam = "Glas" },
                new AfwerkingsGroep { Code = 'P', Naam = "Passe-partout" },
                new AfwerkingsGroep { Code = 'D', Naam = "Diepte kern" },
                new AfwerkingsGroep { Code = 'O', Naam = "Opkleven" },
                new AfwerkingsGroep { Code = 'R', Naam = "Rug" }
            );
            db.SaveChanges();
        }

        int gId = db.AfwerkingsGroepen.Single(g => g.Code == 'G').Id;
        int pId = db.AfwerkingsGroepen.Single(g => g.Code == 'P').Id;
        int dId = db.AfwerkingsGroepen.Single(g => g.Code == 'D').Id;
        int oId = db.AfwerkingsGroepen.Single(g => g.Code == 'O').Id;
        int rId = db.AfwerkingsGroepen.Single(g => g.Code == 'R').Id;

        // 3) TypeLijsten (jouw 10 records)
        if (!db.TypeLijsten.Any())
        {
            db.TypeLijsten.AddRange(
                new TypeLijst
                {
                    Artikelnummer = "001001/A1",
                    LeverancierId = icoId,
                    BreedteCm = 20,
                    Soort = "HOUT",
                    Serie = "Classic",
                    IsDealer = true,
                    Opmerking = "Zwarte houten lijst 20mm plat profiel",
                    PrijsPerMeter = 6.50m,
                    WinstMargeFactor = 2.200m,
                    AfvalPercentage = 12.5m,
                    VasteKost = 1.50m,
                    WerkMinuten = 8,
                    VoorraadMeter = 120.00m,
                    InventarisKost = 780.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 15.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001002/A2",
                    LeverancierId = icoId,
                    BreedteCm = 25,
                    Soort = "HOUT",
                    Serie = "Modern",
                    IsDealer = true,
                    Opmerking = "Witte lijst met glans afwerking",
                    PrijsPerMeter = 7.10m,
                    WinstMargeFactor = 2.300m,
                    AfvalPercentage = 10.0m,
                    VasteKost = 1.80m,
                    WerkMinuten = 7,
                    VoorraadMeter = 100.00m,
                    InventarisKost = 710.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 10.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001003/B1",
                    LeverancierId = hofId,
                    BreedteCm = 22,
                    Soort = "HOUT",
                    Serie = "Classic",
                    IsDealer = false,
                    Opmerking = "Donkere eiken lijst met structuur",
                    PrijsPerMeter = 8.90m,
                    WinstMargeFactor = 2.500m,
                    AfvalPercentage = 15.0m,
                    VasteKost = 2.20m,
                    WerkMinuten = 10,
                    VoorraadMeter = 75.00m,
                    InventarisKost = 667.50m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 8.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001004/B2",
                    LeverancierId = hofId,
                    BreedteCm = 18,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = false,
                    Opmerking = "Aluminium profiel zwart mat",
                    PrijsPerMeter = 4.95m,
                    WinstMargeFactor = 2.000m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 1.20m,
                    WerkMinuten = 6,
                    VoorraadMeter = 200.00m,
                    InventarisKost = 990.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 20.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001005/C1",
                    LeverancierId = fraId,
                    BreedteCm = 28,
                    Soort = "PVC",
                    Serie = "Budget",
                    IsDealer = true,
                    Opmerking = "Wit profiel met fijne ribbels",
                    PrijsPerMeter = 3.80m,
                    WinstMargeFactor = 2.100m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.90m,
                    WerkMinuten = 7,
                    VoorraadMeter = 300.00m,
                    InventarisKost = 1140.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 25.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001006/C2",
                    LeverancierId = fraId,
                    BreedteCm = 30,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = true,
                    Opmerking = "Zilver profiel 30mm breed",
                    PrijsPerMeter = 5.40m,
                    WinstMargeFactor = 2.150m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 1.30m,
                    WerkMinuten = 5,
                    VoorraadMeter = 180.00m,
                    InventarisKost = 972.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 15.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001007/D1",
                    LeverancierId = bolId,
                    BreedteCm = 35,
                    Soort = "HOUT",
                    Serie = "Lux",
                    IsDealer = false,
                    Opmerking = "Hoge lijst donker eik afgerond",
                    PrijsPerMeter = 8.90m,
                    WinstMargeFactor = 2.500m,
                    AfvalPercentage = 12.0m,
                    VasteKost = 2.50m,
                    WerkMinuten = 10,
                    VoorraadMeter = 75.00m,
                    InventarisKost = 667.50m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 10.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001008/D2",
                    LeverancierId = bolId,
                    BreedteCm = 15,
                    Soort = "PVC",
                    Serie = "Budget",
                    IsDealer = false,
                    Opmerking = "Smalle witte kunststof lijst",
                    PrijsPerMeter = 3.20m,
                    WinstMargeFactor = 2.050m,
                    AfvalPercentage = 7.0m,
                    VasteKost = 0.80m,
                    WerkMinuten = 6,
                    VoorraadMeter = 250.00m,
                    InventarisKost = 800.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 30.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001009/E1",
                    LeverancierId = icoId,
                    BreedteCm = 40,
                    Soort = "HOUT",
                    Serie = "Lux",
                    IsDealer = true,
                    Opmerking = "Brede lijst mahonie met nerf",
                    PrijsPerMeter = 9.80m,
                    WinstMargeFactor = 2.600m,
                    AfvalPercentage = 14.0m,
                    VasteKost = 2.80m,
                    WerkMinuten = 12,
                    VoorraadMeter = 60.00m,
                    InventarisKost = 588.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 8.00m
                },
                new TypeLijst
                {
                    Artikelnummer = "001010/E2",
                    LeverancierId = hofId,
                    BreedteCm = 12,
                    Soort = "ALU",
                    Serie = "Modern",
                    IsDealer = false,
                    Opmerking = "Aluminium profiel zilver glans",
                    PrijsPerMeter = 4.50m,
                    WinstMargeFactor = 2.000m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 1.00m,
                    WerkMinuten = 5,
                    VoorraadMeter = 210.00m,
                    InventarisKost = 945.00m,
                    LaatsteUpdate = DateTime.Now,
                    MinimumVoorraad = 25.00m
                }
            );

            db.SaveChanges();
        }

        // 4) AfwerkingsOpties (G, P, D, O, R – alle rijen die je stuurde)
        if (!db.AfwerkingsOpties.Any())
        {
            db.AfwerkingsOpties.AddRange(
                // 🪟 GLAS (G) – HOF
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Helder glas 2 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 18.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = hofId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Ontspiegeld glas 2 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 26.50m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = hofId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = gId,
                    Naam = "Museumglas UV-filter",
                    Volgnummer = '3',
                    KostprijsPerM2 = 60.00m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 4.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 4,
                    LeverancierId = hofId
                },

                // 🎨 PASSE-PARTOUT (P) – FRA
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Passe-partout wit",
                    Volgnummer = '1',
                    KostprijsPerM2 = 12.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Passe-partout zwart",
                    Volgnummer = '2',
                    KostprijsPerM2 = 13.50m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 8.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = pId,
                    Naam = "Dubbele passe-partout",
                    Volgnummer = '3',
                    KostprijsPerM2 = 18.00m,
                    WinstMarge = 2.20m,
                    AfvalPercentage = 10.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 7,
                    LeverancierId = fraId
                },

                // 🧱 DIEPTE / KERN (D) – BOL
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "Foamboard 5 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 15.50m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = bolId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "Foamboard 10 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 19.00m,
                    WinstMarge = 2.30m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = bolId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = dId,
                    Naam = "PVC-kern 5 mm",
                    Volgnummer = '3',
                    KostprijsPerM2 = 22.00m,
                    WinstMarge = 2.20m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 4,
                    LeverancierId = bolId
                },

                // 🧷 OPKLEVEN (O) – ICO
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven zuurvrij",
                    Volgnummer = '1',
                    KostprijsPerM2 = 10.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 6,
                    LeverancierId = icoId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven spray-mount",
                    Volgnummer = '2',
                    KostprijsPerM2 = 8.50m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 5,
                    LeverancierId = icoId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = oId,
                    Naam = "Opkleven film",
                    Volgnummer = '3',
                    KostprijsPerM2 = 11.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 6,
                    LeverancierId = icoId
                },

                // 🪵 RUG (R) – FRA
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "MDF 3 mm",
                    Volgnummer = '1',
                    KostprijsPerM2 = 9.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 2,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "Hardboard 3 mm",
                    Volgnummer = '2',
                    KostprijsPerM2 = 8.00m,
                    WinstMarge = 2.50m,
                    AfvalPercentage = 5.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 2,
                    LeverancierId = fraId
                },
                new AfwerkingsOptie
                {
                    AfwerkingsGroepId = rId,
                    Naam = "Zuurvrij karton",
                    Volgnummer = '3',
                    KostprijsPerM2 = 11.00m,
                    WinstMarge = 2.40m,
                    AfvalPercentage = 6.0m,
                    VasteKost = 0.00m,
                    WerkMinuten = 3,
                    LeverancierId = fraId
                }
            );

            db.SaveChanges();
        }

        // 5) Basis instellingen (optioneel, maar handig voor PricingService)
        if (!db.Instellingen.Any(i => i.Sleutel == "Uurloon"))
        {
            db.Instellingen.Add(new Instelling
            {
                Sleutel = "Uurloon",
                Waarde = "45"
            });
        }

        if (!db.Instellingen.Any(i => i.Sleutel == "BtwPercent"))
        {
            db.Instellingen.Add(new Instelling
            {
                Sleutel = "BtwPercent",
                Waarde = "21"
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            db.SaveChanges();
        }
    }


}
