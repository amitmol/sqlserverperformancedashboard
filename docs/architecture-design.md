# Architecture and design stage

Status: design documents saved; application integration unverified. Updated 13 October 2026.

## Conversation record

The user requested inspection of `docs/PRD.md` and the existing project, identification of the actual stack/conventions, requirement-to-component mappings, snapshot storage, calculation rules, JSON contracts and dashboard UI design. The user explicitly requested documentation only, without implementation code.

Inspection found only README.md and the draft PRD in the working tree, with no application source. Therefore no actual .NET/MVC version, data-access framework, authentication mechanism or UI convention could be established. Proposed integration names are marked provisional rather than presented as existing components.

## Saved design outputs

| Document | Content |
| --- | --- |
| [architecture.md](architecture.md) | Inspection evidence; controllers/actions, services, repositories, view models, Razor and JavaScript mappings; collection boundaries; validated bounded AJAX contracts; access and operational requirements |
| [data-model.md](data-model.md) | Snapshot tables, identities, keys/indexes, retention, cumulative counters versus gauges, KPI formulas, comparison/reset/gap rules and UTC handling |
| [ui-design.md](ui-design.md) | Dashboard sections, shared filters, charts/tables, loading/empty/error states, accessibility and explicitly synthetic review fixtures |

## Verification recorded

Local documentation links resolve. AC01–AC18 are mapped in the architecture document. The illustrative JSON metric specimen parses successfully. No implementation code, executable DDL or application tests were generated.

AC01 remains unresolved because application source is absent. Performance, retention capacity and implementation acceptance are not proven by documentation checks.

## Handoff

Use [work-items.md](work-items.md) to organize implementation after the missing source and deployment details are available. Keep future backend/frontend/testing/review stage records separate; update these design documents when approved decisions change.
