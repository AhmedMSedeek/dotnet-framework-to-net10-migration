# Migration report: eShopLegacyMVC, .NET Framework 4.7.2 → .NET 10 LTS

**Scope:** ASP.NET MVC 5 + Web API 2 + EF6 catalog manager → ASP.NET Core MVC on .NET 10 + EF Core 10.
**Goal:** same pages, same URLs, same API contract, same database, so the client can switch over without a data migration.
**Result:** done. 25 automated tests, page output diffed against the legacy app, and the migrated app was run on the database the legacy app created.

Repo layout: tag `before` = the original app. The commits after it are the migration.

---

## 1. Summary

| | Before (.NET Framework 4.7.2) | After (.NET 10) |
|---|---|---|
| Web stack | ASP.NET MVC 5 + Web API 2 (two frameworks), `Global.asax`, Web.config | ASP.NET Core MVC, one pipeline, `Program.cs`, `appsettings.json` |
| Data | EF6, `CreateDatabaseIfNotExists`, hand-written HiLo generator, raw SQL scripts | EF Core 10, migrations, built-in `UseHiLo`, same schema |
| DI | Autofac (MVC + Web API integrations) | Built-in `Microsoft.Extensions.DependencyInjection` |
| Logging | log4net, `LogicalThreadContext` | `ILogger` with a per-request scope (same fields) |
| Static assets | `System.Web.Optimization` bundles | `wwwroot` + `asp-append-version` cache busting |
| Hosting | IIS only (Windows) | Kestrel; IIS (Hosting Bundle), Linux or containers |
| Support | .NET Framework 4.7.2 | .NET 10 LTS, supported until Nov 2028 |
| Tests | none | 25 (xUnit + `WebApplicationFactory`, no database needed) |

### Measured on the same machine (VM: i5-8500T, 16 GB, LocalDB)

| Metric | Before | After | Change |
|---|---|---|---|
| Cold start → first page, existing database | 19.8 s | 3.4 s | **5.8× faster** |
| Cold start → first page, empty database (create + seed) | 48.4 s | 5.0 s | **9.7× faster** |
| Catalog list `/` (median, warm) | 13.8 ms | 4.7 ms | **2.9× faster** |
| Item details (median, warm) | 9.7 ms | 4.0 ms | 2.4× faster |
| `GET /api/brands` (median, warm) | 6.4 ms | 3.4 ms | 1.9× faster |
| Static image (median, warm) | 4.0 ms | 2.7 ms | 1.5× faster |
| Memory (working set, warm) | 295.5 MB | 140.9 MB | **−52%** |

How measured: Release builds. Before = IIS Express; after = published app on Kestrel. 100 sequential `curl` requests per URL after a warm-up; the medians are shown. This compares the two versions on one machine; it is not a load test, and full IIS or production hardware will give different absolute numbers.

---

## 2. Database: adopted as-is, no data migration

The EF Core model reproduces the EF6 schema exactly: tables, column types and lengths, identity columns, the three HiLo sequences, and even the EF6 constraint names (`PK_dbo.Catalog`, `FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId`, …).

**Verified:**
- A database built from the new migration has a schema signature **identical** to the legacy one (columns, FKs incl. delete rules, indexes, sequences). Script: `db/schema-signature.sql`.
- The new app ran against the database **created by the legacy app**, after one script: `db/adopt-existing-ef6-database.sql`. It is idempotent, touches no data and only registers the baseline migration.
- Create/edit/delete through the new app on that database: the new item got id **21**, continuing the legacy `catalog_hilo` sequence (legacy items 1–12), so ids never collide.

---

## 3. Issues found and fixed during the migration

These compiled cleanly. Three of them were caught only by comparing output with the legacy app.

| # | Issue | Impact if missed | Fix |
|---|---|---|---|
| 1 | `Html.ActionLink("Create New", "Create", null, new { @class = … })` binds to a **different overload** in ASP.NET Core (`null` becomes the controller name; the CSS class becomes `?class=…` in the URL). | Buttons and links silently lose their styling. | Named arguments (`routeValues:`, `htmlAttributes:`) + regression test. |
| 2 | `fonts/` lived at the project root. IIS served it; Kestrel serves only `wwwroot`. | Site falls back to Arial; no error anywhere. | Moved to `wwwroot`; a test now checks every `url(...)` in the stylesheets. |
| 3 | `Html.DisplayFor(modelItem => item.X)` inside a `foreach`: ASP.NET Core compiles that closure expression **on every call** (MVC 5 cached it). | Catalog page 33.6 ms vs 13.8 ms before: slower than the legacy app. | Plain Razor output, identical HTML. Now 4.7 ms. |
| 4 | `BinaryFormatter` (`/api/files`): its implementation is removed in .NET 9+ and it is a known security risk. | Endpoint throws at runtime. | JSON with the same property names (see breaking changes). |
| 5 | Mixed-case paths (`~/images/…` for `Images/`, `site.css` for `Site.css`). | 404s on Linux/container hosts. | Paths corrected to real casing. |
| 6 | Prices use `{0:C}` and depend on the server culture. | Linux containers often run invariant culture → `¤19.50`. | Culture pinned to `en-US` (configurable). |
| 7 | Nullable reference types: if enabled, non-nullable `string` properties become implicitly `[Required]`. | `Description` would become mandatory. | Kept disabled in the web project (documented in the `.csproj`). |
| 8 | `DeleteConfirmed` on a missing item threw `NullReferenceException`. | 500 error. | Returns 404. |
| 9 | Picture endpoint built a path from a database value. | Path traversal if the data is ever tampered with. | `Path.GetFileName` + existence check. |

---

## 4. Breaking changes the client must accept

1. **`GET /api/files`** now returns JSON (`application/json`, same `Id`/`Brand` fields) instead of a BinaryFormatter stream served as `text/html`. Any consumer that deserialized the binary payload must switch to JSON.
2. **`GET /api`** ("Hello World" demo controller) is removed. It was unreachable in the legacy app (always 404), so nothing changes for callers.
3. **Logging** goes to the standard .NET logging providers (console/event log) instead of `logFiles\myapp.log`. If the log file must stay, add the `Microsoft.Extensions.Logging.Log4Net.AspNetCore` provider and keep the existing `log4net` config (about 1 hour).
4. **Application Insights**: the classic Web.config modules are removed. Add `Microsoft.ApplicationInsights.AspNetCore` or OpenTelemetry if telemetry is still needed.
5. **At cut-over**, users' session and anti-forgery cookies change name. Open sessions reset once, and a form left open during the switch must be resubmitted.

No other URL, page, form field or JSON shape changed (verified with `docs/compare-output.py`; remaining differences are attribute order, the `/images`→`/Images` fix, and ASP.NET Core omitting a redundant `data-val-number` on `type="number"` inputs).

---

## 5. Cut-over runbook

1. Back up the production database.
2. Run `db/adopt-existing-ef6-database.sql` against it (idempotent; touches no data).
3. Install the **ASP.NET Core 10 Hosting Bundle** on the IIS server; set the app pool to **No Managed Code**. (Or deploy to Linux/containers.)
4. `dotnet publish -c Release` and deploy the output.
5. Configure `ConnectionStrings__CatalogDBContext` (environment variable or `appsettings.Production.json`).
6. Recommended in production: set `Database:MigrateOnStartup=false` and apply future schema changes with `dotnet ef migrations script --idempotent`, reviewed before running.
7. Smoke test: `/`, `/Catalog/Details/1`, `/items/1/pic`, `/api/brands`, then one create/edit/delete.
8. Rollback: the database is unchanged apart from one `__EFMigrationsHistory` row, so the old site can be pointed back at it.

---

## 6. How it was verified

- `dotnet test`: 25 tests (pages, status codes, API contract byte-for-byte, CRUD with anti-forgery, validation message, static assets, CSS `url()` references, serializer).
- `docs/compare-output.py`: normalised diff of legacy vs migrated HTML/JSON (`docs/baseline/` vs `docs/after/`).
- Schema signature diff (legacy DB vs a fresh migrated DB): identical.
- Manual run on the legacy database plus create/edit/delete through SQL Server.
- Before/after screenshots: `docs/images/`.

## 7. Not done (out of scope, recommended next)
- Async EF Core calls (`ToListAsync` etc.): better throughput under load. A small, separate change.
- Upgrade jQuery 3.3.1 / Bootstrap 4.3.1 (client-side, unrelated to .NET).
- CI pipeline (GitHub Actions: build + test on every push).
