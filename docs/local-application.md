# Local ASP.NET MVC application

Updated 13 October 2026. The user authorized treating this repository as a new application, creating local database storage and running it in Chrome. This supersedes the earlier missing-host blocker and frontend exclusion for this local demonstration.

## Implemented stack

| Area | Actual implementation |
| --- | --- |
| Web framework | ASP.NET Core MVC / .NET 9 in src/Dashboard.Web/Dashboard.Web.csproj; .NET SDK 9.0.308 |
| Views | Razor shared layout and Views/SqlMonitoring/Index.cshtml |
| Browser code | Locally bundled jQuery 3.7.1, wwwroot/js/dashboard.js, responsive CSS and native SVG throughput chart |
| Data access | Injected parameterized ADO.NET repository; Microsoft.Data.SqlClient 7.1.1, shared .NET Standard 2.0 backend |
| Database | SQL Server LocalDB SQLDashboardDev, dedicated SQLDashboard_Development, migrations V001–V003 |
| Local access | Loopback-only development identity with server-issued monitoring-instance claim; not a production login provider |
| Hosting | Background Kestrel bound to http://127.0.0.1:5080; configuration supplied through process environment |

The app refuses a non-Development environment, non-LocalDB connection, wrong database or non-integrated SQL authentication. The repository verifies the development database marker. The host rejects untrusted Host headers and grants access only from loopback. No SQL login/password or monitored-server connection is stored in source.

Concrete MVC JSON actions exist for Context, Overview, Queries, Blocking, Waits, FileIo, History and Samples. Responses use camelCase, UTC timestamps, no-store caching, validated limits and safe structured errors. Duplicate/excessive filters are rejected. Monitoring grants come from the server identity, never browser input.

## Run locally

From the repository root:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1 -OpenChrome
~~~

The script creates/starts the dedicated LocalDB instance if needed, applies migrations, reseeds only the named synthetic fixtures, builds the MVC host and starts it in a hidden background process. It attempts to open the verified URL in Chrome. If desktop launch is blocked, open [the dashboard](http://127.0.0.1:5080) manually. The app remains running after startup finishes.

Use -SkipDatabaseSetup to retain the existing fixtures. Stop the owned server before rebuilding changed application assemblies:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Development.ps1
~~~

The stop script checks process identity and retains LocalDB/data. PID/output logs are under ignored artifacts/. Configuration remains in process environment; integrated Windows authentication requires no password. The local connection example uses Encrypt=false only for this LocalDB demonstration.

## Dashboard behavior

Five sections display persisted local data: overview KPIs, expensive completed queries, sampled blocking chains, interval wait composition and database file I/O. Shared filters include configured instance, database, synthetic scenario and relative/custom UTC range. Tables have sorting/paging where supported. Blocking has exact historical sample selection. The view refreshes every 15 seconds without overlapping refresh groups, with Pause/Resume.

Nine scenarios/19 samples cover normal activity, blocking, high query reads, slowdown, no activity, resets, weighted I/O, gaps and partial capture. Frozen timestamps and synthetic labels remain explicit. There is no live source fallback. Instance-only metrics retain their scope under database filtering.

The chart has a tabular equivalent and preserves null/reset/gap separators. Captured text uses Razor encoding or jQuery text insertion. Loading, no-activity, unavailable and partial/error states are shown; filters/navigation have keyboard labels/focus. Browser checks are not a full accessibility audit.

## Verification and launch outcome

- Full solution Release build: zero warnings/errors.
- Backend verifier: 73 passing assertions plus SQL integrity/publication checks.
- Installed Google Chrome via Playwright, headless: 22 passing UI/HTTP checks covering navigation, scenarios, sorting, historical blocking, exclusions, partial notice, pause, mobile overflow, invalid paging, duplicate filters, denied instance, Host rejection and no JavaScript exceptions.
- Screenshots inspected/saved in artifacts/dashboard-overview.png and artifacts/dashboard-mobile.png.
- Root page and JSON endpoints serve successfully; the app was left running on 127.0.0.1:5080.
- Visible Chrome launch was attempted but automatic approval review rejected it as “blocked by policy,” with no further reason. No visible-launch success is claimed.

Repeat browser verification with the app running:

~~~powershell
cd tests/Dashboard.Browser
npm ci
npm test
~~~

Playwright uses installed Chrome; no browser download is needed. See [backend-milestone.md](backend-milestone.md) for SQL/calculation commands.

## Remaining scope

This is a local synthetic demonstration, not full release acceptance. Production identity, live collection, durable scheduling/retention, privileged text policy, all history/detail endpoints, full entity/direction coverage, additional fixtures and measured load/retention sizing remain pending. No administrative actions or production connections are included.

The earlier missing-application documents are historical inspection records. The new paths/versions above resolve that assumption for this local app; they do not claim a pre-existing application was found.
