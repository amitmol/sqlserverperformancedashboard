# SQL Server Monitoring Dashboard UI design

Design date: 13 October 2026. Design only; no Razor, C#, CSS or JavaScript implementation. Based on [PRD.md](PRD.md), with contracts in [architecture.md](architecture.md) and calculations in [data-model.md](data-model.md).

The existing application UI is unavailable. Colors, spacing, fonts, navigation placement, chart/table libraries, breakpoints and jQuery version remain unverified. Reuse its shared Razor layout and patterns after inspection. This document specifies hierarchy and behavior without prescribing Bootstrap, a new component framework, or an invented brand.

## 1. Page structure

One authenticated dashboard page with five sections: Overview, Expensive Queries, Blocking, Waits, Database File I/O. Use existing navigation conventions to expose these sections. Maintain a single context as users move between sections, including when using KPI drilldowns or browser Back. Encode approved filter values in page navigation state; never store credentials or raw query text in URLs.

Reading order:

1. Application navigation and page title “SQL Server Monitoring.”
2. Persistent synthetic banner when in synthetic mode: **Synthetic data — demonstration only — not connected to SQL Server.**
3. Context/filter bar with configured instance, database scope, mode/scenario, range, timezone and refresh controls.
4. Collection summary: last attempt/outcome, latest successful capture, age, per-section state and coverage.
5. Section navigation, followed by the active section's content and state messages.
6. Metric definition/coverage notes and sample detail as appropriate.

Desktop: filter bar can use two aligned rows; KPI cards wrap into a grid, followed by charts and tables. Narrow view: stack filters/cards, allow controlled table horizontal scrolling with readable headers, and make details an accessible full-width panel. Do not squeeze units, timestamps or state labels into unreadable abbreviations. Implement actual breakpoints from existing styles.

Each KPI detail, table caption and chart title includes a Synthetic data label when applicable, even though the page banner is also present. Any future saved/exported artifact must retain the full synthetic label, instance, scope, UTC range and coverage. Initial demo is a frozen reproducible scenario, not a simulation pretending to be live.

## 2. Shared filters and refresh

| Control | Design and behavior |
| --- | --- |
| Instance | One configured authorized instance at a time. Initial synthetic instance `DEMO-SQL-01`. Display names, submit opaque IDs. No editable host/connection target |
| Database | “All databases” default; authorized database-incarnation options, e.g. synthetic `DemoSales`. Explain unknown attribution exclusion on specific filter. Instance-wide cards/waits remain explicitly labelled |
| Data mode | Explicit Synthetic/Live options only when available; initial demonstration has Synthetic and a clear unavailable Live explanation. No fallback on failure |
| Scenario | Synthetic-only named fixture/version; switching scenario resets pinned samples and detail selection |
| Time range | Last 15 minutes default, 1 hour, 24 hours, custom within retention. Show resolved bounds and display timezone. Frozen scenario ranges anchor to scenario evaluation time |
| Custom dates | Label timezone and validate start/end together; show inline field errors. Submit explicit UTC/offset values and explain retention bounds without silently clipping |
| Historical blocking selector | “Latest successful sample” default. Exact timestamp/sample selection from paged available samples enters historical mode. Current-range filtering does not imply a continuous blocking episode |
| Refresh | Manual Refresh plus auto-refresh every 15 seconds, Pause/Resume. Controls announce next state. Show paused status and continuing age; collection runs independently |
| Section sort/page | Allowlisted columns, visible sort direction, page/size and total; changing context/sort/top returns page 1. Pin watermark across pages to keep results stable |

Select filters, then apply them as one context update (or the verified host's immediate-apply convention). Do not issue a request for half-entered custom ranges. Abandon/ignore obsolete AJAX results using the filter-generation token. During refresh keep old data only within the same instance/database/mode context, with a Refreshing label; after a context switch clear incompatible values. Refresh never leaves old live rows under a new synthetic label or vice versa.

Blocking history shows “Historical sample — captured [timestamp]” and a Return to latest control. Auto-refresh does not move a selected historical sample. The shared time range constrains the available historical sample list; exact selection controls the chain. Frozen synthetic display shows scenario sample and evaluation timestamps; a reviewed freshness state says “Scenario evaluation: Fresh (5 seconds),” not live Fresh.

## 3. Overview

Cards show value, unit, scope, sample/interval time, state and a definition. Required cards:

| Card | Primary display | Context / interaction |
| --- | --- | --- |
| Collection health | Latest attempt: Success / Partial / Failed; section freshness summary | “Collector status; not SQL Server health.” Expand per-section attempts/success timestamps |
| User sessions | Captured user-session count including sleeping | Attributable database scope or instance; unknown context count disclosed |
| Active requests | Current user-request count | Running/runnable/suspended included; excludes collector |
| Blocked requests | Positive-owner blocked-request count | Opens Blocking with applicable filters; root count shown separately |
| Batch requests/sec | Latest valid interval rate | Always instance-wide; interval boundaries and seconds |
| Process memory | MiB physical memory in use by SQL Server process | Instance-wide; not host memory or configured maximum |
| Read latency | Operations-weighted ms/read | Selected-file scope, coverage; opens Database File I/O |
| Write latency | Operations-weighted ms/write | Selected-file scope, coverage; opens Database File I/O |
| SQL Server uptime | Days/hours at capture | Instance-wide, normalized engine start and capture time |

Do not add a CPU percentage card unless optional source/permissions are agreed. Missing counter baseline displays “Baseline required,” not zero. A real zero rate is 0 with a valid measured interval. Missing/denied data displays Unavailable with safe reason. Undefined latency with no operations says No I/O.

Below cards: throughput trend (batches/s) and observed activity trend (sessions, active requests, blocked requests), with units and independent axes/panels to avoid unit confusion. Provide read/write latency trends where useful and a common gap-aware sample table. Cards are latest values; charts use the selected range, explicitly labelled. Charts never connect across reset/gap. Aggregated chart buckets expose aggregation kind, coverage and underlying range.

## 4. Expensive Queries

Section title “Observed completed query work.” Short persistent note: “Cached-plan observations; incomplete workload coverage is possible. In-flight requests are separate.”

Ranking controls: total CPU (default descending), total elapsed, logical reads, executions, average CPU or average elapsed. Top 20 default, Top 50 alternative. Paginate the selected top set after ranking all eligible valid deltas; do not claim top queries from a truncated capture.

Columns: database (or Unknown), query identity, redacted statement summary/text state, executions, total CPU ms, average CPU ms, total elapsed ms, average elapsed ms, logical reads (pages), last observed execution, coverage. Header/tooltips distinguish source last execution start from last observed completion capture. Long summaries wrap or truncate visually with an accessible expansion, never raw HTML. Restricted/absent text reads “Restricted”/“Unavailable” while metrics remain visible if authorized.

Row detail: grouped query identity and database, plan-generation identities (no plan visualization), paired capture timestamps, measured seconds, valid/missing-baseline generation counts, eviction/recompile/invalid reasons, coverage, and authorized text availability. Distinguish observed work from lifetime counters; authorized raw inputs may support review. Baseline-only generations and idle queries appear in coverage diagnostics, not as active expensive-query rankings. Zero executions means undefined averages. Unknown database rows disappear under a specific database filter with an explanatory count, not invented attribution.

Empty state for valid zero work: “No observed completed query work in the covered intervals.” No complete pair: “Insufficient interval coverage.” New plans: “Baseline required for newly observed plans.” Truncated capture: “Ranking unavailable: query population capture was incomplete.” These states must remain distinct.

## 5. Blocking

Default is latest successful activity sample. Show captured timestamp and current/historical/frozen state prominently. Summary: blocked requests, unique captured roots, special/unknown owners and structural coverage. Root 51 blocking requests 62 and 63 is one root and two blocked requests.

Show expandable chain/tree with an equivalent accessible flat table. Session root appears once; request rows may have multiple requests within a session. Columns: session/request identity, database, status (including Sleeping), current wait type/duration ms, elapsed request ms, open transaction count/indicator, redacted summary and relationship state. Show first/last observed relationship timestamps in detail, clearly separate from Current wait duration.

Retain cross-database ancestors needed for the selected descendants and tag them “Context from another database.” If ancestor metadata is unauthorized, show a safe “Blocker details restricted” context node. “Blocker not captured” is a missing-positive-root placeholder. Negative owner IDs have named special-owner nodes and are excluded from the ordinary positive-ID blocker metric. Zero/null does not prove an ordinary blocker. Show cycle/depth/omitted-node markers without recursive hangs. Load additional roots/descendants using bounded continuations and preserve ancestor context.

Notice: “Relationships are sampled. Activity between samples may be missed.” Historical relationships do not establish a continuous episode or deadlock. Request-level waits can omit parallel worker details; disclose that limitation. No kill button, command execution or administrative action exists.

Empty state: “No positive-owner blocked requests observed at this capture.” If special owners exist, still show their separate rows. Collection failure keeps previous chain timestamp visible with failure/stale state rather than showing an empty successful sample.

## 6. Waits

Always label “Instance-level completed waits.” When a database is selected, show “Database filter does not apply to these instance counters.”

Include excluded waits toggle, explicit versioned exclusion list and explanatory rationale. Default total wait descending. Columns: wait type, total wait ms, signal wait ms, resource wait ms, task-count change, share of included wait time and coverage. Included-total denominator is across all included valid types, not just the visible page. A horizontal composition bar chart shares the table's filter/denominator; if only top types are graphed, include an Other category from remaining types. Avoid wall-time or CPU labels.

Selected-type history is optional navigation within existing history support, not a new diagnostics subsystem; retain request-level/current wait separation. Omit average wait per task. Concurrent waits can exceed elapsed wall time. Empty included denominator: “No observed completed waits”; share column is an em dash with text explanation, never a fabricated 0%/100%.

## 7. Database File I/O

Summary for selected databases/files: weighted read/write latency, summed operations and corresponding throughput over complete compatible coverage. Label partial scope when files are unavailable. Never average file latencies. Tables/charts share database context and selected range.

Columns: database, logical file name/identity, Data/Log type, reads/writes, read/write IOPS, read/write MiB/s, read/write latency ms/op, actual measured interval and coverage. File incarnation is available in detail so reused IDs are distinguishable. File detail includes read/write throughput and latency trends, raw paired counters where permitted, baseline/reset reasons and gaps. Physical path is absent by default; if separately approved it requires server permission and a matching authorized field.

No I/O: rate/throughput 0 for the direction, latency No I/O. One idle direction does not hide the active direction. Missing/recreated file: unavailable/baseline, not zero. File summary reports all selected eligible files, independent of table pagination. Chart/table captions label synthetic data and units.

## 8. Loading, empty, error and accessibility behavior

| State | UI treatment |
| --- | --- |
| First load | Section placeholder with “Loading monitoring snapshots,” aria-busy; no fabricated KPI values |
| Refresh in same context | Keep prior values/timestamps, label Refreshing, avoid focus/scroll jumps; prevent overlapping requests |
| Valid empty result | Section-specific no-observation message; retain context and measured coverage |
| Baseline/reset/gap | Text label/reason and null derived value; chart separator, no interpolation |
| Partial capture | Warning naming affected section/metric, successful sections usable with own times |
| Failed collection | Safe failure reason and last-attempt time immediately; retained values carry last-success timestamp and independent age |
| Unavailable/no prior sample | “No successful capture available” plus permitted safe reason; retry refresh cannot manufacture a sample |
| Delayed/stale | Persistent text status and capture age; retained values stay identifiable |
| Historical/frozen | Historical sample / Frozen synthetic scenario; no current-live freshness classification |
| Invalid timestamp | “Timestamp error”; no Fresh label; show source capture diagnostics safely |
| Transport/storage error | Section/page error, Retry and correlation ID; preserve old compatible data with error annotation |
| Expired auth/denied access | Existing sign-in/denied conventions; remove protected data on auth expiry; never insert login HTML into a chart |
| Invalid filters/work budget | Inline errors or narrower-range guidance; keep entered values, no silent fallback/clipping |
| Expired historical sample | “Sample is no longer retained”; offer available sample list or latest without silently switching |

Keyboard focus follows document order. Give every filter a label, selected navigation a text/ARIA state, sort buttons announced direction, expandable rows keyboard controls, and pagination meaningful labels. Provide chart data tables/captions and accessible tooltip/detail equivalents. Severity includes text/icons, never color alone. Use a polite live region for refresh completion/errors without announcing every second of the age timer. Keep user focus during background updates; announce changed context, not wholesale DOM replacement. Follow host accessibility/contrast standards once available.

## 9. Synthetic review checklist

Every fixture is explicitly synthetic, isolated from live history and versioned. Main fixture uses DEMO-SQL-01 / DemoSales, A=`2026-10-13T10:00:00Z`, B=`2026-10-13T10:00:15Z`, evaluation=`2026-10-13T10:00:20Z`. Unless overridden below, identities, epoch, timing run and complete capture coverage match. Values are fabricated, not SQL Server observations. Maintain full precision for calculation and format rates/latencies to two decimals; counts are integers, large units are explicit.

| Fixture / acceptance | Synthetic input | Expected visible result |
| --- | --- | --- |
| Main overview/query/file/wait vector (AC02–04, AC08) | PRD A/B cumulative inputs | 120 batches/s; Q1 20 executions, 2,000/100 CPU ms total/average, 4,000/200 elapsed ms, 30,000 logical pages; file reads 20 IOPS/2.00 MiB/s/5.00 ms; writes 4 IOPS/0.40 MiB/s/3.00 ms; waits 75%/25%, resource 5,400/1,800 ms |
| Main gauges (AC03, AC07) | B: 24 sessions, eight requests, root 51 Sleeping/open transaction, request children 62/63 with waits 12,000/8,000 ms; memory 4,194,304 KB; engine start seven days earlier | 24 sessions, 8 active requests, 2 blocked requests, 1 root, 4,096 MiB, 7 days; scenario evaluation Fresh at 5 seconds; all synthetic labels remain |
| Second query/ranking (AC05) | Q2 Δexecutions 10, ΔCPU 3,000,000 µs, Δelapsed 5,000,000 µs, Δreads 5,000; Q3 high lifetime CPU but Δexecutions/work 0 | Q2 precedes Q1 by total CPU (3,000>2,000 ms); Q1 precedes Q2 by logical reads (30,000>5,000); Q3 excluded from active ranking, average null |
| Weighted files (AC09) | Separate fixture: file F1 10 reads/20 stall ms; F2 90 reads/900 stall ms, same interval | Summary 9.20 ms/read, file values 2 and 10; not 6 |
| No activity (AC10) | All valid deltas 0; no positive blocked requests | Zero throughput/executions; No I/O latency; no query averages; no wait shares; no observed completed work/waits message |
| Unavailable/restricted text | Valid Q1 counters, no text capture; second variant text permission absent | Metrics available, summary Unavailable/Restricted; no raw SQL or HTML |
| Unknown database (AC06) | Q2 valid deltas with unknown context | Visible Unknown in All databases; excluded with explanation for DemoSales; no inferred database |
| Wait policy toggle (AC08) | Main waits plus SLEEP_TASK Δwait 8,000/Δsignal 0 | Default shares 75%/25%; include excluded yields 37.5%/12.5%/50%; policy demo-v1 shown, raw counters unchanged |
| Permission failure / partial capture (AC14) | Wait capture denied, files and activity successful at B; prior wait success optional | Waits unavailable or retained with prior time and failure; file/activity metrics usable, overall partial; no synthetic fallback from live |
| Timeout | Collection deadline expires before queries finish, other sections persist | Query unavailable/partial, timeout last-attempt status, prior success time unchanged; no overlapping cycle |
| Stale/boundaries (AC13) | Evaluate success at ages 30,31,60,61 seconds; failed attempt before evaluation | Fresh/Delayed/Delayed/Stale respectively; failed attempt visible independently. Frozen fixture calls this scenario evaluation |
| Restart/decrease (AC11) | Batch 101,800→50; second variant changed epoch with increasing counter | Reset/baseline null rate, no clamp. Next same-epoch/timing pair e.g. 50→350/15 s gives 20/s |
| Wait clear | Known clear even if new totals exceed old; file counter continuity unchanged | Wait reset/baseline; valid file interval retained; no claim to detect every unobserved clear |
| Plan eviction/recompile (AC06) | Q1 disappears, generation Q1b appears with lifetime counters | No invented old/new delta; new baseline, cache coverage warning. Next Q1b pair may contribute |
| Session reuse | Session 51 appears with a new login/start identity | New node identity; no continuous relationship/episode bound across reuse |
| File recreation | Same database/file numeric IDs, changed incarnation metadata | New file baseline, no old/new rate/latency |
| Missing root | Request 62 owner +51, session 51 absent | “Blocker not captured,” request still counted as positive-ID blocked |
| Special owner | Request owner -2; -3/-4/-5 variants | Separate special/unresolved-owner labels; no fake sessions, no addition to positive-ID KPI |
| Cross-database root | DemoAdmin sleeping root blocks DemoSales descendants | Root preserved as context with label, or restricted placeholder if unauthorized |
| Cycle / bounded tree | A→B→A; independent fixture exceeding 500-node budget | Cycle marker, finite traversal; bounded structure with continuation/truncation and full-sample KPI |
| Delay/gap (AC12) | Batch Δ1,800 with captures 20 seconds apart / 45 seconds apart | 90/s / visible gap and new baseline. Next compatible capture resumes |
| Narrow range | Range contains no entire compatible interval | Insufficient interval coverage; no prorated rate |
| Invalid work/waits | Positive query CPU with Δexecutions 0; signal wait delta > total | Affected invalid metrics with reason, never negative resource or invented average |
| Retry/history (AC15) | Same sample publication ID retried; historical B selected | One sample, exact identities/source/times; Historical sample and no auto-advance |
| Timestamp error | Negative sample age, or engine start normalization ambiguous | Timestamp error / uptime unavailable, never Fresh or negative uptime |
| Access and text safety (AC16) | Unauthorized instance/detail; expired auth; captured summary containing markup | Denied/expired response behavior; no field leakage; markup shown as text |
| UI refresh/accessibility (AC17) | Slow AJAX then filter change, paused view, keyboard navigation | Old response ignored, one refresh group at a time, age increases while paused, keyboard access and table alternatives |

Workload performance/retention acceptance (AC18) is a later measured release check, not something synthetic screen fixtures prove. AC01 remains unresolved because application source is absent. The fixture list defines later review inputs; no implementation tests are generated here.

## 10. Scope and open decisions

Required UI scope is exactly the five sections and persisted-history views in the PRD. The initial demonstration is synthetic; live collection comes later after deployment/permissions review. Existing application layout, asset versions, role mappings, approved wait list, text policy, display timezone preference, cadence/retention/work bounds and draft PRD approval remain unresolved.

Optional only with separate agreement: process CPU utilization with verified source, privileged raw text/physical paths, persisted rollups, exports. No session termination, query execution, automatic tuning/configuration, alert delivery, execution-plan analysis, deadlock capture or Query Store enablement is designed into this release.
