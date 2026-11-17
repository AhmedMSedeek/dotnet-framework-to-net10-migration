using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace eShopLegacyMVC.Models.Infrastructure
{
    /// <summary>
    /// Port of the EF6 CatalogDBInitializer.Seed. Schema creation moved to EF Core migrations;
    /// this class only inserts the starting data, and only into an empty catalog.
    /// </summary>
    public class CatalogSeeder(IWebHostEnvironment env, IConfiguration configuration, ILogger<CatalogSeeder> logger)
    {
        private readonly bool useCustomizationData = configuration.GetValue<bool>("UseCustomizationData");

        public void SeedIfEmpty(CatalogDBContext context)
        {
            if (context.CatalogTypes.Any() || context.CatalogBrands.Any() || context.CatalogItems.Any())
            {
                return;
            }

            logger.LogInformation("Seeding catalog (UseCustomizationData={UseCustomizationData})", useCustomizationData);
            AddCatalogTypes(context);
            AddCatalogBrands(context);
            AddCatalogItems(context);
            AddCatalogItemPictures();
        }

        private void AddCatalogTypes(CatalogDBContext context)
        {
            var types = useCustomizationData ? GetCatalogTypesFromFile() : PreconfiguredData.GetPreconfiguredCatalogTypes();
            foreach (var type in types)
            {
                type.Id = 0; // IDENTITY column: the database assigns the id (the EF6 version did the same in practice)
                context.CatalogTypes.Add(type);
            }
            context.SaveChanges();
        }

        private void AddCatalogBrands(CatalogDBContext context)
        {
            var brands = useCustomizationData ? GetCatalogBrandsFromFile() : PreconfiguredData.GetPreconfiguredCatalogBrands();
            foreach (var brand in brands)
            {
                brand.Id = 0;
                context.CatalogBrands.Add(brand);
            }
            context.SaveChanges();
        }

        private void AddCatalogItems(CatalogDBContext context)
        {
            var items = useCustomizationData ? GetCatalogItemsFromFile(context) : PreconfiguredData.GetPreconfiguredCatalogItems();
            foreach (var item in items)
            {
                item.Id = 0; // assigned by EF Core HiLo from catalog_hilo
                context.CatalogItems.Add(item);
            }
            context.SaveChanges();
        }

        private string SetupFile(string name) => Path.Combine(env.ContentRootPath, "Setup", name);

        private IEnumerable<CatalogType> GetCatalogTypesFromFile()
        {
            var csv = SetupFile("CatalogTypes.csv");
            if (!File.Exists(csv))
            {
                return PreconfiguredData.GetPreconfiguredCatalogTypes();
            }

            GetHeaders(csv, ["catalogtype"]);
            return File.ReadAllLines(csv).Skip(1).Select(CreateCatalogType).Where(x => x != null).ToList();
        }

        static CatalogType CreateCatalogType(string type)
        {
            type = type.Trim('"').Trim();
            if (string.IsNullOrEmpty(type))
            {
                throw new Exception("catalog Type Name is empty");
            }
            return new CatalogType { Type = type };
        }

        private IEnumerable<CatalogBrand> GetCatalogBrandsFromFile()
        {
            var csv = SetupFile("CatalogBrands.csv");
            if (!File.Exists(csv))
            {
                return PreconfiguredData.GetPreconfiguredCatalogBrands();
            }

            GetHeaders(csv, ["catalogbrand"]);
            return File.ReadAllLines(csv).Skip(1).Select(CreateCatalogBrand).Where(x => x != null).ToList();
        }

        static CatalogBrand CreateCatalogBrand(string brand)
        {
            brand = brand.Trim('"').Trim();
            if (string.IsNullOrEmpty(brand))
            {
                throw new Exception("catalog Brand Name is empty");
            }
            return new CatalogBrand { Brand = brand };
        }

        private IEnumerable<CatalogItem> GetCatalogItemsFromFile(CatalogDBContext context)
        {
            var csv = SetupFile("CatalogItems.csv");
            if (!File.Exists(csv))
            {
                return PreconfiguredData.GetPreconfiguredCatalogItems();
            }

            string[] requiredHeaders = ["catalogtypename", "catalogbrandname", "description", "name", "price", "pictureFileName"];
            string[] optionalHeaders = ["availablestock", "restockthreshold", "maxstockthreshold", "onreorder"];
            var headers = GetHeaders(csv, requiredHeaders, optionalHeaders);

            var typeIds = context.CatalogTypes.ToDictionary(ct => ct.Type, ct => ct.Id);
            var brandIds = context.CatalogBrands.ToDictionary(cb => cb.Brand, cb => cb.Id);

            return File.ReadAllLines(csv)
                .Skip(1)
                .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                .Select(columns => CreateCatalogItem(columns, headers, typeIds, brandIds))
                .Where(x => x != null)
                .ToList();
        }

        static CatalogItem CreateCatalogItem(string[] column, string[] headers, Dictionary<string, int> typeIds, Dictionary<string, int> brandIds)
        {
            if (column.Length != headers.Length)
            {
                throw new Exception($"column count '{column.Length}' not the same as headers count'{headers.Length}'");
            }

            string Value(string header) => column[Array.IndexOf(headers, header)].Trim('"').Trim();

            var typeName = Value("catalogtypename");
            if (!typeIds.TryGetValue(typeName, out var typeId))
            {
                throw new Exception($"type={typeName} does not exist in catalogTypes");
            }

            var brandName = Value("catalogbrandname");
            if (!brandIds.TryGetValue(brandName, out var brandId))
            {
                // the legacy message said "type=" here; corrected
                throw new Exception($"brand={brandName} does not exist in catalogBrands");
            }

            var priceString = Value("price");
            if (!decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
            {
                throw new Exception($"price={priceString}is not a valid decimal number");
            }

            var item = new CatalogItem
            {
                CatalogTypeId = typeId,
                CatalogBrandId = brandId,
                Description = Value("description"),
                Name = Value("name"),
                Price = price,
                PictureFileName = Value("picturefilename"),
            };

            item.AvailableStock = OptionalInt(column, headers, "availablestock") ?? item.AvailableStock;
            item.RestockThreshold = OptionalInt(column, headers, "restockthreshold") ?? item.RestockThreshold;
            item.MaxStockThreshold = OptionalInt(column, headers, "maxstockthreshold") ?? item.MaxStockThreshold;

            var reorderIndex = Array.IndexOf(headers, "onreorder");
            if (reorderIndex != -1)
            {
                var s = column[reorderIndex].Trim('"').Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    item.OnReorder = bool.TryParse(s, out var onReorder)
                        ? onReorder
                        : throw new Exception($"onReorder={s} is not a valid boolean");
                }
            }

            return item;
        }

        static int? OptionalInt(string[] column, string[] headers, string header)
        {
            var index = Array.IndexOf(headers, header);
            if (index == -1)
            {
                return null;
            }

            var s = column[index].Trim('"').Trim();
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }

            return int.TryParse(s, out var value) ? value : throw new Exception($"{header}={s} is not a valid integer");
        }

        static string[] GetHeaders(string csvFile, string[] requiredHeaders, string[] optionalHeaders = null)
        {
            var headers = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',');

            if (headers.Length < requiredHeaders.Length)
            {
                throw new Exception($"requiredHeader count '{requiredHeaders.Length}' is bigger then csv header count '{headers.Length}' ");
            }

            if (optionalHeaders != null && headers.Length > requiredHeaders.Length + optionalHeaders.Length)
            {
                throw new Exception($"csv header count '{headers.Length}'  is larger then required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
            }

            foreach (var required in requiredHeaders)
            {
                if (!headers.Contains(required.ToLowerInvariant()))
                {
                    throw new Exception($"does not contain required header '{required}'");
                }
            }

            return headers;
        }

        private void AddCatalogItemPictures()
        {
            if (!useCustomizationData)
            {
                return;
            }

            var picturePath = new DirectoryInfo(Path.Combine(env.WebRootPath, "Pics"));
            foreach (var file in picturePath.GetFiles())
            {
                file.Delete();
            }

            ZipFile.ExtractToDirectory(SetupFile("CatalogItems.zip"), picturePath.FullName);
        }
    }
}
