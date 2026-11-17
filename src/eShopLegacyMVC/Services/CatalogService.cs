using eShopLegacyMVC.Models;
using eShopLegacyMVC.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopLegacyMVC.Services
{
    public class CatalogService(CatalogDBContext db) : ICatalogService
    {
        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
        {
            var totalItems = db.CatalogItems.LongCount();

            var itemsOnPage = db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
        }

        public CatalogItem FindCatalogItem(int id)
        {
            return db.CatalogItems.Include(c => c.CatalogBrand).Include(c => c.CatalogType).FirstOrDefault(ci => ci.Id == id);
        }

        public IEnumerable<CatalogType> GetCatalogTypes() => db.CatalogTypes.AsNoTracking().ToList();

        public IEnumerable<CatalogBrand> GetCatalogBrands() => db.CatalogBrands.AsNoTracking().ToList();

        public void CreateCatalogItem(CatalogItem catalogItem)
        {
            catalogItem.Id = 0; // HiLo value generator assigns the id (replaces CatalogItemHiLoGenerator)
            db.CatalogItems.Add(catalogItem);
            db.SaveChanges();
        }

        public void UpdateCatalogItem(CatalogItem catalogItem)
        {
            db.Entry(catalogItem).State = EntityState.Modified;
            db.SaveChanges();
        }

        public void RemoveCatalogItem(CatalogItem catalogItem)
        {
            db.CatalogItems.Remove(catalogItem);
            db.SaveChanges();
        }

        public void Dispose()
        {
            // DbContext lifetime is owned by the DI container now.
        }
    }
}
