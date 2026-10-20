# Pentia case - Wingineers

ASP.NET Core MVC application for the Wingineers "Measure it all" sales-data case,
with salesperson information, order histories, and monthly order reporting.

## Requirements and running

- .NET 10 SDK (LTS). `global.json` selects SDK 10.0.401 or a later patch in that feature band.
- VS Code with C# support, Visual Studio with .NET 10 support, or the .NET CLI.

From this directory:

```powershell
dotnet restore PentiaCase.sln
dotnet build PentiaCase.sln --no-restore
dotnet run --project src\PentiaCase.Web --launch-profile http
```

Open <http://localhost:5070>. For HTTPS, trust the local development certificate
with `dotnet dev-certs https --trust`, then use `--launch-profile https` and
open <https://localhost:7114>.

## Data loading and refresh

The first homepage visit loads salespeople and orders concurrently from the
Wingineers API. Direct visits to data pages also initialize the cache if needed.
Every page uses the same validated, in-memory snapshot; there is no automatic
expiry. Restarting the application clears the cache.

Use **Refresh data** in the header to fetch both endpoints again. Only a complete,
validated response pair replaces the cache and its UTC fetch timestamp. Failed
refreshes keep the previous data and timestamp and display an explicit warning.
After refreshing, the current page and query string are reloaded from the cache.

## Structure

- `src\PentiaCase.Web\Controllers`: request handling.
- `src\PentiaCase.Web\Views`: Razor views and shared navigation.
- `src\PentiaCase.Web\Models\Api`: external API response models.
- `src\PentiaCase.Web\Models\Caching`: the combined cached data snapshot.
- `src\PentiaCase.Web\Models\ViewModels`: page and shared view models, one type per file.
- `src\PentiaCase.Web\Services\Api`: HTTP integration, response validation, and API configuration.
- `src\PentiaCase.Web\Services\Caching`: shared snapshot loading and refresh.
- `src\PentiaCase.Web\Services\Salespeople`: salesperson lists, order counts, and detail projections.
- `src\PentiaCase.Web\Services\Reports`: monthly grouping, zero-order months, and report projections.
- `src\PentiaCase.Web\Serialization`: JSON date converters.
- `src\PentiaCase.Web\Program.cs`: dependency injection, typed HTTP client, and MVC routing.
- `src\PentiaCase.Web\appsettings.json`: non-secret settings.

The solution uses one MVC application project and an xUnit test project, with no
database. External API DTOs are separate from page view models.
Feature services transform a supplied snapshot into view models without HTTP or
cache dependencies. Controllers load snapshots and retain HTTP status handling,
refresh redirects, fetch-time metadata, and error messages.

## Pages

| Route | Intended behavior |
| --- | --- |
| `/` | Case overview and navigation |
| `/Salespeople` | All salespeople, including zero-order salespeople, with order counts and detail links |
| `/Salespeople/Details/{id}` | Salesperson information and their orders |
| `/Reports/MonthlyOrders` | Chronological monthly order counts, including zero-order months within the report period |

The detail route accepts a positive integer salesperson ID. Unknown IDs return 404.

## Customer API setup

Swagger: <https://azurecandidatetestapi.azurewebsites.net/swagger/index.html>

The base URL is configured under `WingineersApi:BaseUrl`. The typed
`WingineersApiClient` uses a 30-second timeout and sends the configured API key
in the `ApiKey` header on server-side requests to Wingineers. Configuration is
validated at startup.

When changing the integration, verify the API v1 contract for:

1. Endpoint paths, response shapes, identifiers, and any pagination.
2. The API-key header or other authentication scheme.
3. Order timestamps, time zones, and the relationship between orders and salespeople.

The supplied `xxx` is not a usable API key. Do not commit a real key to settings,
source code, or launch profiles. This project has a user-secrets ID configured.
Store a real development key locally:

```powershell
dotnet user-secrets set "WingineersApi:ApiKey" "<your-api-key>" --project src\PentiaCase.Web
```

Local controllers do not require authentication or an `ApiKey` request header.
This application is intended to run on localhost; add appropriate access controls
before exposing it to other users. The configured key is required only for
outbound Wingineers API calls and is not included in page HTML or browser links.
For deployment, supply `WingineersApi__ApiKey` through the hosting environment's
secret configuration. User secrets are for development, not a production vault.

## Implementation checklist

- Fetch the complete relevant datasets; the customer API cannot filter or sort.
  Account for pagination if present. Do not fetch all orders once per salesperson.
- Join orders to salespeople by verified identifiers. Preserve people with zero orders.
- Show salesperson information and associated orders; handle unknown IDs with a 404.
- Group orders by year and month, not month name alone. Define the reporting time
  zone and date range, fill missing months, and sort chronologically.
- Distinguish genuine empty results from authentication errors, timeouts, and
  invalid API responses. Log failures without secrets and show an explicit error.
- Keep xUnit regression tests for zero-order people, unknown IDs, month boundaries,
  invalid data, cache replacement, and upstream failures.

## Presentation discussion

- **Data quality:** identify duplicates, missing IDs, orphan orders, invalid dates,
  and inconsistent time zones. Agree on validation and correction policies
  instead of silently dropping problematic records.
- **Performance:** retrieve shared datasets once per operation, use indexed
  lookups for joins, and consider bounded caching with a documented freshness policy.
  Measure before adding complexity.
- **Future API:** consider server-side pagination, filtering, stable sorting,
  a salesperson order-count projection, a salesperson-specific orders endpoint,
  and a monthly aggregate endpoint with explicit date range and time-zone semantics.
  Define authentication, error contracts, and consistency guarantees.

Styling is the standard MVC Bootstrap template; no custom design work is included.
