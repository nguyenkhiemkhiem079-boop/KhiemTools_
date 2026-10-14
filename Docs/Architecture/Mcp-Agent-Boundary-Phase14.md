# Phase 14 — MCP / Agent Boundary

## Implemented boundary

`McpJsonRpcAdapter` implements the stateless MCP `2026-07-28` JSON-RPC boundary for the
allow-listed Phase 13 capability registry. It supports `server/discover`, `tools/list`, and
`tools/call`; validates the per-request protocol version and client-capabilities metadata; and
returns structured and text tool results. `McpStdioTransport` provides bounded newline-delimited
stdio framing. The transport host owns process lifetime and must not run the blocking loop on
Revit's UI thread.

Only `sheet-export.naming-preview` is exposed. It is classified `PREVIEW_ONLY`, annotated read-only,
requires `dryRun=true`, declares `unitSystem=NotApplicable`, and calls the same canonical naming
service as the UI/workflow. There are no mutating MCP tools, model/context dumps, file operations,
shell/PowerShell execution, dynamic assembly loading, reflection dispatch, or network listener.
The internal API also rejects future mutating requests unless a separate `ExplicitExecute=true`
flag is supplied; this does not itself constitute user consent or authorization for a future tool.

Input is capped at 64 KiB per message and 256 characters per textual field. Extra JSON fields,
duplicate properties, JSON-RPC batches, unknown methods/tools, unsupported versions, invalid units,
and non-dry-run preview calls are rejected. Canonical naming regex evaluation is bounded to 100 ms.

## Verification boundary

The .NET 8 contract harness executes discovery, listing, typed tool calls through the production
API and canonical naming planner, invalid/unknown input, unsupported versions, message limits,
duplicate-property and batch rejection, regex timeout, and stdio framing. Revit 2024/2025 builds
compile the adapter for the add-in targets. This is code/protocol acceptance only: no MCP client
was connected, no MCP subprocess was published/configured, and no Revit-host runtime scenario was
executed. `MCP_RUNTIME_INTEGRATION` and Revit-host acceptance remain `DEFERRED`.
