# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All commands run from `src/` (where `Corvida.sln` lives).

```bash
dotnet build                        # build all projects
dotnet run --project Corvida/Corvida/Corvida.csproj     # run the desktop app
dotnet run --project Corvida.Api/Corvida.Api.csproj     # run the REST API
dotnet run --project Corvida.Mcp/Corvida.Mcp.csproj     # run the MCP server (stdio)
dotnet run --project Corvida.AppHost/Corvida.AppHost.csproj  # run full stack via Aspire
dotnet test                         # run all xUnit tests
dotnet test --filter "FullyQualifiedName~SomeTest"      # run a single test
dotnet clean                        # clean build artifacts
```

**Full stack via Docker** (from repo root):
```bash
docker compose up --build           # API + PostgreSQL; API exposed on :5000
```

## Solution Structure

Seven projects in `src/Corvida.sln`:

| Project | Type | Role |
|---|---|---|
| `Corvida.Core` | Class library | Shared models + entire service layer |
| `Corvida` | Desktop app (WinExe) | Avalonia 12 UI; references Core |
| `Corvida.Api` | ASP.NET Core 10 | REST API with PostgreSQL (EF Core + Npgsql) |
| `Corvida.Mcp` | Console (stdio) | MCP server; exposes Kanban tools to LLMs |
| `Corvida.AppHost` | .NET Aspire AppHost | Orchestrates `Corvida.Api` + PostgreSQL for local dev |
| `Corvida.ServiceDefaults` | Aspire shared lib | OpenTelemetry, resilience, service-discovery defaults for `Corvida.Api` |
| `Corvida.Core.Tests` | xUnit test project | Unit tests for `Corvida.Core` |

Both `Corvida` and `Corvida.Mcp` reference `Corvida.Core` and share the same service layer. `Corvida.Api` also references `Corvida.Core` and `Corvida.ServiceDefaults`.

## Data Model

Three entities form a strict hierarchy: `Board → KanbanGroup → KanbanTask`.

- **Board** — has `Id`, `Name`, and a list of `KanbanGroup`
- **KanbanGroup** — a column (e.g. "To-Do"); holds a list of task IDs (`TaskIds`)
- **KanbanTask** — carries `Title`, `Description` (markdown), `Priority`, `Created`, optional `PlannedStart`/`PlannedEnd`, plus `BoardId` and `GroupId` back-references

`AppSettings` (also in Core) holds `DataPath`, `StorageMode`, and optional `ServerUrl`.

## Storage

Two modes controlled by `AppSettings.StorageMode`:

**LocalFolder** — files in `~/CorvidaData/`:
```
boards/{boardId}/board.json          ← Board + KanbanGroups as JSON
boards/{boardId}/tasks/{taskId}.md   ← KanbanTask as Markdown with YAML frontmatter
```

**ServerHosted** — delegates to `Corvida.Api` over HTTP (`AppSettings.ServerUrl`, default `http://localhost:5000`).

App config lives at `{AppData}/Corvida/settings.json`.

## Service Layer (Corvida.Core)

All service code lives in `Corvida.Core/Services/` with namespace `Corvida.Services`. Models use namespace `Corvida.Models`.

### Storage-aware adapter pattern

`StorageAwareBoardService` and `StorageAwareTaskService` implement the service interfaces and delegate to either the local or HTTP concrete implementation based on `StorageMode`. This is the class registered as `IBoardService`/`ITaskService` in both the desktop app and the MCP server.

| Interface | Local impl | HTTP impl |
|---|---|---|
| `IBoardService` | `BoardService` | `HttpBoardService` |
| `ITaskService` | `TaskService` | `HttpTaskService` |
| `ISettingsService` | `SettingsService` | _(none)_ |

`MarkdownSerializer` (public static) handles YAML frontmatter + markdown body serialization for `KanbanTask` files.

### DI registration (same pattern in both Corvida and Corvida.Mcp)

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<BoardService>();
services.AddSingleton<HttpBoardService>();
services.AddSingleton<IBoardService, StorageAwareBoardService>();
services.AddSingleton<TaskService>();
services.AddSingleton<HttpTaskService>();
services.AddSingleton<ITaskService, StorageAwareTaskService>();
services.AddHttpClient("CorvidaApi");
```

## Desktop App (Corvida)

- **Avalonia 12** with **Fluent** theme and **SukiUI 7** component library
- **Material.Icons.Avalonia** for icons; **Markdown.Avalonia** for rendering task descriptions
- `ViewLocator.cs` auto-maps ViewModel types → View types
- `MainWindowViewModel` owns the page stack and theme toggle (light/dark, persisted in settings)
- `BoardsPageViewModel` drives navigation between `BoardsListViewModel`, `BoardEditorViewModel`, and `TaskEditorViewModel`
- `DialogService` shows `InputDialog`, `ConfirmDialog`, and `PickerDialog` modals (desktop-only; not in Core)
- `ExportService` exports all boards/tasks from the HTTP backend to local disk format

## REST API (Corvida.Api)

ASP.NET Core 10 with EF Core + Npgsql. Connection string: `DefaultConnection` in config.

Endpoints:
- `GET/POST /api/boards` — list all / create board
- `GET/PUT/DELETE /api/boards/{id}` — get / update / delete board
- `PUT /api/boards/{boardId}/tasks/{taskId}` — upsert task
- `GET/DELETE /api/boards/{boardId}/tasks/{taskId}` — get / delete task

Board groups are stored as JSONB in `BoardEntity.GroupsJson`. Tasks are stored in a `TaskEntity` table with a cascade-delete FK to boards.

## MCP Server (Corvida.Mcp)

stdio transport; uses `ModelContextProtocol` 1.3.0. Tools are discovered automatically via `WithToolsFromAssembly()` from classes decorated with `[McpServerToolType]`.

`SettingsLoader` (hosted service) loads `settings.json` before any tool calls are served.

**13 tools across two classes in `Tools/`:**

`BoardTools` — `list_boards`, `get_board`, `create_board`, `delete_board`, `add_group`, `rename_group`, `delete_group`

`TaskTools` — `list_tasks`, `get_task`, `create_task`, `update_task`, `delete_task`, `move_task`

To connect from Claude Desktop, add to `~/.config/claude/claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "corvida": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/Corvida.Mcp/Corvida.Mcp.csproj"]
    }
  }
}
```
