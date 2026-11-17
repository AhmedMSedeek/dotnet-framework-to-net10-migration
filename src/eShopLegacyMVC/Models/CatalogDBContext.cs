using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopLegacyMVC.Models
{
    /// <summary>
    /// EF6 → EF Core 10. The mapping reproduces the schema EF6 created (table, key, constraint and
    /// index names included), so an existing production database is adopted as-is: no data migration.
    /// </summary>
    public class CatalogDBContext(DbContextOptions<CatalogDBContext> options) : DbContext(options)
    {
        public const string CatalogItemHiLoSequence = "catalog_hilo";

        public DbSet<CatalogItem> CatalogItems { get; set; }

        public DbSet<CatalogBrand> CatalogBrands { get; set; }

        public DbSet<CatalogType> CatalogTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema("dbo");

            // Same sequences the legacy SQL scripts created (start 1, increment 10).
            builder.HasSequence<long>(CatalogItemHiLoSequence).StartsAt(1).IncrementsBy(10);
            // Created by the old initializer but never used for ids (brand/type keys are IDENTITY). Kept for schema parity.
            builder.HasSequence<long>("catalog_brand_hilo").StartsAt(1).IncrementsBy(10);
            builder.HasSequence<long>("catalog_type_hilo").StartsAt(1).IncrementsBy(10);

            ConfigureCatalogType(builder.Entity<CatalogType>());
            ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
            ConfigureCatalogItem(builder.Entity<CatalogItem>());
        }

        static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
        {
            builder.ToTable("CatalogType");
            builder.HasKey(ct => ct.Id).HasName("PK_dbo.CatalogType");
            builder.Property(ct => ct.Id).UseIdentityColumn();
            builder.Property(ct => ct.Type).IsRequired().HasMaxLength(100);
        }

        static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
        {
            builder.ToTable("CatalogBrand");
            builder.HasKey(cb => cb.Id).HasName("PK_dbo.CatalogBrand");
            builder.Property(cb => cb.Id).UseIdentityColumn();
            builder.Property(cb => cb.Brand).IsRequired().HasMaxLength(100);
        }

        static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
        {
            builder.ToTable("Catalog");
            builder.HasKey(ci => ci.Id).HasName("PK_dbo.Catalog");

            // Replaces the hand-written CatalogItemHiLoGenerator: EF Core's built-in HiLo uses the same
            // sequence and block size (the sequence increment, 10), so ids continue where the old app stopped.
            builder.Property(ci => ci.Id).UseHiLo(CatalogItemHiLoSequence);

            builder.Property(ci => ci.Name).IsRequired().HasMaxLength(50);
            builder.Property(ci => ci.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(ci => ci.PictureFileName).IsRequired();
            builder.Ignore(ci => ci.PictureUri);

            builder.HasOne(ci => ci.CatalogBrand).WithMany()
                .HasForeignKey(ci => ci.CatalogBrandId)
                .HasConstraintName("FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId");
            builder.HasIndex(ci => ci.CatalogBrandId).HasDatabaseName("IX_CatalogBrandId");

            builder.HasOne(ci => ci.CatalogType).WithMany()
                .HasForeignKey(ci => ci.CatalogTypeId)
                .HasConstraintName("FK_dbo.Catalog_dbo.CatalogType_CatalogTypeId");
            builder.HasIndex(ci => ci.CatalogTypeId).HasDatabaseName("IX_CatalogTypeId");
        }
    }
}
