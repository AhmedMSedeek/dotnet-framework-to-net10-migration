using eShopLegacyMVC.Models;
using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopLegacyMVC.Controllers.WebApi
{
    // Was a Web API 2 ApiController on the convention route api/{controller}/{id}.
    [ApiController]
    [Route("api/[controller]")]
    public class BrandsController(ICatalogService service) : ControllerBase
    {
        // GET api/brands
        [HttpGet]
        public IEnumerable<CatalogBrand> Get() => service.GetCatalogBrands();

        // GET api/brands/5
        [HttpGet("{id:int}")]
        public ActionResult<CatalogBrand> Get(int id)
        {
            var brand = service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
            return brand == null ? NotFound() : brand;
        }

        // DELETE api/brands/5
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var brandToDelete = service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
            if (brandToDelete == null)
            {
                return NotFound();
            }

            // demo only - don't actually delete (unchanged from the original)
            return Ok();
        }
    }
}
