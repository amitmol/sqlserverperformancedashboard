# SQL Server Monitoring Dashboard architecture

Design date: 13 October 2026. Status: proposed design against [PRD.md](PRD.md), which remains a draft. No application implementation, DDL, or tests are introduced. “Required” below means first-release PRD scope, not evidence of separate product approval. Integration names and paths are proposed until the existing application is supplied.

Companion documents: [data-model.md](data-model.md) defines persistence and calculations; [ui-design.md](ui-design.md) defines presentation and synthetic review fixtures.

## 1. Actual project inspection

The working tree contains `README.md` and `docs/PRD.md`. The tracked HEAD contains only `README.md`; `docs/` is currently untracked. Local `main` and remote-tracking `origin/main` point to initial commit `159915b`. The parent assignment directory contains only this repository. No application files or applicable `AGENTS.md` were found. The README describes initial setup, not a working dashboard.

Inspection used the working tree, hidden-file listing excluding Git internals, searches for solution/project/package/configuration/C#/Razor/JavaScript/SQL files, Git status, branches, log, and HEAD tree. Remote-tracking refs were inspected locally; no remote application was fetched. This supersedes the PRD's earlier observation that only the README existed by recording the now-present draft PRD.

| Requested fact | Actual finding | Evidence required to resolve it |
| --- | --- | --- |
| .NET and MVC version | **Unverified.** No `.sln`, `.csproj`, MVC assembly/package references, or startup files | Target framework, MVC references, startup/routing and hosting configuration |
| Data access | **Unverified.** No services, repositories, ORM mappings, ADO.NET usage, or connection configuration | Representative read/write flow, transaction/unit-of-work conventions and migrations |
| Authentication | **Unverified.** No identity setup, authorization attributes, role/claim mapping or protected routes | Login behavior, per-resource authorization, AJAX expiry handling and sensitive-data policy |
| UI conventions | **Unverified.** No shared layout, styles, Razor views, jQuery, chart/table assets or bundles | Layout/navigation, asset versions, accessibility patterns, JSON and AJAX conventions |
| Database/deployment | **Unverified.** No SQL Server compatibility or scheduler/hosting evidence | Version/edition, physical instance/failover model, timezone, permissions, storage and job host |

The user-specified ASP.NET MVC, C#, Razor, jQuery and SQL Server stack is a constraint, not a discovered version. Do not select MVC 5 versus ASP.NET Core, EF versus Dapper/ADO.NET, a new identity provider, a chart library, or a frontend framework from this design. AC01 cannot be completed with this repository. Before implementation, attach source paths for each row and reconcile proposed naming, routes and contracts with that application's conventions.

## 2. Boundaries and data flow

Browser → existing authenticated MVC routes → validated filter/view-model boundary → monitoring read services → monitoring history repositories → SQL Server monitoring storage.

Independent collector → configured read-only SQL Server source adapter → capture/identity validation → atomic sample publication in monitoring storage. A synthetic fixture source uses the same logical capture model with explicitly separate synthetic partitions. Browser requests never query the target DMVs or start collection.

Responsibilities:

| Proposed component | Responsibility |
| --- | --- |
| `SqlMonitoringController` | Razor shell, navigation/filter bootstrap; no metric formulas or direct SQL |
| `SqlMonitoringDataController` | JSON read actions, authorization and validated input; map service results to DTOs |
| `MonitoringAccessService` | Resolve existing monitoring/text permissions, allowed configured instances and authorized context ancestors |
| `MonitoringFilterValidator` | Normalize UTC ranges and allowlisted sorting/paging; reject invalid combinations |
| `MonitoringContextService` | Authorized instances, database identities, retention bounds, section availability and permissions |
| `OverviewService`, `QueryActivityService`, `BlockingService`, `WaitStatisticsService`, `FileIoService`, `HistoryService` | Compose read models, coverage and state from persisted data |
| `SnapshotComparisonService` | Single definition of identity compatibility, baselines, resets, gaps and formulas from data-model.md |
| `FreshnessService` | Per-section capture age and last-attempt status; synthetic/historical treatment |
| `SnapshotRepository` | Header/section/epoch reads, atomic publication and retention boundaries |
| `InstanceSnapshotRepository`, `QuerySnapshotRepository`, `BlockingSnapshotRepository`, `WaitSnapshotRepository`, `FileIoSnapshotRepository` | Parameterized scoped history reads and section persistence using the existing data-access approach |
| `MonitoringCollector` | One nonoverlapping cycle per instance/mode, deadline/cancellation, independent section results |
| `SqlServerSnapshotSource` / `SyntheticSnapshotSource` | Later live read adapter / initial deterministic synthetic scenarios; never automatic fallback |
| `CollectionCoordinator`, `RetentionService` | Durable per-instance lease/fencing; resumable bounded purge |

These are logical boundaries. Fit the existing application's service/repository conventions rather than requiring a new abstraction framework. Namespace, dependency injection, serializer and file placement are unresolved. A module may consolidate closely related classes while keeping these responsibilities.

## 3. Requirement-to-component map

All paths below are **proposed**, not existing source files. Razor paths are under `Views/SqlMonitoring/`; scripts are under `Scripts/monitoring/`, to be relocated into verified asset conventions. Actions are logical action names, not a commitment to framework-specific routing syntax.

| PRD requirement / acceptance | Controller actions | Services → repository methods | View models / DTOs | Razor / JavaScript |
| --- | --- | --- | --- | --- |
| Shared context, one instance, attributable database, synthetic mode (AC02, AC03) | `SqlMonitoring.Index`; `Data.Context` | `ContextService.GetContext`, `AccessService.AuthorizeInstance` → `SnapshotRepository.GetInstances/GetDatabases/GetRetentionBounds` | `DashboardPageViewModel`, `ContextDto`, `DashboardFilter` | `Index.cshtml`, `_ContextBar.cshtml`, `_SyntheticBanner.cshtml`; `monitoring-context.js` |
| Overview gauges, rates, scope and drilldowns (AC03, AC04, AC10) | `Data.Overview` | `OverviewService.GetOverview`, comparison/freshness → `InstanceSnapshotRepository.GetLatestGauges/GetCounterSamples`, `BlockingSnapshotRepository.GetRequestCounts`, `FileIoSnapshotRepository.GetIntervalInputs` | `OverviewDto`, `MetricDto`, `SectionStatusDto` | `_Overview.cshtml`, `_MetricCard.cshtml`; `monitoring-overview.js` |
| Query interval ranking, top 20/50, details and cache coverage (AC05, AC06) | `Data.Queries`, `Data.QueryDetail` | `QueryActivityService.GetRankedQueries/GetDetail` → `QuerySnapshotRepository.GetIntervalInputs/GetGenerationDetail` | `QueryListDto`, `QueryRowDto`, `QueryDetailDto` | `_Queries.cshtml`, `_QueryDetail.cshtml`; `monitoring-queries.js` |
| Sampled blocking, sleeping roots, identity, special/missing owners (AC07) | `Data.Blocking`, `Data.BlockingDetail` | `BlockingService.GetForest/GetDetail` → `BlockingSnapshotRepository.GetSampleSessions/GetSampleRequests/GetEdges/GetObservedBounds` | `BlockingDto`, `BlockingNodeDto`, `BlockingDetailDto` | `_Blocking.cshtml`, `_BlockingDetail.cshtml`; `monitoring-blocking.js` |
| Interval waits, exclusions/version, composition (AC08, AC10) | `Data.Waits` | `WaitStatisticsService.GetWaits` → `WaitSnapshotRepository.GetIntervalInputs/GetExclusionPolicy` | `WaitListDto`, `WaitRowDto` | `_Waits.cshtml`; `monitoring-waits.js` |
| File metrics, weighted database summary and trends (AC09, AC10) | `Data.FileIo`, `Data.FileIoDetail` | `FileIoService.GetFiles/GetDetail` → `FileIoSnapshotRepository.GetIntervalInputs/GetFileIdentity` | `FileIoDto`, `FileIoRowDto`, `FileIoDetailDto` | `_FileIo.cshtml`, `_FileIoDetail.cshtml`; `monitoring-file-io.js` |
| History, sample selection and gap-aware charts (AC11, AC12, AC15) | `Data.History`, `Data.Samples` | `HistoryService.GetSeries/GetSampleList`, comparison → `SnapshotRepository.GetPublishedSections/GetSamples`, relevant counter repository | `HistoryDto`, `SeriesPointDto`, `SampleListDto`, `CoverageDto` | `_HistoryChart.cshtml`, `_SamplePicker.cshtml`; `monitoring-history.js` |
| Freshness/partial failure, refresh, pause, keyboard and table alternatives (AC13, AC14, AC17) | Metadata on all data actions | `FreshnessService.GetStatus`, section metadata reads | `SectionStatusDto`, `ErrorDto` | `_SectionState.cshtml`, all section partials; `monitoring-dashboard.js`, `monitoring-api.js` |
| Server-side access and safe text (AC16) | Both controllers and every detail/history action | `AccessService`, redaction policy → scoped reads only; sensitive repository reads only after permission | `PermissionsDto`, `TextAvailabilityDto`; never serialize unauthorized fields | Shared layout integration, detail partials; text nodes in all scripts |
| Cadence, durable collection, retention and performance (AC18) | No browser collection/purge action | `Collector`, `Coordinator`, `RetentionService` → `SnapshotRepository.TryAcquireLease/PublishSample/GetPurgeCandidates/DeleteSampleBatch` | Internal `CaptureResult`, `SectionCapture`, `PublicationResult` | Context/status only; no operational controls |
| Actual integration inspection (AC01) | Existing app routes pending | No implementation choice until source inspection | Existing conventions pending | Existing layout and asset conventions pending |

Details remain within `Index` using the existing application's partial/dialog/detail-page convention. No execution-plan fetch, query executor, kill-session action, SQL configuration mutation, alert endpoint, or export endpoint is included. If an export is added later, it must preserve source mode and coverage labels.

## 4. Collection and publication

Initial demonstration uses deterministic synthetic fixtures. Live collection is a later implementation stage within these boundaries, after permissions/deployment validation. No target connection is required for the initial demonstration.

Proposed cadence is 15 seconds; normally finish within five seconds. Configure a bounded five-second cycle deadline and shorter section/command deadlines whose budget fits it; exact provider cancellation behavior must be verified. Cancel unfinished sections, persist failure/partial reasons and skip missed scheduled cycles rather than overlap. A deployed scheduler must survive application recycling and operate without viewers; do not assume IIS timers or browser polling satisfy this.

Use a monitoring-storage lease keyed by instance and source partition, with owner, expiry and increasing fencing token. An old owner cannot publish after losing its token. Lease duration/renewal must tolerate the configured deadline and storage latency. Preserve a run identity for the monotonic clock, independent of SQL engine epoch. Collector restart starts a timing baseline even if the engine did not restart.

Read engine/physical identity before and after section capture. A detected epoch change invalidates affected captures in that cycle. Reads of multiple DMVs are not atomic; preserve section capture start/end and reference times. `activity` includes session/request/blocking observations so their KPI and chain share a sample. Other sections are `instance`, `queries`, `waits`, `fileIo`.

Create a unique attempt ID before collection. Publish collected sections and final header in one bounded monitoring-storage transaction. Pending samples are invisible. Same-ID retries are idempotent; conflicting contents are rejected, not overwritten. Ordering is per instance/source/section, and late publication cannot replace a newer last-success pointer. Record failures without overwriting last-success data. If storage itself is unreachable, target data cannot be published: log safely and record an outage on recovery; do not imply a stored failed sample exists.

Collect the eligible query population before ranking. Set a validated configurable collection ceiling and record observed/stored counts, deadline and truncation. A truncated population cannot produce a claimed full-population top N; affected comparisons are unavailable with `populationTruncated`, while safe raw diagnostics remain readable. The supported query population and capacity are release prerequisites.

## 5. JSON AJAX contract

Version 1 is a proposed application-level contract, to be adapted to verified routing and serializer conventions. UTF-8 JSON, camelCase properties, ISO 8601 UTC timestamps with `Z`, invariant numeric format. Identifier strings avoid JavaScript integer precision loss. Raw `bigint` counters are decimal strings when exposed in authorized details; calculated numeric values must be finite and within safe display precision. Undefined numeric values are `null` with an explicit state; never `NaN`, infinity, or a fabricated zero.

Logical read routes under `/SqlMonitoringData/`: `Context`, `Overview`, `Queries`, `QueryDetail`, `Blocking`, `BlockingDetail`, `Waits`, `FileIo`, `FileIoDetail`, `History`, `Samples`. All are authenticated, authorized, side-effect-free GETs reading published storage. No HTTP response starts a capture. URLs must be generated with the existing MVC URL mechanism. Sensitive responses use private/no-store caching. JSON must not embed rendered HTML.

### Filter validation and limits

| Input | Contract and validation |
| --- | --- |
| `instanceId` | Required except for initial context enumeration; opaque configured ID authorized for caller. Never a connection target/string |
| `sourceMode` | Required: `synthetic` or `live`; explicit selection, authorized and available. Never inferred fallback |
| `scenarioId` | Required for synthetic, forbidden for live; known versioned scenario authorized for the selected instance |
| `databaseId` | Optional opaque database-incarnation ID from context, belonging to instance/mode; omitted means all. Unknown attribution is included only in all-databases scope |
| `range` | `15m` (default), `1h`, `24h`, or `custom`. Relative ranges resolve once to absolute UTC bounds per refresh group; synthetic ranges anchor to scenario evaluation time |
| `fromUtc`, `toUtc` | Required for custom; otherwise send the server-resolved bounds to every section. Reject conflicting ranges, missing offsets, reversed/equal bounds, future end, bounds before the configured 30-day retention horizon, or span over 30 days. Normalize explicit offsets to UTC; custom UI submits `Z`. A new collector/scenario may have fewer samples than the requested window: return actual partial/empty coverage, not a filter error. Actual oldest retained data is reported separately |
| `sampleId` | Optional blocking/history detail selector, published and scoped to authorized instance/mode/database context. Exact sample only; do not silently choose nearest. Unavailable/expired ID returns 404 with safe reason |
| `asOfSampleId` | Optional pinned publication watermark returned by context/overview to stabilize pagination and cross-section refresh. No sample after it is considered; older per-section success retains its own sample ID |
| `sort`, `direction` | Endpoint-specific allowlist, `asc`/`desc`; no SQL fragments. Query: totalCpuMs (default desc), totalElapsedMs, logicalReads, executions, avgCpuMs, avgElapsedMs. Waits: totalWaitMs (default desc), signalWaitMs, resourceWaitMs, taskCount, sharePct. I/O: database, fileName, fileType, readIops, writeIops, readMiBps, writeMiBps, readLatencyMs, writeLatencyMs. Blocking: blockedDescendants, currentWaitMs, database, sessionId. Nulls last, then opaque identity as deterministic tie-breaker |
| `top`, `page`, `pageSize` | Queries: `top` 20 default or 50, applied after interval calculation/grouping/ranking. Page is one-based; pageSize 10/20/50, default 20, maximum 50. Other lists pageSize 20/50/100, default 20, maximum 100. Reject zero/negative or page beyond 1,000; page beyond available rows returns empty with unchanged totals |
| `includeExcluded` | Waits only, true/false default false. Returns policy version and denominator covering all included valid types, independent of current page |
| `metric`, `maxPoints` | History metric allowlist: sessions, activeRequests, blockedRequests, batchRequestsPerSec, processMemoryMiB, uptimeSeconds, readLatencyMs, writeLatencyMs, readIops, writeIops, readMiBps, writeMiBps, totalWaitMs. Default 240, range 30–600; at most eight series. Resolve attribution per metric; wait/instance metrics remain instance scope |
| Detail IDs | Query generation/group, file, node IDs are bounded opaque tokens (maximum 128 characters); resolve in authorized pinned context. Browser supplies no plan handles, SQL, filesystem paths or regex |
| `continuationToken` | Blocking descendants and sample lists only; opaque server-validated token, maximum 1,024 characters, bound to caller/instance/mode/filter/watermark/root and expiry. Reject changed context or expired token; continuation obeys the same node/row limits |

Reject unknown/conflicting filter fields rather than silently interpreting them. Validate enum values, booleans, length, range, per-instance access and database/source ownership server-side. Use parameterized queries and allowlisted sort mappings. Database filters on instance metrics remain in shared context but return `scope=instance`, `databaseFilterApplied=false` and an explanation.

Hard response limits: 100 table rows, 50 ranked queries, 100 sample-list rows, 600 points per series, eight series. Blocking returns at most 50 complete roots and 500 nodes per response, maximum traversal depth 64, with per-root continuation for omitted descendants; totals come from the full captured sample. Page by roots so parents are preserved. On node/depth budget exhaustion report structural truncation and `hasMore`; never claim a complete chain.

Proposed per-request work budget: at most 200,000 section samples and 1,000,000 raw counter/observation rows, to be validated with the supported workload. Preflight indexed counts and stop bounded reads on overflow. Return `rangeTooLarge` (422) with a narrower-range suggestion; do not silently restrict the range or rank only a scanned prefix. This may limit 30-day query ranking until capacity is proven; retention is not a promise that every dense 30-day query scan meets two seconds. No new rollup subsystem is assumed.

### Common success envelope

| Property | Meaning |
| --- | --- |
| `contractVersion`, `requestId`, `generatedAtUtc` | Version 1, correlation ID, response generation time (not collection time) |
| `context` | instance/database IDs and names, sourceMode/scenarioId, syntheticLabel, viewMode (`current`/`historical`/`frozenSynthetic`), normalized UTC range, displayTimezone, asOfSampleId |
| `collection` | last attempt ID/time/status, latest success; required-section freshness and separate failure/partial status |
| `sections[]` | section name, displayed sample ID, capture start/end/reference UTC, lastSuccessUtc, lastAttemptUtc, attemptStatus, ageSeconds, freshnessState, truncation, reasonCodes |
| `coverage` | requested bounds/duration, actual valid interval bounds, measured seconds, union-covered seconds, coveragePct, accepted/rejected interval counts, gap/reset/baseline counts, scope, populationCompleteness |
| `data` | Endpoint-specific DTO from the table below |
| `warnings[]` | Safe codes/messages: scope, cache limitation, unknown attribution, partial/structural coverage; no credentials or raw SQL |
| `paging` | page/pageSize, totalEligibleRows, totalRankedRows where relevant, returnedRows, hasMore, pinned watermark; absent for unpaged data |

For entity-dependent metrics, coverage belongs to the row/metric as well as the response; do not sum overlapping entity durations into a section's coverage percentage. A metadata failure is an HTTP error. A successfully read partial/stale/unavailable collection is HTTP 200 with section/metric states, even if every section is unavailable; this distinguishes collection health from HTTP transport failure.

| DTO | Endpoint-specific data shape |
| --- | --- |
| `ContextDto` | allowedInstances, allowedDatabaseIncarnations, modes/scenarios, retention bounds, permissions, cadence/refresh defaults, allowed filter values |
| `OverviewDto` | `metrics[]`, each with key, value/null, unit, scope, databaseFilterApplied, state, reason, sampleId, capturedAtUtc, intervalStartUtc/endUtc, measuredSeconds, coverage, drilldown; collectionHealth separately |
| `QueryListDto` | rows: queryId, database/unknown attribution, authorized redacted summary/textState, executions, totalCpuMs, avgCpuMs, totalElapsedMs, avgElapsedMs, logicalReads, lastExecutionUtc, lastObservedCompletionUtc, planGenerationCount, coverage. Full ranked totals exclude baseline-only/zero-execution rows; incomplete generations appear in diagnostics/detail |
| `QueryDetailDto` | same metrics plus generation identities, sample pairs, inclusion/reset reasons and text availability; no execution plan or execution action |
| `BlockingDto` | roots/nodes/edges, fullSampleBlockedRequestCount, rootCount, specialOwnerCount, sampleUtc, historical flag. Node includes opaque nodeId, sessionId/requestId, database, status, currentWaitType/currentWaitMs, elapsedRequestMs, openTransactions, textState, contextOnly, missing/special/cycle status, hasMoreChildren. Edge includes observedAtUtc and first/last observed bounds if available |
| `WaitListDto` | exclusionPolicyVersion, excludedTypes, includeExcluded, includedTotalWaitMs, rows: waitType, totalWaitMs, signalWaitMs, resourceWaitMs, taskCount, sharePct/null, state, coverage |
| `FileIoDto` | summaries for the complete selected scope plus rows: fileId, databaseId/name, logicalName/type, reads/writes, read/writeIops, read/writeMiBps, read/writeLatencyMs/null, direction-specific states, interval bounds/seconds and coverage. Physical paths absent by default |
| `HistoryDto` | metric/unit/scope, bucket rule, series[] with named points: start/endUtc, value/null, state/reason, sample IDs, measured/covered seconds, coverage, aggregation kind; separate gauge/interval semantics |
| `SampleListDto` | published sample IDs, section availability, UTC capture bounds/status/source, continuation; exact lookup used for blocking |

`BlockingDetailDto` uses the same node/edge field definitions for one scoped request/session and its observed bounds. `FileIoDetailDto` uses the same file-row definitions plus incarnation metadata, paired raw inputs and bounded history points. Continuations return `continuationToken` and `hasMore`, including per-root continuation for blocking descendants. No detail response bypasses the common payload limits or authorization.

`MetricDto.state` is one of `valid`, `baselineRequired`, `reset`, `gap`, `noActivity`, `unavailable`, `invalid`, `partial`. Freshness is separate: `fresh`, `delayed`, `stale`, `unavailable`, `timestampError`, `historical`, `frozenSynthetic`. `noActivity` may have a defined zero rate but null average/latency/share. Reason codes specify the affected metric; partial coverage does not convert unknown work into zero.

Synthetic contract example (data only, not implementation): a batch metric has value 120, unit `batches/s`, scope `instance`, state `valid`, sample A/B times `2026-10-13T10:00:00Z`/`2026-10-13T10:00:15Z`, measuredSeconds 15, sourceMode `synthetic` and the required synthetic label. A file with zero read operations returns readIops 0, readMiBps 0, readLatencyMs null, readLatencyState `noActivity`. All fields ride inside the common envelope.

Illustrative nested metric payload (synthetic contract specimen, not implementation code; common envelope fields are defined above):

```json
{
  "key": "batchRequestsPerSec",
  "value": 120,
  "unit": "batches/s",
  "scope": "instance",
  "databaseFilterApplied": false,
  "state": "valid",
  "reason": null,
  "sampleId": "synthetic-sample-B",
  "capturedAtUtc": "2026-10-13T10:00:15Z",
  "intervalStartUtc": "2026-10-13T10:00:00Z",
  "intervalEndUtc": "2026-10-13T10:00:15Z",
  "measuredSeconds": 15,
  "coverage": {
    "acceptedIntervals": 1,
    "coveredSeconds": 15,
    "populationCompleteness": "complete"
  },
  "syntheticLabel": "Synthetic data — demonstration only — not connected to SQL Server."
}
```

The sample token above is illustrative; stored public sample IDs are opaque UUID strings. Metric/detail objects carry their synthetic label so detached renderers preserve it; sourceMode/scenario identity is authoritative in the envelope.

### Errors, refresh and payload safety

Error envelope: `contractVersion`, `requestId`, `generatedAtUtc`, `error` containing `code`, safe `message`, optional fieldErrors and retryAfterSeconds; no `data`, stack trace, connection string, raw query or SQL exception. 400 invalid filters; 401 expired/missing authentication; 403 denied monitoring permission; 404 inaccessible/absent scoped identifier (avoid revealing other instances); 422 work budget exceeded; 429 bounded-reader pressure with Retry-After; 500 internal read error; 503 storage unavailable with retry guidance. Razor login behavior follows existing auth; JSON must expose authentication expiry as a structured status instead of a 200 login HTML response, using the verified framework's supported mechanism.

`monitoring-api.js` validates content type/version and handles envelopes centrally. `monitoring-dashboard.js` runs one refresh group at a time with jQuery AJAX, a filter generation token and cancellation of obsolete requests. Resolve range/watermark once, then fetch section payloads; ignore out-of-order responses from earlier contexts. One pending request per section; next automatic refresh begins after the group settles and the interval permits. Retry transport failures with bounded backoff, pause repeated failures visibly, and support manual retry. Pause stops refresh, not collection or the age timer. A client timeout does not cancel collection. Avoid broad global AJAX behavior changes to the host application.

## 6. History aggregation, security and operations

Trend point budgets use explicitly labelled buckets. Rates use summed valid numerators divided by measured seconds; file latency uses summed stalls/operations. Gauge buckets show min/max/last (default), not an unexplained average. Wait buckets sum completed waits. Split every bucket at resets, gaps and source changes; return null separators. If requested resolution cannot represent all discontinuities within the point cap, return 422 asking for a narrower range; do not hide gaps or connect across them. Raw table/sample inspection remains available. No persisted rollups are included in release one.

Authorize every instance, database/detail ID and ancestor node server-side. Monitoring permission grants instance visibility; sensitive text and host/login data require a separate existing permission. Cross-database blocking context requires the user to be entitled to see that ancestor; otherwise render an authorized minimal placeholder. Default text is a safe redacted/parameterized summary or “Unavailable”; do not assume literal stripping makes arbitrary SQL safe. Until policy is agreed, do not persist full SQL text or host/login strings. Use session login timestamp for identity without serializing login names. Use Razor encoding and jQuery text insertion; chart tooltips receive text, not captured HTML.

Keep target monitoring credentials in the existing server-side secret mechanism. Target access is read-only; monitoring-storage writer is a distinct least-privilege role. No clear-wait command is issued. Query/wait/file DMV permission differences by server version must be verified: Microsoft documents VIEW SERVER STATE for older SQL Server and VIEW SERVER PERFORMANCE STATE for SQL Server 2022+ for the query DMV; other chosen sources and deployment types need their own checks. [Microsoft query DMV permissions](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-query-stats-transact-sql#permissions).

Log request/capture correlation, elapsed duration, section status, counts, reset/gap reasons and purge progress without query text, credentials or privileged identity metadata. HTTP endpoint p95 target is two seconds for ten concurrent viewers; declared validation must state instances, query generations, files, sessions, waits, retention and response sizes. Collection p95/normal-cycle target is five seconds with 15-second cadence. Indexed reads, hard work budgets and no live DMV calls on viewer requests support these targets but do not establish them without measurements.

## 7. Unresolved assumptions and optional work

| Decision | Proposed position | Resolution needed |
| --- | --- | --- |
| Application source | No version/dependency selection | Locate source and finish AC01 before application implementation |
| PRD approval | Treat listed first-release scope as design input | Product acceptance of draft and outstanding integration decisions |
| Hosting/scheduler | Durable independent worker, one fenced collector per instance | Existing job host, recycle/failover behavior and lease location |
| SQL deployment | Standalone/managed topology not assumed | Versions, physical identity, failover and source-specific permission checks |
| Identity fidelity | New baseline when incarnation cannot be verified | Available database/file identity metadata and timezone normalization |
| Cadence/retention/caps | 15 seconds / 30 days, provisional API/work bounds above | Workload sizing and endpoint/cycle validation |
| Permissions/text | Existing monitoring/text permissions; raw text disabled | Roles/claims, redaction review and sensitive retention agreement |
| UI/assets/JSON | Existing layout, jQuery and supported chart/table tools | Inspect bundles, serializers, date/auth handling and accessibility patterns |
| Wait exclusions | Small proposed versioned list in data model | DBA approval of production list, source version and rationale |

Optional, separately approved work: supported process CPU card; physical paths/privileged SQL text after policy; persisted rollups; exports. CPU requires a supported source and permission and must not use completed-query CPU as utilization. Explicitly outside PRD scope: session termination, query execution, automatic tuning/configuration changes, alert delivery, execution plans, deadlock capture and enabling Query Store.
