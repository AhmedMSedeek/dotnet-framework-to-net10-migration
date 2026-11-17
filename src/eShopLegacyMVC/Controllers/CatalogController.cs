using eShopLegacyMVC.Models;
using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopLegacyMVC.Controllers
{
    public class CatalogController(ICatalogService service, ILogger<CatalogController> log) : Controller
    {
        private const string BindableFields = "Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder";

        // GET /[?pageSize=3&pageIndex=10]
        public IActionResult Index(int pageSize = 10, int pageIndex = 0)
        {
            log.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
            var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
            ChangeUriPlaceholder(paginatedItems.Data);
            return View(paginatedItems);
        }

        // GET: Catalog/Details/5
        public IActionResult Details(int? id)
        {
            log.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var catalogItem = service.FindCatalogItem(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            return View(catalogItem);
        }

        // GET: Catalog/Create
        public IActionResult Create()
        {
            log.LogInformation("Now loading... /Catalog/Create");
            SetSelectLists();
            return View(new CatalogItem());
        }

        // POST: Catalog/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind(BindableFields)] CatalogItem catalogItem)
        {
            log.LogInformation("Now processing... /Catalog/Create?catalogItemName={Name}", catalogItem.Name);
            if (ModelState.IsValid)
            {
                service.CreateCatalogItem(catalogItem);
                return RedirectToAction(nameof(Index));
            }

            SetSelectLists(catalogItem);
            return View(catalogItem);
        }

        // GET: Catalog/Edit/5
        public IActionResult Edit(int? id)
        {
            log.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var catalogItem = service.FindCatalogItem(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            SetSelectLists(catalogItem);
            return View(catalogItem);
        }

        // POST: Catalog/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit([Bind(BindableFields)] CatalogItem catalogItem)
        {
            log.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
            if (ModelState.IsValid)
            {
                service.UpdateCatalogItem(catalogItem);
                return RedirectToAction(nameof(Index));
            }
            SetSelectLists(catalogItem);
            return View(catalogItem);
        }

        // GET: Catalog/Delete/5
        public IActionResult Delete(int? id)
        {
            log.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var catalogItem = service.FindCatalogItem(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            return View(catalogItem);
        }

        // POST: Catalog/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            log.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
            var catalogItem = service.FindCatalogItem(id);
            if (catalogItem == null)
            {
                return NotFound(); // the legacy action threw a NullReferenceException here
            }
            service.RemoveCatalogItem(catalogItem);
            return RedirectToAction(nameof(Index));
        }

        // Target of UseExceptionHandler; replaces the MVC 5 HandleErrorAttribute.
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Error() => View("Error");

        private void SetSelectLists(CatalogItem item = null)
        {
            ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", item?.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", item?.CatalogTypeId);
        }

        private void ChangeUriPlaceholder(IEnumerable<CatalogItem> items)
        {
            foreach (var catalogItem in items)
            {
                AddUriPlaceHolder(catalogItem);
            }
        }

        private void AddUriPlaceHolder(CatalogItem item)
        {
            item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
        }
    }
}
