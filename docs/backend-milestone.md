# First database and backend milestone

Updated 13 October 2026. Status: database scripts and portable backend implemented/verified; MVC application integration remains blocked by missing source/host selection. This is not a completed application milestone.

Follow-up: the user subsequently authorized a new application. A local .NET 9 MVC host, Razor/jQuery dashboard and concrete JSON actions now run successfully. See [local-application.md](local-application.md). Blocker statements below describe the earlier checkpoint.

## Inspection and integration boundary

Before this work, tracked HEAD `159915b` contained only README.md. The working tree also contained the requirements/design/stage documents and `.session` records, but no solution, C# project, application startup, authentication or frontend. No application-development work item was implemented. Documents and future-stage briefs are not implementation evidence.

The installed toolchain is .NET SDK 9.0.308/MSBuild 17.14.28 and SQL Server LocalDB 15.0.4382.1. These are environment findings, not an existing MVC application version. The new backend targets .NET Standard 2.0 to keep the calculation/persistence boundary portable; the executable verification project targets .NET 8. Neither project chooses an MVC host, identity provider, ORM or frontend framework. `DbConnection` is injected so integration can use an approved provider; the verification harness uses Microsoft.Data.SqlClient 7.1.1. See Microsoft's [.NET Standard guidance](https://learn.microsoft.com/en-us/dotnet/standard/net-standard) and the [official provider package](https://www.nuget.org/packages/Microsoft.Data.SqlClient/7.1.1).

The original PRD explicitly requires actual application inspection before choosing MVC 5 versus ASP.NET Core. A question requesting the existing application path or a standalone host choice was raised during this milestone. Until that dependency is resolved, no concrete MVC controllers/routes, authentication integration or HTTP host can be honestly implemented or verified using “existing conventions.” A framework-independent JSON-ready read service is implemented as the integration boundary; it is not an MVC controller.

## Changed implementation files

| Files | Implemented behavior |
| --- | --- |
| `SQLServerDashboard.sln`, `.gitignore` | Buildable backend/verification solution; generated builds and local secrets ignored |
| `sql/000_create_development_database.sql` | Explicit local-only development database bootstrap/marker |
| `sql/migrations/V001__snapshot_identity.sql` | Snapshot/source/engine/timing/entity identities, headers/sections, metadata, leases, gap/retention state and indexes |
| `sql/migrations/V002__snapshot_counters.sql` | Raw counter and activity tables, composite source/epoch FKs, keys/indexes and versioned wait exclusions |
| `sql/migrations/V003__publication_and_integrity.sql` | Section timing-run integrity, blocking-owner integrity and transactional fenced publication procedure |
| `sql/seeds/R__synthetic_scenarios.sql` | Nine synthetic fixture partitions, 19 published snapshots, counters/observations; repeatable isolated transactional reseed |
| `src/Dashboard.Backend/Models.cs` | Typed persistence/read models, metrics, null/state/coverage DTOs |
| `src/Dashboard.Backend/SqlSnapshotRepository.cs` | Parameterized scoped reads, historical watermark, consistent read transaction, 5-second command timeout and bounded reads |
| `src/Dashboard.Backend/SnapshotComparisonService.cs` | Timing/identity validation, baseline/reset/gap handling, batch formula and freshness |
| `src/Dashboard.Backend/FilterValidator.cs` | Allowlisted endpoint inputs, UTC ranges, scenario/database ownership, sort/paging/point limits |
| `src/Dashboard.Backend/DashboardService.cs` | Overview, query grouping/ranking, wait shares/exclusions, file rates/weighted latency and bounded blocking graphs |
| `src/Dashboard.Backend/DashboardReadService.cs` | Server-authorized-instance boundary and JSON-ready Context/Overview/Queries/Blocking/Waits/FileIo/History/Samples payloads |
| `tests/Dashboard.Verification/*`, `sql/verification/VerifyDevelopmentSchema.sql` | Executable assertions against calculation edge cases and persisted development fixtures, plus SQL integrity/publication checks |
| `scripts/Initialize-DevelopmentDatabase.ps1`, `scripts/Verify-Backend.ps1` | Repeatable local setup/build/check commands with native exit-code checks |

The detailed design documents remain specifications, not claims that every proposed endpoint/table capability has shipped. SensitiveQueryMetadata is optional and intentionally absent; raw SQL, host/login strings and physical paths are not stored. Retention checkpoint/gap tables exist, but a purge worker is not implemented. No live collection, production connection, Razor, JavaScript or frontend is included.

## Setup

Run from the repository root on Windows with the .NET SDK, SQL Server LocalDB and SQLCMD installed. NuGet restore needs access to the package feed. All scripts target a newly dedicated local instance/database, not an existing monitored server.

```powershell
sqllocaldb create SQLDashboardDev
sqllocaldb start SQLDashboardDev
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-DevelopmentDatabase.ps1
dotnet build SQLServerDashboard.sln --configuration Release
```

If the local instance already exists, skip `create`; `start` is safe. The bootstrap refuses non-LocalDB servers; migrations/seeding refuse a database other than `SQLDashboard_Development` with its explicit development marker. The repository checks its configured source/database before opening a connection, then verifies LocalDB and the marker. No target SQL Server DMV is read; SQL connections are solely to synthetic snapshot storage.

Credentials and configuration remain outside source. Verification uses integrated Windows authentication, with a connection setting supplied only to the current process environment:

```powershell
$env:DashboardDevelopmentConnection = 'Server=(localdb)\SQLDashboardDev;Database=SQLDashboard_Development;Integrated Security=true;Encrypt=false;Connect Timeout=5'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-Backend.ps1 -Sql
```

`Encrypt=false` applies only to this local LocalDB development example. No password, SQL login, production connection string or secret is committed. `DashboardDevelopmentConnection` is used by the verifier, not a new application-wide secret convention. Reconcile configuration with the actual application when provided.

Without SQL integration, run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-Backend.ps1`. The ExecutionPolicy override applies only to that child process because the machine's default policy blocks scripts; no persistent policy is changed.

Running the initializer again skips applied migration versions and atomically replaces only the nine named version-1 synthetic partitions. Live and unrelated partitions are untouched. Fixture sample IDs/partition IDs and measured values are deterministic; internal identity keys may change on reseed, so old database/query/file tokens must be reloaded. This explicit development reseed is different from collector publication retries. Stop viewers during fixture regeneration; no HTTP host exists yet.

## Verification results

Release build: zero warnings and zero errors. The no-SQL suite contains 29 calculation/filter checks; the SQL suite includes these and 44 additional checks for a total of 73 passing assertions at this checkpoint. SQL schema verification passed: three migration versions, nine scenarios/19 samples, cross-partition identity rejection, duplicate-section rejection, transactional publication/same-ID retry and expired-lease rejection. Verification mutations were rolled back. The initializer ran repeatedly without duplicate fixture samples.

Calculated persisted results include 120 batches/s; Q1 20 completions, 2,000 CPU ms/100 average, 4,000 elapsed ms/200 average and 30,000 reads; included wait shares 75%/25%; data-file read 20 IOPS/2 MiB/s/5 ms and write 4 IOPS/0.4 MiB/s/3 ms; weighted latency 9.2 ms/read; blocking session 51 as one sleeping root with two descendants; slowdown at 50 ms/read; zero denominators as null averages/latencies/shares. Reset sample is null/reset and the next compatible pair resumes at 20 batches/s. A 45-second gap is not bridged. Denied wait capture retains its earlier success timestamp and leaves file metrics usable.

The first SQL integration run exposed a temporary-table scope issue; it was corrected by creating the temp table outside the parameterized execution scope, then rerunning the passing checks. SQLCMD input is explicitly UTF-8 so the synthetic label is preserved.

## Milestone limits and remaining work

1. Supply the existing MVC application or explicitly choose a new standalone host. Integrate concrete MVC JSON actions, configured serializer, authentication/authorization, per-instance grants, safe HTTP errors and cancellation. The current service accepts a server-provided allowed-instance set, not browser grants; no login mechanism is introduced.
2. Context enumeration without an instance, QueryDetail/BlockingDetail/FileIoDetail routes, signed continuation tokens and most history metrics remain pending. Current history exposes raw batch-rate and memory points only, refusing excessive point counts instead of sampling away gaps.
3. Read budgets for this milestone are stricter than the proposed design: 10,000 selected samples and 100,000 total model rows, with at most 100 table rows/50 ranked queries/600 history points. Blocking refuses pages over 500 nodes or depth 64 rather than returning continuation tokens. Validate these limits with the eventual deployment/workload.
4. Complete per-generation/file coverage diagnostics and direction-specific I/O validity. The current I/O service conservatively rejects both directions for an inconsistent file pair; it does not fabricate a valid latency. Compare with the design's more granular intended behavior before declaring WI06 complete.
5. Add remaining full synthetic fixtures for unknown attribution, special/missing roots, file/session reuse, eviction/recompile, collector timeout, and cycle/structural limits. Some reset/recompile/invalid scenarios currently have in-memory mutation checks rather than separate persisted scenario partitions.
6. Implement production-quality retention cleanup, metadata pruning, collector lease acquisition/renewal and durable collection hosting later. Publication validates an existing lease but does not acquire one. No viewer request starts a collector. No production/live collection has been implemented.
7. Add whole-payload JSON numeric precision limits and serializer/HTTP contract tests when the host is chosen. Raw `bigint` persistence models are internal; public IDs are strings, but the host still must enforce safe numeric serialization and scoped sensitive-field policy.
8. Existing-app build/integration, HTTP/auth-expiry/security integration, live permissions, frontend/browser/accessibility, ten-viewer p95 load targets and 30-day retention sizing cannot be verified with the absent application. They remain unrun, not passed. No production tests are permitted in this milestone.

See [work-items.md](work-items.md) for evidence-based partial statuses. This milestone does not complete AC01, AC16–18 or full release acceptance.
