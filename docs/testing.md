# Testing stage

Status: backend calculation/SQL integration checks pass; MVC/HTTP/frontend/live validation unrun. Updated 13 October 2026.

Follow-up: local MVC/HTTP/UI behavior now passes 22 installed-Chrome checks in addition to 73 backend assertions and SQL checks. The full solution builds without warnings/errors. Full accessibility/live/load validation remains unrun. See [local-application.md](local-application.md).

## Backend milestone evidence

Release build of the new backend/verification solution passes with zero warnings/errors. The verifier passes 29 checks without SQL or 73 total with the dedicated SQLDashboardDev LocalDB database. The SQL integrity script verifies migration versions, repeatable fixture counts, cross-partition FKs, duplicate rejection, transactional publication/idempotent retry and lease expiry. See [backend-milestone.md](backend-milestone.md) for commands, versions, expected outputs and limitations.

No original MVC application exists here, so existing-app builds, concrete HTTP JSON/authentication tests, UI/browser/accessibility checks and production workload/retention measurements remain unrun. These results do not complete full release acceptance.

## Checks actually completed

During the design conversation, the three design documents were checked for readable UTF-8 text and valid local documentation links. Architecture mappings include AC01–AC18. The illustrative JSON metric specimen parses successfully. These are documentation checks, not application acceptance tests.

## Planned validation

Use acceptance criteria in [PRD.md](PRD.md), calculation vectors in [data-model.md](data-model.md), synthetic scenarios in [ui-design.md](ui-design.md) and WI09/WI11 in [work-items.md](work-items.md).

| Area | Required evidence when implemented |
| --- | --- |
| Calculations | PRD synthetic outputs, weighted latency, zero denominators, full precision and active interval ranking |
| Continuity | Engine/timing restarts, manual wait clear, eviction/recompile, file/session identity reuse and missing samples |
| Collection/history | Partial/denied/timeout sections, last attempt versus last success, idempotent publication, historical selection and explicit coverage |
| API/access | Filter/sort/range limits, bounded result/work budgets, unauthorized scoped IDs, auth expiry, safe text and safe errors |
| UI | Synthetic labelling, stale/historical states, pause/resume, no overlapping refresh, ignored obsolete responses, keyboard access and chart-table equivalents |
| Operations | Declared workload, ten concurrent viewers, endpoint p95 target, collection deadline/cadence, independent scheduling and 30-day retention capacity |

AC01 cannot pass until application source is available. Synthetic screen fixtures cannot establish live collection performance or storage capacity.

## Record for a future testing chat

Record environment/version, build or commit, fixture/workload, commands or review steps, observed results, failed criteria, evidence paths and remaining limitations. Distinguish automated checks from manual review and planned checks.

The original design stage generated no tests. The subsequent backend milestone added and ran `tests/Dashboard.Verification` and `sql/verification/VerifyDevelopmentSchema.sql` against local synthetic data only.
