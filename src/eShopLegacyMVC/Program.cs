using System.Globalization;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Was: Web.config <appSettings> + Autofac ApplicationModule
var useMockData = builder.Configuration.GetValue<bool>("UseMockData");

builder.Services
    .AddControllersWithViews() // MVC 5 and Web API 2 become one pipeline
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null); // keep the PascalCase JSON clients already consume

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(); // was <sessionState mode="InProc" />

if (useMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDBContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDBContext")));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
    builder.Services.AddScoped<CatalogSeeder>();
}

var app = builder.Build();

if (!useMockData && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Was: Database.SetInitializer(CreateDatabaseIfNotExists) in Global.asax
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
    db.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedIfEmpty(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Catalog/Error"); // was <HandleErrorAttribute> global filter
}

// Prices are rendered with {0:C}; pin the culture the old site ran with so "$19.50" stays "$19.50"
// on any host (Linux containers often run with an invariant culture).
var culture = new CultureInfo(app.Configuration["Culture"] ?? "en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture],
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseMiddleware<RequestLoggingScopeMiddleware>(); // was Application_BeginRequest + log4net LogicalThreadContext
app.UseMiddleware<SessionStartMiddleware>();        // was Session_Start in Global.asax

app.MapControllers(); // attribute routes: items/{id}/pic, api/brands, api/files
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.Run();

/// <summary>Replaces Global.asax Session_Start: stamps the machine name and session start time once per session.</summary>
public class SessionStartMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Session.GetString("MachineName") is null)
        {
            context.Session.SetString("MachineName", Environment.MachineName);
            context.Session.SetString("SessionStartTime", DateTime.Now.ToString(CultureInfo.CurrentCulture));
        }
        await next(context);
    }
}

/// <summary>Replaces log4net "activityid" / "requestinfo" thread properties with a logging scope.</summary>
public class RequestLoggingScopeMiddleware(RequestDelegate next, ILogger<RequestLoggingScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["ActivityId"] = context.TraceIdentifier,
            ["RequestInfo"] = $"{context.Request.Path}{context.Request.QueryString}, {context.Request.Headers.UserAgent}",
        }))
        {
            logger.LogDebug("WebApplication_BeginRequest");
            await next(context);
        }
    }
}

public partial class Program; // exposed for WebApplicationFactory in the tests
