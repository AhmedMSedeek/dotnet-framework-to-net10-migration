using eShopLegacy.Utilities;
using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopLegacyMVC.Controllers.WebApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController(ICatalogService service) : ControllerBase
    {
        // GET api/files
        // BREAKING (flagged in MIGRATION-REPORT.md): the legacy endpoint streamed a BinaryFormatter payload (served as text/html).
        // BinaryFormatter is unsafe and removed in .NET 9+, so the same data is now returned as a JSON file.
        [HttpGet]
        public IActionResult Get()
        {
            var brands = service.GetCatalogBrands()
                .Select(b => new BrandDTO { Id = b.Id, Brand = b.Brand })
                .ToList();

            var stream = new Serializing().SerializeJson(brands);
            return File(stream, "application/json", "brands.json");
        }

        public class BrandDTO
        {
            public int Id { get; set; }
            public string Brand { get; set; }
        }
    }
}
