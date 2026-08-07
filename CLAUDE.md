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
docker compose up --build           # API + PostgreSQL; API exposed on :5083
```

## Solution Structure

Eight projects in `src/Corvida.sln`:

| Project | Type | Role |
|---|---|---|
| `Corvida.Core` | Class library | Shared models + entire service layer |
| `Corvida` | Desktop app (WinExe) | Avalonia 12 UI; references Core |
| `Corvida.Api` | ASP.NET Core 10 | REST API with PostgreSQL (EF Core + Npgsql), SignalR realtime hub |
| `Corvida.Mcp` | Console (stdio) | MCP server; exposes Kanban tools to LLMs |
| `Corvida.AppHost` | .NET Aspire AppHost | Orchestrates `Corvida.Api` + PostgreSQL for local dev |
| `Corvida.ServiceDefaults` | Aspire shared lib | OpenTelemetry, resilience, service-discovery defaults for `Corvida.Api` |
| `Corvida.Core.Tests` | xUnit test project | Unit tests for `Corvida.Core` |
| `Corvida.Api.Tests` | xUnit test project | Integration tests for `Corvida.Api` (EF Core migrations, board/task/agent endpoints) |

Both `Corvida` and `Corvida.Mcp` reference `Corvida.Core` and share the same service layer. `Corvida.Api` also references `Corvida.Core` and `Corvida.ServiceDefaults`.

## Data Model

Four entities, in `Corvida.Core/Models`:

- **Board** — has `Id`, `Name`, a list of `KanbanGroup`, `IsArchived`, `AgentIds` (board membership), and `CellOrders` (persisted per-cell task ordering for the swimlane grid)
- **KanbanGroup** — a column (e.g. "To-Do"); holds a list of task IDs (`TaskIds`)
- **KanbanTask** — carries `Title`, `Description` (markdown), `Priority`, `Created`, optional `PlannedStart`/`PlannedEnd`, `AssignedAgentId`, plus `BoardId` and `GroupId` back-references
- **Agent** — a board member (human or AI); carries `Id`, `Name`, `Personality` (markdown), `Color`, optional `AvatarDataUri`. Boards reference agents by `AgentIds`; tasks reference an assignee by `AssignedAgentId`. Agents are managed from the desktop app's **Agents** page and are independent of any single board (assign the same agent across multiple boards).

`AppSettings` (also in Core) holds `DataPath`, `StorageMode`, and optional `ServerUrl`.

## Storage

Two modes controlled by `AppSettings.StorageMode`:

**LocalFolder** — files in `~/CorvidaData/`:
```
boards/{boardId}/board.json          ← Board + KanbanGroups as JSON
boards/{boardId}/tasks/{taskId}.md   ← KanbanTask as Markdown with YAML frontmatter
agents/{agentId}.md                  ← Agent as Markdown with YAML frontmatter
```

**ServerHosted** — delegates to `Corvida.Api` over HTTP (`AppSettings.ServerUrl`, default `http://localhost:5083`).

App config lives at `{AppData}/Corvida/settings.json`.

## Service Layer (Corvida.Core)

All service code lives in `Corvida.Core/Services/` with namespace `Corvida.Services`. Models use namespace `Corvida.Models`.

### Storage-aware adapter pattern

`StorageAwareBoardService`, `StorageAwareTaskService`, and `StorageAwareAgentService` implement the service interfaces and delegate to either the local or HTTP concrete implementation based on `StorageMode`. These are the classes registered as `IBoardService`/`ITaskService`/`IAgentService` in both the desktop app and the MCP server.

| Interface | Local impl | HTTP impl |
|---|---|---|
| `IBoardService` | `BoardService` | `HttpBoardService` |
| `ITaskService` | `TaskService` | `HttpTaskService` |
| `IAgentService` | `AgentService` | `HttpAgentService` |
| `ISettingsService` | `SettingsService` | _(none)_ |

`MarkdownSerializer` / `AgentMarkdownSerializer` (public static) handle YAML frontmatter + markdown body serialization for `KanbanTask` / `Agent` files respectively.

### DI registration (same pattern in both Corvida and Corvida.Mcp)

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<BoardService>();
services.AddSingleton<HttpBoardService>();
services.AddSingleton<IBoardService, StorageAwareBoardService>();
services.AddSingleton<TaskService>();
services.AddSingleton<HttpTaskService>();
services.AddSingleton<ITaskService, StorageAwareTaskService>();
services.AddSingleton<AgentService>();
services.AddSingleton<HttpAgentService>();
services.AddSingleton<IAgentService, StorageAwareAgentService>();
services.AddHttpClient("CorvidaApi");
```

The desktop app additionally registers `IRealtimeClient` (`SignalRRealtimeClient`), which connects to `Corvida.Api`'s `/hubs/kanban` SignalR hub (only relevant in `ServerHosted` mode) and pushes `BoardChanged`/`BoardDeleted`/`TaskChanged`/`TaskDeleted`/`AgentChanged`/`AgentDeleted` events into `WeakReferenceMessenger` so open pages update live when another client changes shared data.

## Desktop App (Corvida)

- **Avalonia 12** with **Fluent** theme and **SukiUI 7** component library
- **Material.Icons.Avalonia** for icons; **Markdown.Avalonia** for rendering task/agent/skill markdown content
- `ViewLocator.cs` auto-maps ViewModel types → View types
- `MainWindowViewModel` owns the page stack and theme toggle (light/dark, persisted in settings)

### Menu pages

Every top-level menu page implements `PageBase` (`MenuTitle`, `Icon`, `DisplayOrder`) and is registered as `services.AddTransient<PageBase, TPage>()` in `App.axaml.cs`; `MainWindowViewModel` sorts the injected `IEnumerable<PageBase>` by `DisplayOrder` and the side menu binds to it directly, so a new page needs no other wiring. Current pages, in `DisplayOrder`:

| Order | Page | Drives |
|---|---|---|
| 0 | `BoardsPageViewModel` | `BoardsListViewModel`, `BoardEditorViewModel`, `TaskEditorViewModel` |
| 10 | `AgentsPageViewModel` | `AgentsListViewModel`, `AgentEditorViewModel` — CRUD over `Agent` |
| 20 | `SkillsPageViewModel` | `SkillsListViewModel`, `SkillEditorViewModel` — CRUD over `Skill` (see [Skills](#skills-corvida)) |
| 50 | `ArchivedBoardsViewModel` | read-only list of archived boards |
| 99 | `SettingsViewModel` | storage mode, data path, skill/agent/MCP installers, export |

Each list/editor pair follows the same shape: the list `ViewModel` takes an `Action<T> onEdit` callback into its constructor and the owning page's nav-stack (`NavigateTo`/`GoBack`) wires it to push/pop the editor; the editor takes `(entity, service, Action<T> onSaved, Action onBack)` and exposes a `[RelayCommand] Save()` / `[RelayCommand] GoBack()`. Copy `AgentsPageViewModel`/`AgentsListViewModel`/`AgentEditorViewModel` (or the newer `Skills*` equivalents) as the template for a new CRUD page rather than the heavier `BoardEditorViewModel`.

- `DialogService` shows `InputDialog`, `ConfirmDialog`, and `PickerDialog` modals (desktop-only; not in Core)
- `ExportService` exports all boards/tasks from the HTTP backend to local disk format
- **Design-time XAML data**: some editor ViewModels expose a `public static T DesignInstance { get; }` factory that builds sample data with no-op service/callback stubs (services are never invoked by the previewer), wired up via `d:DataContext="{x:Static vm:TViewModel.DesignInstance}"` on the view's root element — see `BoardEditorViewModel.DesignInstance` / `BoardEditorView.axaml`. Follow this pattern when a new editor view needs populated data in the Avalonia previewer.

## REST API (Corvida.Api)

ASP.NET Core 10 with EF Core + Npgsql. Connection string: `DefaultConnection` in config.

Endpoints:
- `GET/POST /api/boards` — list active boards / create board
- `GET /api/boards/archived` — list archived boards
- `GET/PUT/DELETE /api/boards/{id}` — get / update / delete board
- `POST /api/boards/{id}/archive` / `POST /api/boards/{id}/restore` — archive (read-only) / restore a board
- `GET/PUT/DELETE /api/boards/{boardId}/tasks/{taskId}` — get / upsert / delete task
- `GET/POST /api/agents` — list all / create agent
- `GET/PUT/DELETE /api/agents/{id}` — get / update / delete agent

Board groups are stored as JSONB in `BoardEntity.GroupsJson`. Tasks are stored in a `TaskEntity` table with a cascade-delete FK to boards. Agents are stored in an `AgentEntity` table, independent of any board.

**Realtime**: `KanbanHub` (SignalR, mapped at `/hubs/kanban`) broadcasts `BoardChanged`/`BoardDeleted`/`TaskChanged`/`TaskDeleted`/`AgentChanged`/`AgentDeleted` to connected clients whenever an endpoint mutates data, so every endpoint handler that changes state also calls the corresponding hub method via `IHubContext<KanbanHub, IKanbanHubClient>`.

## MCP Server (Corvida.Mcp)

stdio transport; uses `ModelContextProtocol` 1.3.0. Tools are discovered automatically via `WithToolsFromAssembly()` from classes decorated with `[McpServerToolType]`.

`SettingsLoader` (hosted service) loads `settings.json` before any tool calls are served.

**22 tools across three classes in `Tools/`:**

`BoardTools` — `list_boards`, `get_board`, `create_board`, `delete_board`, `add_group`, `rename_group`, `delete_group`, `add_board_member`, `remove_board_member`, `reorder_board_members`

`TaskTools` — `list_tasks`, `get_task`, `create_task`, `update_task`, `delete_task`, `move_task`, `assign_agent`

`AgentTools` — `list_agents`, `get_agent`, `create_agent`, `update_agent`, `delete_agent`

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

## Skills (Corvida)

The desktop app can install Claude-Code-style skills (a `SKILL.md` with YAML frontmatter + Markdown body, used by e.g. `/corvida-plan` and `/corvida-implement` in this very repo) into Claude Code, OpenCode, or Hermes' skills folders, and lets you create/edit/delete them from a **Skills** menu page instead of hand-editing files.

- **Bundled skills** ship under `Corvida/Corvida/Skills/<id>/` (copied to the build output via `Corvida.csproj`'s `<None Include="Skills\**" .../>`) — currently `corvida-implement` and `corvida-plan`. Each skill folder has `SKILL.md` (frontmatter `name`/`description` + body) and `agents/openai.yaml` (`interface.display_name`/`interface.short_description`, consumed by non-Claude agent surfaces).
- **Editable copy**: `SkillPaths.UserSkillsRoot` (`{AppData}/Corvida/skills/`) is a user-writable folder seeded once from the bundled skills via `SkillPaths.EnsureSeeded()` (called at app startup in `App.axaml.cs`). All editing/install operations read from and write to this folder, never the bundled/read-only one — a packaged (deb) install can't write next to its own binaries, and edits there would be lost on the next build anyway.
- `ISkillService`/`SkillService` — file-based CRUD (`GetSkillsAsync`/`CreateSkillAsync`/`SaveSkillAsync`/`DeleteSkillAsync`) over `UserSkillsRoot`, desktop-only (not in `Corvida.Core` — skills aren't synced via the API/MCP like boards/tasks/agents are). `SkillMarkdownSerializer` / `SkillAgentYamlSerializer` hand-parse the two file formats, same style as `MarkdownSerializer` in Core.
- `ISkillInstallerService`/`SkillInstallerService` — discovers skills dynamically from `UserSkillsRoot` (no hardcoded list) and copies both `SKILL.md` and `agents/openai.yaml` into a target root. Driven from the **Settings** page's "Install Skills" section, with per-tool target paths (`~/.claude/skills`, `~/.config/opencode/skills`, `~/.hermes/skills`).

## Agent seeding & export (Corvida)

Separate from the Skills feature, but the same file-based-installer shape — this exports Corvida's own **Agent** entities (the Kanban board-member records) as Claude-Code-style subagent `.md` files, and seeds a starter set of them at first run:

- **Bundled agent templates** ship under `Corvida/Corvida/Agents/*.md` (copied to the build output via `Corvida.csproj`'s `<None Include="Agents\**" .../>`) — currently `architect`, `code-reviewer`, `developer`, `orchestrator`, `security-reviewer`. Each file uses the same frontmatter+body shape as a Claude Code subagent (`name`/`description` + Markdown body).
- `BuiltInAgentSeeder.EnsureSeededAsync` (static, called fire-and-forget from `App.axaml.cs` at startup) reads those templates via `SkillMarkdownSerializer.Parse` and creates one `Agent` per template on first run — with a deterministic `builtin-{slug}` ID (so re-runs don't duplicate them) and a per-slug accent color — via whatever `IAgentService` is currently active (local or HTTP).
- `IAgentInstallerService`/`AgentInstallerService` does the reverse: it serializes every *existing* `Agent` (built-in or user-created) via `SkillMarkdownSerializer.Serialize` and writes one `{slug}.md` per agent into a target root, so any Corvida Agent — not just the bundled templates — can be installed as a real Claude Code/OpenCode/Hermes subagent. Driven from the **Settings** page's "Install Agents" section, with the same per-tool target paths as Skills but under an `agents/` (not `skills/`) folder.

## MCP self-registration (Corvida)

`IMcpInstallerService`/`McpInstallerService` lets the desktop app register the bundled `Corvida.Mcp` server with an external tool's own config, from the **Settings** page's "Install MCP Server" section (needs a path to `Corvida.Mcp.csproj` plus a per-tool config path, e.g. `~/.claude.json`, `~/.config/opencode/opencode.json`, `~/.hermes/config.yaml`):

- Each target (`McpTarget.ClaudeCode`/`OpenCode`/`Hermes`) merges a single `"corvida"` entry into that tool's real config file — `mcpServers`/`mcp` JSON key for Claude Code/OpenCode, `mcp_servers` YAML key for Hermes — without touching any other key in the file, since these are the user's actively-used configs (Claude Code's `~/.claude.json` in particular carries project history unrelated to MCP).
- Uninstall removes just that one entry. Install/uninstall state is re-checked on every path-field edit in `SettingsViewModel` so the toggle buttons stay accurate.
