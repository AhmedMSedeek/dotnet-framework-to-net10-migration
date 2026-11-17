-- Adopt a database created by the legacy EF6 app, so EF Core treats it as already migrated.
-- Run ONCE against the existing production database before the first start of the .NET 10 app.
-- Safe to re-run. Does not touch data. (EF6's own __MigrationHistory table is left in place; drop it later if wanted.)
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID(N'dbo.Catalog') IS NULL OR OBJECT_ID(N'dbo.CatalogBrand') IS NULL OR OBJECT_ID(N'dbo.CatalogType') IS NULL
    THROW 50000, 'Legacy catalog tables not found: this is not an eShopLegacyMVC database.', 1;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
    CREATE TABLE dbo.__EFMigrationsHistory (
        MigrationId    nvarchar(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
        ProductVersion nvarchar(32)  NOT NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261117071450_InitialFromEf6')
    INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261117071450_InitialFromEf6', N'10.0.0');

COMMIT;
