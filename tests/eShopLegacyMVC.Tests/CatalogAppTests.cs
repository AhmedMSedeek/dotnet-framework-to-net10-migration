using System.Net;
using System.Text.RegularExpressions;
using eShopLegacy.Utilities;
using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShopLegacyMVC.Tests;

/// <summary>Runs the real app in memory with UseMockData=true, so no SQL Server is needed (CI-friendly).</summary>
public class MockCatalogFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("UseMockData", "true");
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}

public class CatalogAppTests(MockCatalogFactory factory) : IClassFixture<MockCatalogFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public void Tests_run_against_the_mock_catalog()
    {
        using var scope = factory.Services.CreateScope();
        Assert.IsType<CatalogServiceMock>(scope.ServiceProvider.GetRequiredService<ICatalogService>());
    }

    [Fact]
    public async Task Index_lists_items_with_prices_formatted_as_before()
    {
        var html = await Client().GetStringAsync("/");

        Assert.Contains(".NET Bot Black Hoodie", html);
        Assert.Contains("$19.50", html); // same as the legacy page (docs/baseline/index.html)
        Assert.Contains("Showing 10 of 12 products - Page 1 - 2", html);
        Assert.Contains("/items/1/pic", html);
    }

    [Fact]
    public async Task ActionLinks_keep_their_css_classes()
    {
        // Regression: in ASP.NET Core, ActionLink(text, action, null, new { @class }) binds to the
        // (text, action, controllerName, routeValues) overload and turns the class into a query string.
        var html = await Client().GetStringAsync("/");
        Assert.Contains("<a class=\"btn esh-button esh-button-primary\" href=\"/Catalog/Create\">Create New</a>", html);
        Assert.DoesNotContain("?class=", html);

        var details = await Client().GetStringAsync("/Catalog/Details/1");
        Assert.DoesNotContain("?class=", details);
    }

    [Fact]
    public async Task Layout_shows_session_info_like_Session_Start_did()
    {
        var html = await Client().GetStringAsync("/");
        Assert.Contains(Environment.MachineName, html);
    }

    [Theory]
    [InlineData("/Catalog/Details", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Details/999", HttpStatusCode.NotFound)]
    [InlineData("/Catalog/Edit/999", HttpStatusCode.NotFound)]
    [InlineData("/items/0/pic", HttpStatusCode.BadRequest)]
    [InlineData("/items/999/pic", HttpStatusCode.NotFound)]
    [InlineData("/api/brands/99", HttpStatusCode.NotFound)]
    public async Task Error_status_codes_match_the_legacy_app(string url, HttpStatusCode expected)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Every_url_in_the_stylesheets_is_served()
    {
        // Regression: IIS served /fonts from the project root; Kestrel only serves wwwroot.
        var client = Client();
        var cssFiles = new[] { "/Content/base.css", "/Content/custom.css", "/Content/Site.css" };
        var checkedUrls = 0;
        foreach (var css in cssFiles)
        {
            var text = await client.GetStringAsync(css);
            foreach (Match m in Regex.Matches(text, @"url\(\s*[""']?([^""')?#]+)"))
            {
                var url = new Uri(new Uri("http://x" + css), m.Groups[1].Value).AbsolutePath;
                var response = await client.GetAsync(url);
                Assert.True(response.IsSuccessStatusCode, $"{css} references {url}, which returns {(int)response.StatusCode}");
                checkedUrls++;
            }
        }
        Assert.True(checkedUrls > 0);
    }

    [Theory]
    [InlineData("/Images/brand.png")]
    [InlineData("/Scripts/jquery-3.3.1.min.js")]
    [InlineData("/Scripts/jquery.validate.unobtrusive.js")]
    [InlineData("/Scripts/modernizr-2.8.3.js")]
    [InlineData("/favicon.ico")]
    public async Task Static_files_referenced_by_the_layout_are_served(string url)
    {
        (await Client().GetAsync(url)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Picture_endpoint_serves_png()
    {
        var response = await Client().GetAsync("/items/1/pic");
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000);
    }

    [Fact]
    public async Task Brands_api_keeps_the_PascalCase_contract()
    {
        var json = await Client().GetStringAsync("/api/brands");
        Assert.Equal(
            """[{"Id":1,"Brand":"Azure"},{"Id":2,"Brand":".NET"},{"Id":3,"Brand":"Visual Studio"},{"Id":4,"Brand":"SQL Server"},{"Id":5,"Brand":"Other"}]""",
            json);

        Assert.Equal("""{"Id":2,"Brand":".NET"}""", await Client().GetStringAsync("/api/brands/2"));
    }

    [Fact]
    public async Task Files_api_returns_json_instead_of_BinaryFormatter()
    {
        var response = await Client().GetAsync("/api/files");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("""[{"Id":1,"Brand":"Azure"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Brands_delete_is_still_a_no_op()
    {
        var client = Client();
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/brands/2")).StatusCode);
        Assert.Contains(".NET", await client.GetStringAsync("/api/brands/2"));
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var response = await Client().PostAsync("/Catalog/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>Create/edit/delete change the in-memory catalog, so they get their own app instance.</summary>
public class CatalogCrudTests : IDisposable
{
    private readonly MockCatalogFactory factory = new();

    public void Dispose() => factory.Dispose();

    private static async Task<string> TokenFrom(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static Dictionary<string, string> Item(string token, string name, string price, int id = 0) => new()
    {
        ["__RequestVerificationToken"] = token,
        ["Id"] = id.ToString(),
        ["Name"] = name,
        ["Description"] = "Created by a test",
        ["CatalogBrandId"] = "2",
        ["CatalogTypeId"] = "1",
        ["Price"] = price,
        ["PictureFileName"] = "dummy.png",
        ["AvailableStock"] = "5",
        ["RestockThreshold"] = "1",
        ["MaxStockThreshold"] = "10",
    };

    [Fact]
    public async Task Create_edit_and_delete_round_trip()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Create
        var created = await client.PostAsync("/Catalog/Create", new FormUrlEncodedContent(Item(await TokenFrom(client, "/Catalog/Create"), "Test Mug", "9.99")));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var details = await client.GetStringAsync("/Catalog/Details/13");
        Assert.Contains("Test Mug", details);
        Assert.Contains("$9.99", details);

        // Edit
        var edited = await client.PostAsync("/Catalog/Edit/13", new FormUrlEncodedContent(Item(await TokenFrom(client, "/Catalog/Edit/13"), "Test Mug v2", "10.50", id: 13)));
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("Test Mug v2", await client.GetStringAsync("/Catalog/Details/13"));

        // Delete
        var token = await TokenFrom(client, "/Catalog/Delete/13");
        var deleted = await client.PostAsync("/Catalog/Delete/13", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Catalog/Details/13")).StatusCode);
    }

    [Fact]
    public async Task Invalid_price_is_rejected_with_the_legacy_message()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync("/Catalog/Create", new FormUrlEncodedContent(Item(await TokenFrom(client, "/Catalog/Create"), "Bad price", "1.234")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-rendered, nothing saved
        Assert.Contains("The field Price must be a positive number with maximum two decimals.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deleting_a_missing_item_returns_404_instead_of_crashing()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await TokenFrom(client, "/Catalog/Delete/1");
        var response = await client.PostAsync("/Catalog/Delete/999", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class SerializingTests
{
    [Fact]
    public void Json_round_trip_keeps_property_names()
    {
        var s = new Serializing();
        var stream = s.SerializeJson(new[] { new { Id = 7, Brand = "Halal Mugs" } });
        Assert.Equal("""[{"Id":7,"Brand":"Halal Mugs"}]""", new StreamReader(stream).ReadToEnd());

        var back = s.DeserializeJson<List<Dictionary<string, object>>>(stream);
        Assert.Equal("Halal Mugs", back![0]["Brand"].ToString());
    }
}
