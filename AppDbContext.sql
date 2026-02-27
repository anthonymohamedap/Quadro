IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [Instellingen] (
        [Sleutel] nvarchar(450) NOT NULL,
        [Waarde] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Instellingen] PRIMARY KEY ([Sleutel])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [Klanten] (
        [Id] int NOT NULL IDENTITY,
        [Voornaam] nvarchar(max) NOT NULL,
        [Achternaam] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [Telefoon] nvarchar(max) NULL,
        [Straat] nvarchar(max) NULL,
        [Nummer] nvarchar(max) NULL,
        [Postcode] nvarchar(max) NULL,
        [Gemeente] nvarchar(max) NULL,
        CONSTRAINT [PK_Klanten] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [Leveranciers] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(max) NOT NULL,
        [Naam] nvarchar(max) NULL,
        CONSTRAINT [PK_Leveranciers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [AfwerkingsOpties] (
        [Id] int NOT NULL IDENTITY,
        [Groep] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [LeverancierId] int NULL,
        [Naam] nvarchar(max) NOT NULL,
        [PrijsPerM2] decimal(10,2) NOT NULL,
        [WinstMargeFactor] decimal(5,2) NOT NULL,
        [AfvalPercentage] decimal(5,2) NOT NULL,
        [VasteKost] decimal(10,2) NOT NULL,
        [WerkMinuten] int NOT NULL,
        [MachineMinuten] int NOT NULL,
        [EnkelDealer] bit NOT NULL,
        [Opmerking1] nvarchar(max) NULL,
        [Opmerking2] nvarchar(max) NULL,
        CONSTRAINT [PK_AfwerkingsOpties] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AfwerkingsOpties_Leveranciers_LeverancierId] FOREIGN KEY ([LeverancierId]) REFERENCES [Leveranciers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [Lijsten] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(max) NOT NULL,
        [LeverancierId] int NOT NULL,
        [BreedteCm] int NOT NULL,
        [Opmerking] nvarchar(max) NULL,
        [PrijsPerMeter] decimal(10,2) NOT NULL,
        [WinstMargeFactor] decimal(5,2) NOT NULL,
        [AfvalPercentage] decimal(5,2) NOT NULL,
        [VasteKost] decimal(10,2) NOT NULL,
        [WerkMinuten] int NOT NULL,
        [MachineMinuten] int NOT NULL,
        [Serie] nvarchar(max) NULL,
        [VoorraadMeter] decimal(10,2) NOT NULL,
        [InventarisKost] decimal(10,2) NOT NULL,
        [LaatstePrijsUpdate] datetime2 NULL,
        [MinimumVoorraad] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_Lijsten] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Lijsten_Leveranciers_LeverancierId] FOREIGN KEY ([LeverancierId]) REFERENCES [Leveranciers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [Offertes] (
        [Id] int NOT NULL IDENTITY,
        [AangemaaktOp] datetime2 NOT NULL,
        [AantalStuks] int NOT NULL,
        [BreedteCm] decimal(10,2) NOT NULL,
        [HoogteCm] decimal(10,2) NOT NULL,
        [LijstId] int NULL,
        [Inleg1LijstId] int NULL,
        [Inleg2LijstId] int NULL,
        [KlantId] int NULL,
        [ExtraWerkMinuten] int NOT NULL,
        [ExtraKost] decimal(10,2) NOT NULL,
        [KortingPercentage] decimal(5,2) NOT NULL,
        [VastePrijs] decimal(10,2) NULL,
        [CorrectieBedrag] decimal(10,2) NOT NULL,
        [SerieLabel] nvarchar(max) NULL,
        [Opmerkingen] nvarchar(max) NULL,
        CONSTRAINT [PK_Offertes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Offertes_Klanten_KlantId] FOREIGN KEY ([KlantId]) REFERENCES [Klanten] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Offertes_Lijsten_Inleg1LijstId] FOREIGN KEY ([Inleg1LijstId]) REFERENCES [Lijsten] ([Id]),
        CONSTRAINT [FK_Offertes_Lijsten_Inleg2LijstId] FOREIGN KEY ([Inleg2LijstId]) REFERENCES [Lijsten] ([Id]),
        CONSTRAINT [FK_Offertes_Lijsten_LijstId] FOREIGN KEY ([LijstId]) REFERENCES [Lijsten] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [OfferteAfwerkingen] (
        [OfferteId] int NOT NULL,
        [Groep] nvarchar(450) NOT NULL,
        [OptieId] int NOT NULL,
        CONSTRAINT [PK_OfferteAfwerkingen] PRIMARY KEY ([OfferteId], [Groep]),
        CONSTRAINT [FK_OfferteAfwerkingen_AfwerkingsOpties_OptieId] FOREIGN KEY ([OptieId]) REFERENCES [AfwerkingsOpties] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OfferteAfwerkingen_Offertes_OfferteId] FOREIGN KEY ([OfferteId]) REFERENCES [Offertes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [WerkBonnen] (
        [Id] int NOT NULL IDENTITY,
        [OfferteId] int NOT NULL,
        [AfhaalDatum] datetime2 NULL,
        [TotaalPrijsIncl] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_WerkBonnen] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WerkBonnen_Offertes_OfferteId] FOREIGN KEY ([OfferteId]) REFERENCES [Offertes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE TABLE [WerkTaken] (
        [Id] int NOT NULL IDENTITY,
        [WerkBonId] int NOT NULL,
        [GeplandVan] datetime2 NOT NULL,
        [GeplandTot] datetime2 NOT NULL,
        [DuurMinuten] int NOT NULL,
        CONSTRAINT [PK_WerkTaken] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WerkTaken_WerkBonnen_WerkBonId] FOREIGN KEY ([WerkBonId]) REFERENCES [WerkBonnen] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AfwerkingsOpties_LeverancierId] ON [AfwerkingsOpties] ([LeverancierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Lijsten_LeverancierId] ON [Lijsten] ([LeverancierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OfferteAfwerkingen_OptieId] ON [OfferteAfwerkingen] ([OptieId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Offertes_Inleg1LijstId] ON [Offertes] ([Inleg1LijstId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Offertes_Inleg2LijstId] ON [Offertes] ([Inleg2LijstId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Offertes_KlantId] ON [Offertes] ([KlantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Offertes_LijstId] ON [Offertes] ([LijstId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WerkBonnen_OfferteId] ON [WerkBonnen] ([OfferteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WerkTaken_WerkBonId] ON [WerkTaken] ([WerkBonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251012121309_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251012121309_InitialCreate', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [FK_AfwerkingsOpties_Leveranciers_LeverancierId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Lijsten] DROP CONSTRAINT [FK_Lijsten_Leveranciers_LeverancierId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] DROP CONSTRAINT [FK_Offertes_Lijsten_Inleg1LijstId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] DROP CONSTRAINT [FK_Offertes_Lijsten_Inleg2LijstId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] DROP CONSTRAINT [FK_Offertes_Lijsten_LijstId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DROP TABLE [OfferteAfwerkingen];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DROP INDEX [IX_Lijsten_LeverancierId] ON [Lijsten];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Offertes]') AND [c].[name] = N'Opmerkingen');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Offertes] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Offertes] DROP COLUMN [Opmerkingen];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Offertes]') AND [c].[name] = N'VastePrijs');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Offertes] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Offertes] DROP COLUMN [VastePrijs];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'AfvalPercentage');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [AfvalPercentage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'Opmerking');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [Opmerking];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'BreedteCm');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [BreedteCm];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'Code');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [Code];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'InventarisKost');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [InventarisKost];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'LaatstePrijsUpdate');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [LaatstePrijsUpdate];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'LeverancierId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [LeverancierId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'MachineMinuten');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [MachineMinuten];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'MinimumVoorraad');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [MinimumVoorraad];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'PrijsPerMeter');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [PrijsPerMeter];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'Serie');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [Serie];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'VasteKost');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [VasteKost];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'VoorraadMeter');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [VoorraadMeter];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Lijsten]') AND [c].[name] = N'WinstMargeFactor');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Lijsten] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [Lijsten] DROP COLUMN [WinstMargeFactor];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'EnkelDealer');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [AfwerkingsOpties] DROP COLUMN [EnkelDealer];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'Groep');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [AfwerkingsOpties] DROP COLUMN [Groep];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'Opmerking1');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [AfwerkingsOpties] DROP COLUMN [Opmerking1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'Opmerking2');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [AfwerkingsOpties] DROP COLUMN [Opmerking2];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'WinstMargeFactor');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [AfwerkingsOpties] DROP COLUMN [WinstMargeFactor];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[SerieLabel]', N'BonNummer', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[LijstId]', N'RugId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[KortingPercentage]', N'Korting', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[Inleg2LijstId]', N'PassePartout2Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[Inleg1LijstId]', N'PassePartout1Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[ExtraKost]', N'TotaalInclBtw', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[CorrectieBedrag]', N'SubtotaalExBtw', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[AangemaaktOp]', N'Datum', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[IX_Offertes_LijstId]', N'IX_Offertes_RugId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[IX_Offertes_Inleg2LijstId]', N'IX_Offertes_PassePartout2Id', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Offertes].[IX_Offertes_Inleg1LijstId]', N'IX_Offertes_PassePartout1Id', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[Lijsten].[WerkMinuten]', N'TypeLijstId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    EXEC sp_rename N'[AfwerkingsOpties].[PrijsPerM2]', N'KostprijsPerM2', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [WerkTaken] ADD [Omschrijving] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [AfgesprokenPrijs] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [BtwBedrag] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [DiepteKernId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [ExtraPrijs] decimal(10,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [GlasId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [IsBestelbon] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [OpklevenId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD [TypeLijstId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Lijsten] ADD [LengteMeter] float(10) NOT NULL DEFAULT 0.0E0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Leveranciers]') AND [c].[name] = N'Naam');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Leveranciers] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Leveranciers] ALTER COLUMN [Naam] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Leveranciers]') AND [c].[name] = N'Code');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Leveranciers] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [Leveranciers] ALTER COLUMN [Code] nvarchar(3) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Instellingen]') AND [c].[name] = N'Waarde');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Instellingen] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [Instellingen] ALTER COLUMN [Waarde] nvarchar(255) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Instellingen]') AND [c].[name] = N'Sleutel');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [Instellingen] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [Instellingen] ALTER COLUMN [Sleutel] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'Naam');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [AfwerkingsOpties] ALTER COLUMN [Naam] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AfwerkingsOpties]') AND [c].[name] = N'Code');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [AfwerkingsOpties] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [AfwerkingsOpties] ALTER COLUMN [Code] nvarchar(5) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [AfwerkingsOpties] ADD [AfwerkingsGroepId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [AfwerkingsOpties] ADD [WinstMarge] decimal(6,3) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE TABLE [AfwerkingsGroepen] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(1) NOT NULL,
        [Naam] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AfwerkingsGroepen] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE TABLE [TypeLijsten] (
        [Id] int NOT NULL IDENTITY,
        [Artikelnummer] nvarchar(20) NOT NULL,
        [LeverancierCode] nvarchar(3) NOT NULL,
        [LeverancierId] int NOT NULL,
        [BreedteCm] int NOT NULL,
        [Opmerking] nvarchar(max) NOT NULL,
        [PrijsPerMeter] decimal(10,2) NOT NULL,
        [WinstMargeFactor] decimal(6,3) NOT NULL,
        [AfvalPercentage] decimal(5,2) NOT NULL,
        [VasteKost] decimal(10,2) NOT NULL,
        [WerkMinuten] int NOT NULL,
        [MachineMinuten] int NOT NULL,
        [VoorraadMeter] decimal(10,2) NOT NULL,
        [InventarisKost] decimal(10,2) NOT NULL,
        [LaatsteUpdate] datetime2 NOT NULL,
        [MinimumVoorraad] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_TypeLijsten] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TypeLijsten_Leveranciers_LeverancierId] FOREIGN KEY ([LeverancierId]) REFERENCES [Leveranciers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_Offertes_DiepteKernId] ON [Offertes] ([DiepteKernId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_Offertes_GlasId] ON [Offertes] ([GlasId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_Offertes_OpklevenId] ON [Offertes] ([OpklevenId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_Offertes_TypeLijstId] ON [Offertes] ([TypeLijstId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_Lijsten_TypeLijstId] ON [Lijsten] ([TypeLijstId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_AfwerkingsOpties_AfwerkingsGroepId] ON [AfwerkingsOpties] ([AfwerkingsGroepId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    CREATE INDEX [IX_TypeLijsten_LeverancierId] ON [TypeLijsten] ([LeverancierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [AfwerkingsOpties] ADD CONSTRAINT [FK_AfwerkingsOpties_AfwerkingsGroepen_AfwerkingsGroepId] FOREIGN KEY ([AfwerkingsGroepId]) REFERENCES [AfwerkingsGroepen] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [AfwerkingsOpties] ADD CONSTRAINT [FK_AfwerkingsOpties_Leveranciers_LeverancierId] FOREIGN KEY ([LeverancierId]) REFERENCES [Leveranciers] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Lijsten] ADD CONSTRAINT [FK_Lijsten_TypeLijsten_TypeLijstId] FOREIGN KEY ([TypeLijstId]) REFERENCES [TypeLijsten] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_DiepteKernId] FOREIGN KEY ([DiepteKernId]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_GlasId] FOREIGN KEY ([GlasId]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_OpklevenId] FOREIGN KEY ([OpklevenId]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_PassePartout1Id] FOREIGN KEY ([PassePartout1Id]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_PassePartout2Id] FOREIGN KEY ([PassePartout2Id]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_AfwerkingsOpties_RugId] FOREIGN KEY ([RugId]) REFERENCES [AfwerkingsOpties] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    ALTER TABLE [Offertes] ADD CONSTRAINT [FK_Offertes_TypeLijsten_TypeLijstId] FOREIGN KEY ([TypeLijstId]) REFERENCES [TypeLijsten] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018131315_UpdateQuadroPrecisionAndRelations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251018131315_UpdateQuadroPrecisionAndRelations', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    ALTER TABLE [TypeLijsten] ADD [Opmerking1] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    ALTER TABLE [TypeLijsten] ADD [Opmerking2] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    ALTER TABLE [TypeLijsten] ADD [IsDealer] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    ALTER TABLE [TypeLijsten] ADD [Serie] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    ALTER TABLE [TypeLijsten] ADD [Soort] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018183307_typelist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251018183307_typelist', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018184919_type'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251018184919_type', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251019094642_new'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TypeLijsten]') AND [c].[name] = N'Opmerking1');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [TypeLijsten] DROP CONSTRAINT [' + @var27 + '];');
    ALTER TABLE [TypeLijsten] DROP COLUMN [Opmerking1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251019094642_new'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TypeLijsten]') AND [c].[name] = N'Opmerking2');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [TypeLijsten] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [TypeLijsten] DROP COLUMN [Opmerking2];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251019094642_new'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251019094642_new', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251019161428_newleverancier'
)
BEGIN
    EXEC sp_rename N'[Leveranciers].[Code]', N'Code', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251019161428_newleverancier'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251019161428_newleverancier', N'9.0.9');
END;

COMMIT;
GO