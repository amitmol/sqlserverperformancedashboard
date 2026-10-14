# Project agents

Eight project-scoped Codex agents correspond to the project's stage records:

| Agent name | Definition | Main responsibility |
| --- | --- | --- |
| requirements | [requirements.toml](agents/requirements.toml) | PRD scope and acceptance criteria |
| architecture_design | [architecture_design.toml](agents/architecture_design.toml) | Component, schema, calculation and UI design |
| work_items | [work_items.toml](agents/work_items.toml) | Evidence-based WI01–WI12 planning/status |
| backend_development | [backend_development.toml](agents/backend_development.toml) | C#, SQL storage, repositories, services and MVC JSON |
| frontend_development | [frontend_development.toml](agents/frontend_development.toml) | Razor, jQuery, dashboard interactions and visual states |
| testing | [testing.toml](agents/testing.toml) | Calculation, SQL, HTTP and Chrome verification |
| code_review | [code_review.toml](agents/code_review.toml) | Independent review; no application edits |
| documentation | [documentation.toml](agents/documentation.toml) | Setup, evidence and truthful stage/session records |

These standalone TOML definitions use name, description and developer_instructions, following [official OpenAI documentation for custom agents](https://learn.chatgpt.com/docs/agent-configuration/subagents#custom-agents). They define reusable roles; creating the files does not start agents or complete their work.

No model, reasoning, permission, credential or MCP-server override is included. Agents inherit the parent settings. Read-only behavior for the reviewer is an instruction, not an independent permission sandbox.

## Use

Open this repository in a client supporting project custom agents. Start a fresh session after adding the definitions if the client has already loaded project configuration. Ask for the relevant names explicitly; for example:

> Have work_items inspect WI07, then assign the scoped implementation to backend_development. Have testing verify the changes and code_review review the diff. Use documentation to update setup and evidence.

The parent coordinates dependencies, gives each agent a bounded assignment and file ownership, and consolidates results. Let independent work run in parallel only when explicitly requested or instructed by the active project workflow. Shared files such as Program.cs, project files and docs/work-items.md need a single owner at a time.

The primary source for current implementation is [local-application.md](../docs/local-application.md), alongside actual code. The user authorized a new local application; older missing-host statements are historical. Development remains local/synthetic by default, with no production connections or unrequested administrative operations.

Stage outputs belong in docs/; available stage conversation records belong in .session/. Definitions do not authorize invented transcripts, historical results presented as newly run checks, or status changes without evidence.

## Validation

Validate TOML parsing, required fields, unique names, filename/name agreement and referenced paths. Runtime agent discovery depends on the installed client version; a syntax check alone does not prove that an agent was spawned.
