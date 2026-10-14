# Monitoring snapshot data model and calculations

Design date: 13 October 2026. Proposed logical SQL Server schema, not executable DDL. Read with [architecture.md](architecture.md) and [PRD.md](PRD.md). SQL version, existing persistence conventions and workload are unverified; all SQL types and indexes below require compatibility review before implementation. No requirement for an ORM or database migration is implied.

## 1. Storage conventions and identities

Use a logical `monitoring` schema in approved SQL Server monitoring storage, distinct from read-only monitored targets. Preserve raw cumulative counters and observations; derive interval results in a common comparison service. Separate live and synthetic history through `SourcePartitionId`, enforced in foreign keys and every repository query. Synthetic source partitions additionally require scenario ID and fixture version. An instance alias alone does not identify physical engine continuity.

Types: identifiers `uniqueidentifier` for public sample/partition IDs, internal entity/sample sequence keys `bigint`; raw counters `bigint`; capture/source-normalized times `datetime2(7)` with an enforced UTC convention; source offset minutes `smallint`; names `nvarchar(128)` unless specified; flags `bit`; bounded enum strings or checked small integers. Calculations use checked `decimal(38,6)` or suitable higher intermediate scale, with overflow validation, and retain full precision until display. Do not perform integer division. Foreign keys include source partition where needed to prohibit mode mixing. Return IDs as strings to JavaScript.

Entity identity rules:

| Entity | Identity and continuity |
| --- | --- |
| Source partition | Configured instance ID + sourceMode + synthetic scenario/version where applicable; live cannot reference synthetic IDs |
| Engine epoch | Source partition + verified physical SQL Server identity + engine start source timestamp + normalized UTC/provenance. Restart or physical failover produces new epoch |
| Collector timing run | New ID whenever monotonic-clock continuity is lost, process restarts or ownership moves; independent of engine epoch |
| Database incarnation | Partition + database ID + verified incarnation metadata (available GUID/create metadata). Rename preserves identity; recreate/restore/reuse starts new identity if continuity changes or cannot be proven |
| File incarnation | Database incarnation + file ID + available file GUID/create metadata; observed disappearance/reappearance or ambiguous continuity starts a baseline. Names/paths alone are insufficient |
| Query generation | Epoch + database incarnation/explicit unknown context + plan handle + statement byte offsets + raw creation time/provenance + plan_generation_num; retain SQL handle and query hash as metadata. Hash digest may accelerate lookup but verify complete tuple on collision |
| Session | Epoch + session ID + raw login time/provenance; connection ID/start metadata strengthens identity if available. Do not use session number alone |
| Request | Session identity + request ID + request start metadata; multiple requests per session remain distinct |
| Wait type/counter | Epoch and section reset generation + type or complete counter identity; timing run still constrains interval compatibility |

When a provider does not expose trustworthy identity metadata, mark `identityUncertain` and disable affected comparisons instead of fabricating stable identity. Zero/special plan handles or ambiguous source timestamps need explicit unsupported/uncertain states. Unknown database context has an explicit identity sentinel and attribution state; never guess it from SQL text.

## 2. Tables, keys and indexes

All names are logical proposals. PK means primary key; UQ unique constraint; IX nonunique index. Every fact row belongs to a published sample, with source/epoch checked through composite foreign keys. Snapshot sequence keys are narrow internal clustering candidates; assess partitioning and clustered layout against the actual SQL version and query plans.

| Table | Essential columns / keys | Indexes and purpose |
| --- | --- | --- |
| `MonitoredInstance` | InstanceId PK, display name, enabled, server-side configuration reference; no connection string | UQ configuration identity; authorized instance enumeration |
| `SourcePartition` | PartitionId PK, InstanceId FK, Mode, ScenarioId/Version, retention policy | UQ instance/mode/scenario/version; enforce scenario present only for synthetic |
| `EngineEpoch` | EpochId PK, PartitionId FK, physical identity digest, engineStartRaw, EngineStartUtc nullable, timezone/provenance, first/lastSeenUtc | UQ partition + physical identity + raw engine start tuple; IX partition/firstSeenUtc |
| `CollectorRun` | RunId PK, PartitionId, owner token, monotonic frequency, start/endUtc, timing provenance | IX partition/startUtc |
| `CollectorLease` | PartitionId PK/FK, ownerId, fencingToken, expiresUtc, renewalUtc | IX expiresUtc for recovery; transactionally checked fencing |
| `Snapshot` | SampleId unique public ID, internal SampleKey PK, PartitionId, EpochId nullable if unavailable, RunId, collectionSequence, schemaVersion, start/end/publishedUtc, monotonic duration, status, bounded failure code, expected/collected section count, fencingToken | UQ PartitionId/SampleId; UQ PartitionId/RunId/collectionSequence; IX PartitionId/publishedUtc/SampleKey for latest/history; IX endUtc/SampleKey for bounded purge. IDs + partition/epoch available for composite child FKs |
| `SnapshotSection` | PK SampleKey/Section; PartitionId, captureStart/End/ReferenceUtc, capture monotonic tick, RunId, status, observed/storedRows, truncated, failureReason, resetGeneration, knownReset flag, identity/timing status | IX PartitionId/Section/ReferenceUtc/SampleKey including status/run/reset/truncation; indexed latest-success lookup. Failed section has attempt bounds but no fabricated successful capture time |
| `InstanceSnapshot` | PK SampleKey; epoch, batch raw value + object/counter/instance name + cntr_type, physicalMemoryInUseKb, engineStartUtc; availability flags per input | Latest/history joins by sample; no metric absence encoded as zero |
| `DatabaseIdentity` | DatabaseIdentityKey PK, PartitionId, server database ID, incarnation tuple, first/lastSeen, retiredUtc | IX PartitionId/serverDatabaseId/firstSeenUtc; exact tuple UQ or digest + full collision verification |
| `DatabaseObservation` | PK SampleKey/DatabaseIdentityKey, captured name, attribution/availability | IX DatabaseIdentityKey/SampleKey; prevents applying today's renamed database name to old history |
| `QueryGeneration` | GenerationKey PK, PartitionId/EpochId, database key nullable + context state, planHandle/SQLHandle varbinary(64), offsets int, rawCreationTime, normalizedCreationUtc nullable, planGeneration bigint, queryHash binary(8) nullable, identity digest, first/lastSeen | IX PartitionId/EpochId/identityDigest; full-tuple uniqueness check; IX database/hash for authorized grouping |
| `QueryCounterSnapshot` | PK SampleKey/GenerationKey; executionCount, totalWorkerUs, totalElapsedUs, totalLogicalReads, rawLastExecutionTime, normalizedLastExecutionUtc nullable, metadataState | IX GenerationKey/SampleKey including counters; IX SampleKey/database context through generation join for range/database reads |
| `QuerySummary` | SummaryKey PK, GenerationKey, version, safe summary nvarchar(512), redactionPolicyVersion, textState, createdUtc | IX GenerationKey/version; optional metadata cannot invalidate valid counters |
| `SensitiveQueryMetadata` | Optional, disabled initially; MetadataKey PK, generation/request reference, encrypted text and bounded authorized host/login data, expiresUtc/policy | IX expiresUtc; separate restricted read path/permissions; retention unresolved, not implicitly 30 days |
| `WaitCounterSnapshot` | PK SampleKey/WaitType nvarchar(120); waitingTasksCount, waitTimeMs, signalWaitTimeMs | IX WaitType/SampleKey including counters; reset generation belongs to wait section |
| `WaitExclusionPolicy` / `WaitExclusionType` | PolicyVersion PK, effectiveUtc, rationale; PK PolicyVersion/WaitType | Immutable policy versions; explicit type lookup, no wildcard silently hiding new wait types |
| `FileIdentity` | FileIdentityKey PK, DatabaseIdentityKey, PartitionId, serverFileId int, file GUID/create tuple nullable, identity state, first/lastSeen/retiredUtc | IX DatabaseIdentityKey/serverFileId/firstSeenUtc; tuple/digest identity check |
| `FileIoSnapshot` | PK SampleKey/FileIdentityKey; numReads, bytesRead, readStallMs, numWrites, bytesWritten, writeStallMs, sampleMs raw; captured logicalName/type; optional privileged path reference | IX FileIdentityKey/SampleKey including counters; IX SampleKey/database identity for filtered samples |
| `SessionIdentity` | SessionIdentityKey PK, PartitionId/EpochId, sessionId smallint, rawLoginTime, loginUtc nullable, connectionIdentity nullable, identity digest | IX EpochId/sessionId/rawLoginTime; full identity check |
| `RequestIdentity` | RequestIdentityKey PK, SessionIdentityKey, requestId int, rawStartTime/startUtc nullable | UQ session/request/start tuple; protects against request-ID reuse |
| `SessionSnapshot` | PK SampleKey/SessionIdentityKey; isUserProcess, isCollector, database context nullable, status, openTransactions, metadata availability | IX SampleKey/database context/isUserProcess; include all eligible sleeping sessions, not only blockers |
| `RequestSnapshot` | PK SampleKey/RequestIdentityKey; database key/context, status, currentWaitType, currentWaitMs, elapsedRequestMs, inFlightCpuMs (optional diagnostic), openTransactions, rawBlockingSessionId, safeSummaryKey nullable | IX SampleKey/database context; IX SampleKey/rawBlockingSessionId; in-flight CPU is never query completed CPU/utilization |
| `BlockingEdgeSnapshot` | PK SampleKey/BlockedRequestIdentityKey; blockerSessionIdentityKey nullable, rawOwnerId, ownerKind, relationshipState, observedUtc | IX SampleKey/blockerSessionIdentityKey for forest traversal; positive missing owner still retained; no fake negative session FK |
| `CollectionGap` | GapKey PK, PartitionId, section optional, UTC start/end, reason (outage, storage failure, retention), recordedUtc | IX PartitionId/startUtc/endUtc; section gap annotation |
| `RetentionCheckpoint` | PartitionId PK, policy, purge cutoff, last deleted key, updatedUtc | Durable resumable cleanup; oldest retained bounds returned to UI |

Proposed indexes are a starting design, not a request to create every covering index blindly. Keep wide SQL text out of fact/index keys. Population reads need bounded access plans and must be benchmarked. If storage/time partitioning is available and worthwhile, partition fact tables consistently by capture-date and source partition with compatible keys; do not require an edition-specific feature before inspection. SourcePartition is a logical isolation key, not necessarily SQL physical partitioning.

Check constraints: nonnegative available counters/gauges; section end >= start; valid modes/status/types; known capture provenance; waits signal <= total is validated for derived deltas as well as stored-source diagnostics. Null means unavailable, not zero. Malformed raw capture may be retained as diagnostic records with invalid status rather than forced into normal facts. Failure reasons are bounded safe codes/messages. Publication enforces child source/epoch consistency, counts and unique identities in one transaction.

## 3. Cumulative versus point-in-time data

| Source family | Stored raw interpretation | Derived behavior |
| --- | --- | --- |
| Batch performance counter | Cumulative value only for a verified supported counter type and complete counter identity | Delta/time; a `/sec` suffix alone is insufficient. Unsupported type → unavailable, not raw “rate” |
| Query stats | Completed executions and cumulative CPU/elapsed microseconds/logical reads for cached generation | Difference generation first, then group valid deltas within database/query hash |
| Wait stats | Cumulative completed wait ms, signal ms and task count | Section-reset-aware interval deltas; retain excluded types raw |
| File stats | Cumulative operations, bytes, stalls in ms | Delta/time for throughput, stall/operation for latency |
| Process memory | Gauge: physical memory currently in use in KB | Divide by 1,024 for MiB; no counter differencing |
| User sessions and active requests | Counts from sampled eligible observations | Count at capture; sleeping user sessions included, collector excluded; distinct request identities, not distinct session IDs, for request KPIs |
| Blocked requests | Gauge: request observations with raw positive blocking session ID | Count each request once even when root missing; special/unknown owners separately |
| Request wait/elapsed and transaction indicators | Point observations of a current request, even though durations grow during its lifetime | Display source values; not an interval completion total or proven episode duration |
| Engine start/uptime | Engine metadata + current capture time | Sample capture UTC minus engine-start UTC; uptime is at capture, not advanced by browser clock |

Session database context is the session's observed context, not a complete account of every database it touches. Requests use their captured request database. Unknown attribution is counted separately and excluded from a specific database view. Instance memory, uptime, batches and wait statistics cannot be attributed by database and retain visible instance scope.

Microsoft source confirmations: cached query counters concern completed work and vanish with cache eviction ([query stats](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-query-stats-transact-sql)); performance counter interpretation depends on `cntr_type` ([performance counters](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-performance-counters-transact-sql)); file counters are cumulative and `sample_ms` is version-sensitive ([file stats](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-io-virtual-file-stats-transact-sql)); total wait includes signal and supports clears ([wait stats](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-wait-stats-transact-sql)). These support the PRD's calculation model, not a claim of verified deployment compatibility.

## 4. UTC and interval comparison algorithm

Store capture boundaries in UTC from a reliable collector clock. Also store the monotonic tick/frequency and run identity at each corresponding section reference instant. Choose section read completion as reference; preserve read start/end to expose collection skew. Compute elapsed time T from the difference of reference ticks divided by frequency within the same continuous run. Scheduled cadence, HTTP time and cycle duration are not T. UTC determines range inclusion/display, not an unqualified replacement for monotonic timing.

Normalize source engine start, query creation/last-execution, session login and request start using the SQL Server source timezone and rules at the source event date. Retain raw source value, offset/zone provenance and normalization quality. A present-time offset cannot resolve historical DST ambiguity. If no reliable resolution exists, preserve raw identity metadata, mark ambiguous UTC fields unknown and suppress uptime/affected time-derived results. Never interpret a naive source datetime as UTC. Expose display timezone explicitly, default UTC pending host convention; custom-range inputs must make zone/offset explicit. When UTC jumps or becomes inconsistent with trusted monotonic timing, record `clockDiscontinuity` and establish a new baseline; no chart rate spans that discontinuity.

For every candidate pair A/B:

1. Select consecutive published successful complete captures for the section in the same source partition. Read one predecessor for baseline diagnostics even when outside the range; its interval contributes only if both capture endpoints lie inside the selected bounds.
2. Require B after A, same engine epoch, same timing run/frequency, trustworthy identity, no known section reset, and no incomplete/truncated relevant source population. Validate timestamps and positive finite T.
3. Require T <= 30 seconds at the proposed 15-second cadence. A 20-second delay uses 20 seconds. More than 30 seconds is a gap: B establishes baseline; only a later compatible B/C pair contributes. A failed intervening section attempt is shown immediately; if the successful pair remains within 30 seconds it may still contribute with explicit attempt/coverage annotation, without interpolating the failed observation.
4. Join entity identities. A newly seen query/file/wait type gets baseline, an absent entity is unknown/evicted rather than zero, a changed generation/incarnation cannot join. Missing wait rows are not presumed zero. Lost population completeness invalidates affected full-population rankings/aggregates.
5. Calculate each raw delta, reject decreases and inconsistent relationships. Apply reset scope below. Preserve raw inputs/reasons. The following complete sample is baseline after invalidation; its next compatible pair resumes.
6. Include valid intervals wholly within the requested UTC range, including endpoints. Do not prorate edges, fill gaps or infer completed query timing from last_execution_time. Attribute observed completed work to B's measured interval; retain source last execution separately.
7. Aggregate valid numerators/denominators without averaging averages. Return per-entity coverage, valid interval count and rejected reasons. The denominator for coveragePct is requested duration; covered seconds are the union of valid elapsed spans, not the sum across files/queries.

Latest overview gauges come from latest successful section observations; overview cumulative KPIs use the latest valid pair for their section and label that pair. Selected-range trends/query/wait/file tables use range aggregates and show their actual coverage. No valid complete pair in range → “Insufficient interval coverage.” Different sections can have different latest successful sample IDs; never stamp them all with the newest cycle time.

## 5. Formulas and zero denominators

Let ΔC = C(B) − C(A), T = measured seconds. All formulas require the compatibility algorithm above. For range aggregation, sums run only over valid intervals; a query average is weighted by executions, a latency by operations.

| KPI/metric | Formula | Zero / missing / invalid behavior |
| --- | --- | --- |
| Collection health | Last attempt outcome plus per-required-section last-success age | No success → unavailable. Collection failure independent of target health |
| User sessions | Count eligible captured user sessions including sleeping | Complete capture with no rows → 0. Incomplete capture → partial/unknown exact count |
| Active requests | Count current user requests, collector excluded; running/runnable/suspended included | No requests in complete capture → 0; includes other current user-request states with explicit status |
| Blocked requests | Count captured eligible requests where blocking_session_id > 0 | 0 if none; missing positive root still counts; negative IDs separate |
| Batch requests/sec | Δbatch / T | Δbatch = 0 → 0; no compatible baseline → null |
| Process memory MiB | physicalMemoryInUseKb / 1,024 | Missing/permission failure → null, not 0 |
| Uptime seconds | captureUtc − engineStartUtc | Negative or unresolved normalization → invalid/unavailable |
| Query executions/logical reads | ΔexecutionCount / ΔlogicalReads as totals, respectively | 0 executions excluded from active ranking; positive work without execution growth invalid |
| Query total CPU/elapsed ms | Δmicroseconds / 1,000 | Valid zero work → 0 |
| Query average CPU/elapsed ms | Δmicroseconds / (1,000 × Δexecutions) | Δexecutions = 0 → null “No executions” |
| Logical reads/execution (detail) | ΔlogicalReads / Δexecutions | Zero executions → null |
| Read/write IOPS | Δoperations / T | No operations → 0 for that direction |
| Read/write MiB/s | Δbytes / (1,048,576 × T) | No corresponding operations and bytes → 0; bytes growth without operations → invalid affected direction |
| Read/write latency ms/op | ΔstallMs / Δoperations | Zero operations → null “No I/O”; positive stall with zero operations invalid |
| Resource wait ms | ΔwaitMs − ΔsignalMs | Signal > total → invalid; never clamp to 0 |
| Wait share % | 100 × type valid ΔwaitMs / sum of all included valid type ΔwaitMs | Sum 0 → null “No observed completed waits”; compute denominator before pagination |
| Range rate | Σ valid counter deltas / Σ their measured seconds | No valid duration → null; use each entity's duration, never sum overlapping file seconds as a common clock |
| Scope file rate | For shared complete file intervals: Σ selected-file deltas / Σ interval T | Only shared complete coverage supports a full-scope rate; otherwise report partial subset and coverage, not silently complete throughput |
| Scope/range latency | Σ valid stalls / Σ corresponding operations | Zero operations → null; retain compatible direction-specific coverage |

Memory is SQL Server process physical use, not host memory or configured maximum. Query CPU ms is completed-query work, not process CPU %. Wait totals can exceed wall time across workers; shares express wait composition. First release omits average wait per task and query percentiles. Last execution indicates source execution start, not a reliable interval completion boundary; `lastObservedCompletionUtc` is B's capture when positive completions were observed.

Query ranking computes valid generation deltas first, then groups by database incarnation + nonnull query hash; if hash is unavailable keep generations separate. Unknown contexts are visibly unknown and do not group across known databases. Group averages use summed work/summed completions, not generation means. A group may have valid work and missing generations: display only observed valid work with partial cache coverage, and never assert an exhaustive workload audit. A truncated capture invalidates ranking rather than providing a misleading full-population list.

## 6. Reset and coverage states

| Trigger | Rebaseline scope and visible state |
| --- | --- |
| Engine restart or physical failover | All section counters: new epoch, Reset/baseline; gauges at new sample remain usable |
| Collector timing restart / clock discontinuity | All affected rate/interval comparisons: new timing baseline; gauges unaffected |
| Known wait clear or any wait counter decrease | Entire wait section gets new reset generation, including types whose values did not fall; file/query/batch sections unaffected |
| Batch decrease / counter identity/type change | Batch series only; do not assume wrap arithmetic |
| Query eviction/recompile/creation change | Affected query generation only; new identity baseline, evicted work not invented |
| File/database recreation or decrease in related I/O counters | Affected incarnation/file baseline; never join reused numeric IDs |
| Negative delta or inconsistent work/execution/operation relationship | Affected derived metrics invalid, with raw evidence and next baseline; no negative or zero-clamped result |
| Gap > 30 seconds | Affected section comparison gap and next baseline |
| Missing input/permission error | Unavailable/partial affected metrics; keep old values with old timestamp |
| Truncated section | Invalid affected population calculation; distinct from display pagination |

An unobserved clear followed by enough growth can escape decrease detection. Known clears invalidate even when values increase; without an external trustworthy signal this cannot always be detected. Record this limit. Collector never resets target counters. Cache entries created and evicted between samples cannot be measured. Blocking episodes entirely between observations cannot be claimed as captured.

Proposed wait exclusion policy `demo-v1`, requiring DBA approval before live use: `SLEEP_TASK`, `SLEEP_SYSTEMTASK`, `LAZYWRITER_SLEEP`, `SQLTRACE_BUFFER_FLUSH`, `WAITFOR`, `BROKER_TASK_STOP`, `XE_TIMER_EVENT`, `XE_DISPATCHER_WAIT`. Preserve these raw; the policy is intentionally small and not an authoritative diagnosis of harmlessness. `WAITFOR` can reflect application behavior; label the rationale and let Include excluded waits reveal it. New/unknown types remain included by default. Versioned filtering changes the denominator, never stored history.

## 7. Blocking observations and freshness

Join requests to sessions within the same activity sample. Positive owner maps to a captured session if available; session-only sleeping roots are valid. Negative owners become special-owner nodes, not numeric sessions; zero/null is not a proven ordinary blocker. Construct a session-root/request-descendant forest, merge shared roots, deduplicate edges and use visited-path tracking. Mark cycles, missing roots and collection skew. Sorting/pagination must retain ancestor context; an edge change is an observation, not proof of deadlock.

First/last observed blocking bounds are derived from contiguous observations with matching request/session/owner identities. Break observation continuity on gap, identity change, missing sample or relationship change. Bounds mean “observed at,” not continuous duration. Parallel task-level waits are not collected by default; label request-level wait limitations rather than promising every worker's waits. Microsoft documents special owners and per-task differences in [request metadata](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-requests-transact-sql).

For current live views: age = server evaluation UTC − latest successful section capture UTC. Fresh: 0–30 seconds inclusive; Delayed: >30–60 inclusive; Stale: >60. No success → Unavailable; negative/nonfinite/invalid time → Timestamp error. Overall freshness uses oldest required section; missing required success prevents an overall Fresh state. Last attempt failure/partial status is independent and immediate. Memory/batch input failures can mark submetrics unavailable even when the instance section is partially usable.

Historical data says Historical sample; frozen synthetic data says Frozen synthetic scenario with its evaluation time. The synthetic fixture may calculate an age-state for review (B evaluated five seconds later = Fresh), but must label it scenario evaluation, never live freshness. Pausing browser refresh continues the age display anchored to server time; re-sync on resume, while a browser clock error cannot reclassify data as Fresh.

## 8. Retention and sizing

Proposed raw retention: 30 rolling days per live partition at 15-second cadence, subject to workload approval. This yields 5,760 cycles/day and 172,800 cycles/30 days/instance. All raw counter/session/request/edge snapshots and section metadata expire by sample end UTC, consistently; do not retain an orphaned predecessor beyond cutoff to manufacture an edge interval. State retention bounds explicitly and require a new baseline at the earliest retained capture.

Purge in configurable bounded batches (initial proposal: 1,000 samples, smaller if child volume/transaction time warrants), deleting child facts before headers. Resume from durable checkpoint; new publication must not reference deleted samples. Use a consistent watermark for reads; if retention races a pinned request return an explicit expired-data error instead of mixing sample sets. Record actual oldest retained time and retention gaps, and expose purge lag. Purge gap annotations too after they fall entirely outside retention, retaining only a bounded current retention-boundary marker.

Remove unreferenced query/session/request/file/database/epoch/run identities after their last retained sample and any authorized metadata dependencies expire. Keep configured instances and current policies/leases. Query summaries follow their referenced generations. Optional sensitive metadata has an independently approved short lifetime and access policy; initially disabled. Synthetic fixtures are isolated, deterministic and replaceable by version, with no claim of rolling live retention; do not let their fixed October timestamps expire through the live purge job.

Sizing model: cycles × (header/section bytes + Q × query-row bytes + W × wait-row bytes + F × file-row bytes + S × session-row bytes + R × request-row bytes + E × edge-row bytes), plus entity dimensions, index overhead, logs, free space and growth margin. Q/W/F/S/R/E are observed averages and peaks, not constants assumed unlimited.

Synthetic sizing illustration only: Q=1,000 query rows at an estimated 160 bytes/row gives about 27.65 GB decimal (25.75 GiB) of query facts alone over 30 days per instance, before indexes/other sections. Q=10,000 increases that to about 276.48 GB. Validate actual SQL row/index sizes, peak cardinality, cleanup throughput and storage IOPS before approving 30 days. Do not select only top 20 at capture to reduce storage: that loses valid ranking coverage. If capacity is insufficient, agree cadence/retention/population policy and disclose coverage rather than silently discard work.

## 9. Review vectors

All inputs below are synthetic; no SQL workload is needed. Detailed fixtures and visible results are in [ui-design.md](ui-design.md).

| Vector | Expected calculation/state |
| --- | --- |
| A/B 15 seconds; batch 100,000→101,800 | 120 batches/s |
| Query 100→120 executions; CPU 5M→7M µs; elapsed 8M→12M µs; reads 80k→110k | 20 completions, 2,000 CPU ms/100 avg, 4,000 elapsed ms/200 avg, 30,000 pages/1,500 avg |
| File Δreads 300; Δbytes 31,457,280; Δstall 1,500 ms | 20 IOPS, 2 MiB/s, 5 ms/read |
| File Δwrites 60; Δbytes 6,291,456; Δstall 180 ms | 4 IOPS, 0.4 MiB/s, 3 ms/write |
| Wait A/B total/signal: PAGEIOLATCH_SH Δ6,000/600; LCK_M_X Δ2,000/200 | Resource 5,400/1,800 ms; included shares 75%/25% |
| Two files: 10 reads × 2 ms, 90 reads × 10 ms | (20+900)/100 = 9.2 ms/read |
| Batch 101,800→50 | Reset/baseline, null rate; next compatible pair resumes |
| Same Δbatch 1,800 over 20 seconds / 45 seconds | 90/s / gap and baseline, respectively |
| Freshness ages 30,31,60,61 seconds | Fresh, Delayed, Delayed, Stale |

These are later acceptance inputs, not generated implementation tests. Outstanding prerequisites: actual SQL version/source capabilities, reliable incarnation and timezone metadata, scheduler topology, approved exclusion/text policy, cadence/storage sizing and measured endpoint/cycle workload.
