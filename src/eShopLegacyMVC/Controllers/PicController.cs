using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopLegacyMVC.Controllers
{
    public class PicController(ICatalogService service, IWebHostEnvironment env, ILogger<PicController> log) : Controller
    {
        public const string GetPicRouteName = "GetPicRouteTemplate";

        // GET: items/5/pic
        [HttpGet]
        [Route("items/{catalogItemId:int}/pic", Name = GetPicRouteName)]
        public IActionResult Index(int catalogItemId)
        {
            log.LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);

            if (catalogItemId <= 0)
            {
                return BadRequest();
            }

            var item = service.FindCatalogItem(catalogItemId);
            if (item == null)
            {
                return NotFound();
            }

            // Was Server.MapPath("~/Pics"). GetFileName blocks path traversal through a crafted PictureFileName.
            var path = Path.Combine(env.WebRootPath, "Pics", Path.GetFileName(item.PictureFileName));
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, GetImageMimeTypeFromImageFileExtension(Path.GetExtension(path)));
        }

        private static string GetImageMimeTypeFromImageFileExtension(string extension) => extension switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            ".wmf" => "image/wmf",
            ".jp2" => "image/jp2",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}
