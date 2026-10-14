# Documentation stage

Status: requirements/design, stage records and verified backend development setup saved; MVC/user/operations documentation pending. Updated 13 October 2026.

The subsequent backend milestone added source code and [backend-milestone.md](backend-milestone.md), with local setup, verification and remaining work. The separate conversation record is in [../.session/backend-development.md](../.session/backend-development.md).

The next authorized stage added a new .NET 9 MVC application and local browser preview. Current startup/stack/evidence is in [local-application.md](local-application.md), and the visible conversation is in [../.session/frontend-development.md](../.session/frontend-development.md).

## Separate stage records

| File | Current role/status |
| --- | --- |
| [requirements.md](requirements.md) | Requirements summary and draft/open decisions |
| [architecture-design.md](architecture-design.md) | Completed design-conversation record and output links |
| [work-items.md](work-items.md) | Proposed ordered backlog; development not started |
| [backend-development.md](backend-development.md) | Partial backend implementation record; MVC integration pending |
| [frontend-development.md](frontend-development.md) | Pending frontend stage brief and future conversation record |
| [testing.md](testing.md) | Documentation/backend SQL and calculation checks; MVC/frontend/live validation pending |
| [code-review.md](code-review.md) | Pending review stage brief; no code approval |
| [documentation.md](documentation.md) | This inventory and documentation-stage record |

These files separate the available conversation's stage outcomes and future handoffs. They are summaries/records, not verbatim exports of other chats; no unseen conversations have been reconstructed. Future stage chats should append their actual requests, decisions, outputs and verification to the corresponding file.

## Authoritative source and detailed design

- [PRD.md](PRD.md): existing draft product requirements and AC01–AC18.
- [architecture.md](architecture.md): existing inspection, component mappings, AJAX contracts and operational boundaries.
- [data-model.md](data-model.md): existing logical schema, retention, identities and calculations.
- [ui-design.md](ui-design.md): existing UI specification and synthetic review scenarios.

Keep these detailed documents as the sources of truth; stage summaries link to them instead of duplicating the full specification. No original document is renamed or removed.

## Future documentation work

After implementation, document confirmed prerequisites/versions, deployment/configuration and secret handling, permission mapping, collector hosting/recovery, retention/sizing, supported workload, dashboard interpretation and verified troubleshooting behavior. Describe only implemented capabilities and actual validation results.

Preserve synthetic labels in examples and future exports. Record remaining limitations and distinguish optional features from shipped behavior. Update README setup instructions only once actual application setup is known.
