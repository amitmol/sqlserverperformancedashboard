# Frontend development stage

Status: local Razor/jQuery dashboard implemented and tested after authorization for a new application. Updated 13 October 2026.

See [local-application.md](local-application.md) for source paths, startup, 22 Chrome/HTTP checks and remaining UI work. The brief below describes the initial plan; full accessibility and all detail/history features remain pending.

## Inputs and prerequisites

Use [ui-design.md](ui-design.md), AJAX contracts/component mappings in [architecture.md](architecture.md), and WI08 in [work-items.md](work-items.md). Inspect the real shared Razor layout, navigation, styles, bundles, jQuery version, chart/table tools and accessibility conventions before implementation.

## Planned scope

- Five sections: Overview, Expensive Queries, Blocking, Waits and Database File I/O.
- Shared instance/database/mode/range/timezone context, exact historical sample selection and applicable-filter-preserving drilldowns.
- Clearly labelled synthetic cards, tables, charts and details; no synthetic fallback after live failures.
- Bounded jQuery AJAX refresh groups, obsolete-response protection, pause/resume, pagination/sorting and safe text rendering.
- Per-section timestamps/coverage, loading/empty/error/partial/reset/gap states, chart gaps and tabular equivalents.
- Keyboard navigation, announced state changes and severity conveyed through text as well as color.

Do not add a frontend framework or prescribe a new visual brand. Do not add optional CPU/exports/privileged text or out-of-scope administrative actions without a scope decision.

## Record for a future development chat

Append the request/date, confirmed host UI conventions, changed Razor/JavaScript/style paths, completed work-item IDs, actual browser/accessibility checks, unresolved issues and handoff when work occurs.

No Razor, JavaScript, CSS or chart implementation was created during the design conversation.
