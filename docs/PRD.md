# SQL Server Monitoring Dashboard PRD

Status: Draft pending inspection of the existing application. Prepared 13 October 2026. This document defines product behavior and acceptance criteria only; it contains no implementation code. All examples are synthetic.

## Purpose and scope

Give database administrators and application support staff a view of SQL Server activity that helps them identify costly queries, blocking, waits, and slow database file I/O. Integrate the dashboard into the user's existing ASP.NET MVC application using C#, Razor views, jQuery, and SQL Server, following that application's established conventions.

The first release includes overview KPIs, expensive queries, blocking chains, wait statistics, database file I/O, and persisted historical snapshots. It supports selecting one configured SQL Server instance at a time, with database filtering where attribution is available. The initial demonstration uses clearly labelled synthetic data. Live collection is a later implementation stage.

Session termination, query execution, automatic tuning, changes to database configuration, alert delivery, execution-plan analysis, deadlock capture, and enabling Query Store are outside this release.

## Repository inspection and integration constraints

Inspected the current working directory and Git refs on 13 October 2026. The repository contains only README.md and Git metadata. Local and remote branches contain only main, with initial commit 159915b. No application exists in the available repository. The parent assignment directory contains only this repository.

| Area | Inspection result | Evidence needed before implementation |
| --- | --- | --- |
| MVC and .NET version | Unverified; the requested stack comes from the user | Solution, project files, target framework, MVC package references and application startup |
| Data access | Unverified | Existing repositories/services, ORM or ADO.NET usage, connection configuration and transaction patterns |
| Authentication and authorization | Unverified | Startup/configuration, existing authorization attributes, roles/claims and representative protected routes |
| UI conventions | Unverified | Shared Razor layout, navigation, views, CSS, jQuery version, bundling, tables, charts and AJAX conventions |
| SQL Server compatibility | Unverified | Server version/edition, deployment model, available DMV permissions and instance scope |
| Collection and persistence | Unverified | Existing scheduler/background processing, storage conventions and deployment topology |

Implementation must reuse the verified framework version, data-access boundaries, authentication mechanism, layout, styling, assets, route patterns, error handling and jQuery conventions. Do not choose MVC 5 versus ASP.NET Core, an ORM, a new identity provider, or a UI framework from this draft. Record representative source paths in this section after the application becomes available. No framework migration is required by this PRD.

## User experience

Provide five sections: Overview, Expensive Queries, Blocking, Waits, and Database File I/O. A shared context bar shows instance, database scope, data mode, time range, collection timestamp, age and refresh controls. Default history range is the last 15 minutes; alternatives are one hour, 24 hours and a custom range within retention. Blocking opens on the latest successful sample; selecting a historical timestamp explicitly enters historical mode.

Use the application's existing navigation and visual conventions once verified. Tables support sorting, pagination and readable units; charts have tabular equivalents. Filters remain consistent across sections. Instance-wide metrics retain a visible instance scope when a database filter is active. Keyboard access, text status labels, clear focus states and loading/empty/error states are required. Color alone must not convey severity.

Every demonstration page displays: **Synthetic data — demonstration only — not connected to SQL Server.** Every table, chart, KPI detail and any exported artifact retains a synthetic label. Never replace failed live collection with synthetic values. Keep synthetic and live history separate, including identifiers and storage partitioning.

## Overview KPIs

| KPI | Meaning and scope | Calculation or source requirement |
| --- | --- | --- |
| Collection health | Collector reachability and freshness, per instance | Success, partial success, failure and timestamp; not a statement that SQL Server is healthy |
| User sessions | Current connected user sessions, instance or attributable database scope | Snapshot count; include sleeping sessions; disclose unknown database attribution |
| Active requests | Current user requests, excluding the collector | Snapshot count, including running, runnable and suspended requests; not just running requests |
| Blocked requests | Requests with an identified positive blocking session ID | Snapshot count, separate from special/unknown blocking owners |
| Batch requests per second | Instance throughput for the measured interval | Difference in the Batch Requests/sec cumulative counter divided by elapsed seconds |
| SQL Server process memory | Current physical memory in use | Snapshot value converted from KB to MiB; distinct from host memory or configured maximum |
| File read and write latency | Operations-weighted latency across selected files | Sum of valid stall deltas divided by sum of corresponding operation deltas |
| SQL Server uptime | Time since the recorded engine start | Current sample time minus engine start, with timezone normalization |

SQL Server process memory is available through the process-memory DMV. Performance counter interpretation depends on counter type; a counter name ending in /sec is not sufficient to treat its raw value as a rate. [Microsoft process memory documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-process-memory-transact-sql?view=sql-server-ver17), [Microsoft performance counter documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-performance-counters-transact-sql?view=sql-server-ver17).

A CPU utilization card is optional pending agreement on a supported process CPU source and permissions. Query CPU time must never be presented as instance CPU utilization. Do not invent a CPU percentage when that source is absent.

Overview acceptance: every KPI exposes units, scope and sample/interval time. Initial cumulative KPIs display “Baseline required.” Missing metrics display “Unavailable,” independently of successful metrics. Clicking blocked requests or I/O latency opens the corresponding section with the same applicable context.

## Expensive queries

Show observed completed query work during the selected interval. Default ranking is total CPU time descending; users can rank by elapsed time, logical reads, executions, or average CPU/elapsed time. Default top 20, with selectable top 50 and pagination. Columns include database attribution, query identity, redacted statement summary, executions, total and average CPU ms, total and average elapsed ms, logical reads, last observed execution and coverage status. Detail shows plan-generation identity and sample boundaries.

The proposed source is sys.dm_exec_query_stats with statement text/context metadata. Its rows follow cached-plan lifetimes and disappear on eviction; statistics reflect completed queries. Query timing totals are in microseconds. This is observed cache activity, not a complete query audit. [Microsoft query statistics documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-query-stats-transact-sql?view=sql-server-ver17).

Difference each stable statement/plan generation before grouping. Identity includes instance epoch, database context when known, plan handle, statement offsets, creation time and plan generation. Query hash may group valid deltas within the same database; do not use it alone for baseline identity or cross-database grouping. Unknown database attribution remains visible as Unknown and is excluded from a specific database filter with an explanation.

Newly observed plans require a baseline; evicted/recompiled plans cannot contribute an invented delta. Collect the eligible population before choosing top N, or explicitly mark truncated coverage. Long executions are attributed to the interval in which their completed counters are observed, even if execution began earlier. Last execution time does not prove that all cumulative work happened inside the selected window. Percentiles are excluded because cumulative totals do not establish a distribution.

Acceptance: rankings use interval deltas, not lifetime totals. A query with no executions in a valid interval has no average; it cannot outrank active queries using a lifetime average. Detail exposes partial cache coverage and missing baselines. Completed-query CPU and current in-flight request CPU remain distinct.

## Blocking

Display a sampled chain from root blocker to blocked requests with session/request identity, database, status, current wait type, current wait duration, elapsed request time, transaction indicators and authorized redacted text. Include sleeping sessions that block other sessions. Distinguish the request's current wait duration from the dashboard's first/last observed blocking timestamps.

Request metadata exposes blocking session IDs and current wait information. Negative blocking IDs represent special owners or unresolved ownership, not real session numbers. Parallel request wait details can require task-level metadata. [Microsoft request documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-requests-transact-sql?view=sql-server-ver17).

Preserve cross-database roots when filtering descendants by database; label that context. Prevent infinite traversal if relationships change during collection or form a cycle. A missing root is labelled “Blocker not captured.” Special owners remain separately labelled. Session identity includes login/start metadata to avoid confusing reused session IDs. Historical samples show observed relationships, not a guaranteed continuous episode or proof of deadlock.

Acceptance: a synthetic sleeping root blocking two requests appears once with two descendants and a blocked-request KPI of two. Unknown/special owners are not counted as ordinary positive-ID blockers. An episode occurring entirely between samples cannot be claimed as captured. No kill-session or administrative action is offered.

## Wait statistics

Show instance-level interval wait totals by wait type: total wait ms, signal wait ms, resource wait ms, task-count change and share of included wait time. Default sort is total wait ms descending. Provide an explicit background/idle exclusion list with its version, and an Include excluded waits toggle. Retain raw counters so filtering does not change history.

The proposed source is sys.dm_os_wait_stats. Total wait time includes signal wait time; it measures completed waits rather than current waits. Wait statistics can be cleared or reset at engine restart. [Microsoft wait statistics documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-wait-stats-transact-sql?view=sql-server-ver17).

Resource wait ms equals total wait delta minus signal wait delta. Share equals a type's valid wait delta divided by the sum of valid included wait deltas. Concurrent workers can produce wait totals exceeding elapsed wall time; percentages represent wait composition, not CPU use or percentage of wall time blocked. Task-count increments and completed wait timing may cross different sample boundaries, so average wait per task is omitted from the first release.

Acceptance: shares recalculate after exclusion changes and sum to approximately 100% when a nonzero denominator exists. A zero denominator displays no observed completed waits and no percentage. Database filtering does not imply database-specific attribution for these instance counters. Current blocking waits remain separate from interval wait statistics.

## Database file I/O

Show one row per data/log file, including database, file identity/name/type, read and write IOPS, MiB/s, read/write latency in ms per operation, operation counts and measured interval. Restrict physical paths to authorized users if paths are included. Provide trends and database summaries using sums and operation-weighted latency.

The proposed source is sys.dm_io_virtual_file_stats joined to file metadata. It provides cumulative operation, byte and stall totals initialized on SQL Server service start. sample_ms has version-specific rollover behavior; it is not the sole measure of elapsed time. [Microsoft file I/O documentation](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-io-virtual-file-stats-transact-sql?view=sql-server-ver17).

Acceptance: data and log files are distinguishable, and zero reads/writes give zero corresponding throughput but undefined latency displayed as “No I/O.” Overall latency is never an arithmetic mean of file latencies. Recreated files or reused database/file IDs require a new identity/baseline. A file missing from collection is not reported as having zero activity.

## Historical snapshots

Proposed defaults are a 15-second collection cadence and 30-day raw retention. These are product targets pending workload/storage validation. Collection runs independently of open browsers, with one active collector per instance and no overlapping cycles. Decide the scheduler/hosting mechanism only after application inspection; an application recycle must not silently erase history.

Each collection cycle records a unique sample ID, instance identity, source mode, schema version, engine start/epoch, UTC start/end times, per-section capture times, elapsed monotonic duration, success/partial/failure status, collection duration, row counts, truncation and failure reasons. Persist raw cumulative counters, gauges, request/chain observations, stable entity identities and per-section coverage. Store sensitive query metadata according to access policy; query text is optional.

DMV reads are not one atomic point in time. Preserve section timestamps rather than implying perfect synchronization. A sample becomes visible after its collected sections and metadata are persisted; failed sections must not borrow an old timestamp from successful sections. Deduplicate sample IDs and order per instance/section to prevent retry double counting. Record outages and retention gaps explicitly.

History supports latest samples, charts and historical blocking lookup. Counter rates use valid intervals; gauges display samples and may have clearly labelled time-weighted averages. Blocking counts across samples are observations, not counts of unique incidents. Historical timestamps use an explicit display timezone while storage remains UTC. Normalize source timestamps with SQL Server timezone context.

For a selected range, include only valid measured intervals wholly within the range and show their actual coverage. Do not prorate intervals at range edges or interpolate missing observations. A narrow range with no complete interval displays insufficient interval coverage. Rollups, if later introduced, retain summed numerators, denominators, measured duration and coverage; they must not average averages or connect across resets.

## Cumulative calculations and resets

For two compatible successful samples A and B of the same entity and epoch, define elapsed seconds T as the actual elapsed time between corresponding section captures and delta C as C at B minus C at A. Scheduled cadence and collection-cycle execution time are not substitutes for T. Use a monotonic clock for interval timing and UTC for presentation; a collector restart or loss of that clock's continuity requires a new timing baseline. Validate identity, section completeness, ordering, T greater than zero and nonnegative deltas before calculating.

| Derived metric | Required calculation |
| --- | --- |
| Batch requests/sec | Delta batch count / T |
| Query total CPU or elapsed ms | Delta time in microseconds / 1,000 |
| Query average CPU or elapsed ms | Delta time in microseconds / (1,000 × delta executions), when executions > 0 |
| Query logical reads per execution | Delta logical reads / delta executions, when executions > 0 |
| File read or write IOPS | Corresponding delta operations / T |
| File read or write MiB/s | Corresponding delta bytes / (1,048,576 × T) |
| File read or write latency | Corresponding delta stall ms / delta operations, when operations > 0 |
| Wait resource ms | Delta total wait ms minus delta signal wait ms |
| Multi-interval rate | Sum valid counter deltas / sum their measured seconds |
| Multi-file or multi-interval latency | Sum valid stall deltas / sum corresponding operation deltas |

Engine restart, failover to a different physical instance, a counter decrease, plan-generation change, file/database identity change or a known manual clear starts a new affected baseline. Never clamp a negative delta to zero or bridge a restart. A wait reset rebases the wait section; it does not invalidate unrelated file counters. Inconsistent values, including signal exceeding total wait or positive query work without execution growth, are marked invalid for the affected derived metrics.

Missing samples produce visible gaps. For the 15-second default, consecutive successful captures separated by more than 30 seconds must not form a chart interval; the next success establishes a baseline. Short delays use their actual elapsed time and expose the interval. Nonpositive time, clock discontinuity without reliable duration, unknown identity and truncated source population invalidate the affected calculations. Raw values and reasons remain available for review.

A clear followed by enough activity to exceed the old value can evade a simple decrease check. A known clear/restart signal invalidates the interval regardless of counter values; otherwise document that unobserved resets cannot always be detected. The collector must never issue commands to clear counters.

## Freshness and collection failures

Freshness is based on the age of the latest successful capture for each section, measured from capture time rather than browser refresh or HTTP response time. Proposed states at the default cadence are Fresh at age 0–30 seconds, Delayed above 30 through 60 seconds, and Stale above 60 seconds. Before any successful capture, display Unavailable. Future or invalid timestamps display timestamp error rather than Fresh.

Show both last attempt and last success. A failed attempt displays its failure immediately even while retained values are still Fresh by age. Partial success marks the affected sections individually. Retain the last known values with their timestamps and error state; missing values remain unknown. Overall freshness reflects the oldest required section, with partial/failure status shown independently. Connection success alone does not establish that every section is available.

Auto-refresh defaults to 15 seconds, can be paused and resumed, and never starts overlapping requests. Pausing the view does not pause collection or stop the displayed age increasing. Historical views show “Historical sample” and their capture timestamp; they are not classified as current Fresh data. A frozen synthetic scenario shows its scenario timestamp and never claims live freshness.

## Synthetic review scenarios

All following names, counters, identities and observations are fabricated. Scenario instance is DEMO-SQL-01, database DemoSales, with samples A and B at 13 October 2026 10:00:00 and 10:00:15 UTC. B is evaluated at 10:00:20 UTC for freshness. No connection or workload is required to review these examples.

| Synthetic cumulative input | Sample A | Sample B | Expected interval output |
| --- | --- | --- | --- |
| Batch count | 100,000 | 101,800 | 120 batches/sec |
| Query Q1 executions | 100 | 120 | 20 observed completions |
| Q1 CPU microseconds | 5,000,000 | 7,000,000 | 2,000 ms total; 100 ms/execution |
| Q1 elapsed microseconds | 8,000,000 | 12,000,000 | 4,000 ms total; 200 ms/execution |
| Q1 logical reads | 80,000 | 110,000 | 30,000 pages; 1,500 pages/execution |
| DemoSales data-file reads | 1,000 | 1,300 | 20 read IOPS |
| Data-file bytes read | 104,857,600 | 136,314,880 | 2.00 MiB/sec |
| Data-file read stall ms | 5,000 | 6,500 | 5.00 ms/read |
| Data-file writes | 500 | 560 | 4 write IOPS |
| Data-file bytes written | 52,428,800 | 58,720,256 | 0.40 MiB/sec |
| Data-file write stall ms | 1,000 | 1,180 | 3.00 ms/write |
| PAGEIOLATCH_SH total/signal wait ms | 10,000 / 1,000 | 16,000 / 1,600 | 6,000 total; 600 signal; 5,400 resource |
| LCK_M_X total/signal wait ms | 2,000 / 200 | 4,000 / 400 | 2,000 total; 200 signal; 1,800 resource |

With only those two wait types included, their shares are 75% and 25%. At B, synthetic gauges are 24 user sessions, eight active requests, two positive-ID blocked requests, 4,096 MiB process memory and seven days uptime. Sleeping session 51 with an open transaction blocks requests in sessions 62 and 63; current waits are 12,000 and 8,000 ms. The blocked KPI is two and the root count is one. These gauges are independent observations, not derivatives of query/file counters. Age at evaluation is five seconds, so the scenario freshness calculation is Fresh, while the synthetic banner remains present.

Additional required synthetic fixtures cover a second expensive query for ranking, multiple files for weighted latency, no activity, unavailable text, unknown database context, permission failure, partial collection, collection timeout, stale data, restart, wait clear, plan eviction/recompile, reused session IDs, file recreation, missing root, special blocking owner and gaps. Label every fixture synthetic and document expected results.

## Access and operational requirements

Reuse existing authentication and server-side authorization for both Razor pages and data/history endpoints. A monitoring permission controls instance access; a separate permission controls sensitive SQL text and host/login metadata. Map these permissions to the existing application model after inspection. Hidden UI elements alone do not provide access control. Render captured text safely and use redacted/parameterized summaries by default; privileged text collection requires a defined retention policy.

Use a dedicated monitoring identity with version-appropriate read permissions; no administrative write permissions are required. For the proposed query/wait/file sources, SQL Server 2019 and earlier generally require VIEW SERVER STATE; SQL Server 2022 and later require VIEW SERVER PERFORMANCE STATE. Verify each selected source against the actual version and deployment. [Microsoft query DMV permissions](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-query-stats-transact-sql?view=sql-server-ver17).

Keep credentials in the application's established server-side configuration/secret mechanism. Do not expose connection strings or permit arbitrary browser-supplied connection targets. History writes are confined to monitoring storage, while target SQL Server access remains read-only. Collector logs expose duration, outcomes, baseline/reset reasons and coverage without credentials or raw SQL text.

Proposed performance targets: latest persisted-data endpoints complete within two seconds at the 95th percentile under ten concurrent viewers; collection normally finishes within five seconds of the 15-second cycle. Use a declared validation workload and bounded timeouts. Missing these targets must produce delayed/partial states, not overlapping collection or silent row truncation. Storage sizing and supported query population are release prerequisites, not assumptions of unlimited capacity.

## Release acceptance criteria

| ID | Scenario | Pass condition |
| --- | --- | --- |
| AC01 | Application inspection | Framework, data access, auth and UI conventions recorded with source paths; integration choices match them before implementation |
| AC02 | Synthetic demonstration | Every section and any export is labelled synthetic; failed live data never activates synthetic fallback |
| AC03 | Overview and navigation | Defined gauges/rates show units and scope; drilldowns preserve applicable filters; instance metrics remain labelled instance-wide |
| AC04 | Reproducible calculations | Synthetic table outputs match within display rounding; full precision retained for calculation |
| AC05 | Query ranking | Interval CPU, elapsed and reads determine ranking; idle lifetime-heavy query cannot appear as currently expensive |
| AC06 | Cache coverage | New/evicted/recompiled plans have baseline/coverage states; unknown database attribution is not fabricated |
| AC07 | Blocking chain | Root 51 and children 62/63 render correctly; sleeping root preserved; special owners and missing roots have explicit states |
| AC08 | Wait composition | Synthetic shares are 75%/25%; exclusion toggles change the denominator; resource and signal totals reconcile |
| AC09 | Weighted I/O | Files with 10 reads at 2 ms and 90 at 10 ms aggregate to 9.2 ms/read, not 6 ms/read |
| AC10 | Zero denominator | No I/O, no executions and no included waits produce appropriate undefined averages/shares without divide errors |
| AC11 | Reset | Counter falls from 101,800 to 50 or engine epoch changes: interval shows Reset/baseline, no negative/zero-clamped rate; next compatible pair resumes |
| AC12 | Delayed sample | A valid 20-second pair uses 20 seconds; a 45-second gap is shown as a gap and requires a new baseline |
| AC13 | Freshness boundaries | Ages 30, 31, 60 and 61 seconds produce Fresh, Delayed, Delayed and Stale; failed attempts leave last-success time unchanged |
| AC14 | Partial failure | Denied wait collection marks waits unavailable/stale while successful file metrics remain usable with their own timestamps |
| AC15 | History | Restart/retry cannot erase or duplicate samples; historical requests preserve identities, mode, UTC times and coverage; no chart crosses a reset/gap |
| AC16 | Access | Unauthenticated users follow existing login behavior; unauthorized users cannot access page/data/history or restricted text; text renders safely |
| AC17 | UI behavior | Keyboard controls, table alternatives, sort/page/filter controls and pause/resume work; no overlapping refresh requests |
| AC18 | Performance and retention | Declared workload meets endpoint/collection targets and 30-day retention sizing; collection persists without an open browser |

No implementation tests or application code are generated at this stage. These criteria define the later validation suite and synthetic review checklist.

## Decisions needed before implementation

Locate the existing MVC application and finish the inspection table. Confirm SQL Server version, edition, instance topology and available permissions. Map monitoring/text access to existing roles; agree text redaction and retention. Validate the proposed cadence, history retention, storage capacity and collector hosting against deployment constraints. Confirm any CPU source and the initial idle/background wait exclusion list. Keep the product requirements above as a draft until those integration details are resolved.
