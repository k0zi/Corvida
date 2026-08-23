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

Ten projects in `src/Corvida.sln`:

| Project | Type | Role |
|---|---|---|
| `Corvida.Core` | Class library | Shared models + entire service layer |
| `Corvida` | Desktop app (WinExe) | Avalonia 12 UI; references Core |
| `Corvida.Api` | ASP.NET Core 10 | REST API with PostgreSQL (EF Core + Npgsql), SignalR realtime hub |
| `Corvida.Mcp` | Console (stdio) | MCP server; exposes Kanban tools to LLMs |
| `Corvida.AppHost` | .NET Aspire AppHost | Orchestrates `Corvida.Api` + PostgreSQL for local dev |
| `Corvida.ServiceDefaults` | Aspire shared lib | OpenTelemetry, resilience, service-discovery defaults for `Corvida.Api` |
| `Corvida.Core.Tests` | xUnit test project | Unit tests for `Corvida.Core` |
| `Corvida.Shared` | Avalonia class library | UI shared by the desktop and browser hosts: ViewModels, Views, dialogs, converters, `ViewLocator` |
| `Corvida.Web` | Avalonia Browser (WASM) | The same UI running in a browser; references `Corvida.Shared` only. Shipped by `docker-compose` as an nginx container that also proxies the API on the same origin — see `Corvida.Web/nginx.conf` |
| `Corvida.Api.Tests` | xUnit test project | Integration tests for `Corvida.Api` (EF Core migrations, board/task/agent endpoints, authentication) |

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

Frontmatter values go through `YamlScalar.Encode`/`Decode` rather than being interpolated straight
into `key: {value}`. Every value here is user-typed prose, and the plain form breaks on things users
actually enter: a description containing `": "` is an invalid mapping, a newline spills the rest of
the value into the frontmatter as a stray line, and a hex colour's leading `#` opens a comment that
swallows the value. `Decode` still reads the unquoted form, so files written before this exist keep
loading.

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

ASP.NET Core 10 with EF Core + Npgsql. Connection string: `CorvidaApi` in config (injected by Aspire or `ConnectionStrings__CorvidaApi` in docker-compose; there is no value in `appsettings.json`).

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

## Web UI hosting (Corvida.Web)

`docker-compose` builds `Corvida.Web/Dockerfile` into an nginx image on ports **8183** (HTTP) and
**8283** (HTTPS); `install.sh` exposes `--web-port`, `--web-https-port` and `--no-web`.

nginx serves the WebAssembly bundle **and proxies `/api`, `/connect` and `/hubs`** to the API
container. Same-origin is load-bearing twice over: it removes CORS from the picture entirely, and it
is the only way the HTTPS port can work, since a page served over HTTPS may not call a plain-HTTP
API. The `/hubs` location carries the WebSocket upgrade headers and a long read timeout, or SignalR
connections drop every 60 seconds.

The API address is **not** baked in at build time. `Program.ResolveApiBaseUrlAsync` reads
`config.json` from the site root at startup and falls back to the page's own origin (taken from the
`location.href` that `main.js` passes as `args[0]`). The container ships `{"apiBaseUrl": ""}` —
same origin — while the repo's `wwwroot/config.json` points at `http://localhost:5083` so
`dotnet run` still works against a separately hosted API.

The HTTPS certificate is self-signed, generated on first start by
`docker-entrypoint.d/10-corvida-cert.sh` into the `webcerts` volume.

### Three things the published WASM build needs that the desktop does not

All three fail *silently* — the app loads, requests return 200, and the UI is simply wrong — so
verify the browser build by looking at a rendered page, not by checking HTTP status codes.

1. **`TrimmerRoots.xml`** preserves `Corvida.Core` and `Corvida.Shared`. `ViewLocator` resolves
   views by type name, so nothing references them statically and the trimmer removes them; every
   page then renders as `Not Found: Corvida.Views.SomeView`.
2. **`JsonSerializerIsReflectionEnabledByDefault=true`.** The WebAssembly SDK turns
   System.Text.Json's reflection path off, so every `GetFromJsonAsync` throws
   `InvalidOperationException: JsonSerializerIsReflectionDisabled`. Because the list view models
   load fire-and-forget, the exception is swallowed and every list renders empty while the network
   tab shows a perfectly good response. The alternative is a `JsonSerializerContext` threaded
   through every call site in `Corvida.Core`, which the desktop and MCP hosts do not need.
3. **Cache headers that distinguish fingerprinted assets from the entry points.** Only
   `name.<hash>.ext` files are content-addressed; `dotnet.js`, `avalonia.js`, `storage.js` and
   `sw.js` keep their names across builds. Marking the whole `_framework/` directory `immutable`
   means a warm browser never re-fetches `dotnet.js` — which carries the boot manifest — so it
   keeps loading the entire previous bundle and a rebuild appears to do nothing.
4. **`python3` in the build stage** — the emscripten toolchain that compiles the WebAssembly
   runtime shells out to it, and the .NET SDK image does not include it. This one fails loudly.

## Authentication (Corvida.Api + all clients)

Authentication is **switchable at runtime** and **off by default**: a fresh install behaves exactly
as the API did before it existed. The switch is a single row in `auth.SystemSettings`.

### The gate

`CorvidaAccessHandler` (in `Corvida.Api/Auth/`) is the one place that decides. Authorization
policies are built once at startup, so the flag cannot live in a requirement — instead the handler
reads `IAuthGateState`, which caches the flag (3s TTL while open, 30s while closed; the asymmetry is
deliberate, since a stale "open" is a hole and a stale "closed" is only a 401) and fails closed if
the database is unreachable.

Every endpoint group carries `.RequireAuthorization(Policies.User)`, the hub included, and a
`FallbackPolicy` gates anything that forgets to. **Never use a bare `.RequireAuthorization()`** — it
adds `DenyAnonymousAuthorizationRequirement`, which cannot be satisfied while the gate is open.

### Identity and tokens

- **OpenIddict 7.6** is the authorization server, in-process, validating its own tokens via
  `UseLocalServer()`. Grants: `password` and `refresh_token`.
- **ASP.NET Core Identity** provides the user store, registered via `AddIdentityCore` +
  `AddSignInManager` — **without `AddDefaultTokenProviders()`**, so DataProtection never enters the
  picture. Two rules follow, and the code depends on them: use `CheckPasswordSignInAsync` (never
  `PasswordSignInAsync`, which needs the two-factor ticket path) and change passwords with
  `ChangePasswordAsync` / `RemovePassword`+`AddPassword` (never `Generate*TokenAsync`).
- **Claim destinations** are the sharp edge: a claim with no destination is silently dropped from
  the issued token. `TokenEndpoints.GetDestinations` sends everything to the access token except the
  security stamp. The identity is built with `nameType: Claims.Name, roleType: Claims.Role` to match
  what the validation stack expects — get that wrong and `User.IsInRole` is quietly false.
- **Signing keys** live in `auth.SigningKeys` (`ISigningKeyProvider`), not in the machine X.509
  store, so tokens survive a container restart. Disabling auth **rotates them**, which is what
  actually invalidates outstanding access tokens — they are self-contained JWTs, so truncating the
  token table does nothing to them.

### Schema

Identity, OpenIddict and the auth tables live in a separate `AuthDbContext` under the `auth` schema,
in the same database. `UseOpenIddict()` is applied in `OnModelCreating`, **not** on
`DbContextOptionsBuilder` — the options-builder overload works by replacing `IModelCustomizer`, which
`ApiWebApplicationFactory` would silently drop when it repoints the context at the test container.
Keeping every auth table in one schema is what lets the "disable" wipe derive its `TRUNCATE` list
from `db.Model.GetEntityTypes()` instead of a hand-maintained list that would rot.

### Clients

All three hosts register the same pieces from `Corvida.Core`:

```csharp
services.AddSingleton<ITokenStore, InMemoryTokenStore>();   // MCP uses ApiKeyTokenProvider instead
services.AddSingleton<IAuthClient, AuthClient>();
services.AddSingleton<ITokenProvider, TokenProvider>();
services.AddTransient<AuthMessageHandler>();
services.AddHttpClient("CorvidaApi").AddHttpMessageHandler<AuthMessageHandler>();
```

Registering the handler on the named client covers `HttpBoardService`/`HttpTaskService`/
`HttpAgentService` at once — each resolves its `HttpClient` per call, so none of them needed
changing. `TokenProvider` refreshes **single-flight** behind a `SemaphoreSlim`: OpenIddict rotates
refresh tokens and detects reuse, so concurrent redemptions would revoke the whole chain.

The login screen is an overlay inside the existing shell (`MainWindowViewModel.Login`), not a second
window — `Corvida.Web` runs under `ISingleViewApplicationLifetime`, where a second window is
impossible. Both `MainWindow.axaml` and `MainView.axaml` layer it over their side menu.

Anything that calls the API at startup — `BuiltInAgentSeeder`, the realtime connection, page loads —
runs from `MainWindowViewModel.SignedIn`, not at construction: behind a closed gate it would 401,
and the seeder in particular swallows failures and never retries.

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

Separate from the Skills feature, but the same file-based-installer shape — this exports Corvida's own **Agent** entities (the Kanban board-member records) as subagent `.md` files in the target tool's own format, and seeds a starter set of them at first run:

- **Bundled agent templates** ship under `Corvida/Corvida/Agents/*.md` (copied to the build output via `Corvida.csproj`'s `<None Include="Agents\**" .../>`) — currently `architect`, `code-reviewer`, `developer`, `orchestrator`, `security-reviewer`. Each file uses the same frontmatter+body shape as a Claude Code subagent (`name`/`description` + Markdown body).
- `BuiltInAgentSeeder.EnsureSeededAsync` (static, called fire-and-forget from `App.axaml.cs` at startup) reads those templates via `SkillMarkdownSerializer.Parse` and creates one `Agent` per template on first run — with a deterministic `builtin-{slug}` ID (so re-runs don't duplicate them) and a per-slug accent color — via whatever `IAgentService` is currently active (local or HTTP).
- `IAgentInstallerService`/`AgentInstallerService` does the reverse: it writes every *existing* `Agent` (built-in or user-created) out as one subagent definition per agent, so any Corvida Agent — not just the bundled templates — can be installed as a real subagent. Driven from the **Settings** page's "Install Agents" section.

### Exporting agents as subagents

Three things about this export are easy to get wrong, and all three fail silently.

**The target's format is not Corvida's format.** `AgentTarget` (`ClaudeCode`/`OpenCode`) picks an
`IAgentFileFormat`, because the two tools disagree in ways that produce a file the tool loads but
misreads. Claude Code takes the agent's identity from a `name` frontmatter key; OpenCode derives it
from the file path instead and ignores `name`, so definitions are written flat — a nested file would
be named `team/reviewer`. OpenCode's `mode` also defaults to `all`, which puts the agent in the
primary-agent switcher rather than the delegation list, so the export says `mode: subagent`
explicitly. Both use the file body verbatim as the system prompt, which is what `Agent.Personality`
becomes — that is why the Agents page labels it as such.

`tools`, `model` and `color` are deliberately omitted. Omitting the first two gives the subagent the
full tool set and the caller's model, the right default for an agent Corvida knows nothing else
about; `color` is omitted because Claude Code does not document what values it accepts (OpenCode
does — `#rrggbb` — so `Agent.Color` could be emitted there if it's ever wanted).

**Hermes is not a target here.** It has no on-disk agent definitions at all: its subagents are
spawned at runtime from a goal string via its `delegate_task` tool, so nothing ever read the
`~/.hermes/agents/` folder this used to write into. Hermes remains a target for skills and for the
MCP server, which it genuinely does load.

**Ownership is tracked outside the files.** `AgentInstallManifest`
(`{AppData}/Corvida/agent-installs.json`) records which files Corvida wrote into which root. Install
never overwrites a file that isn't in the manifest — those belong to the user, whose own
hand-written subagents live in the same folder — and reports them as conflicts instead; uninstall
deletes only manifest-owned files, and leaves alone any whose content hash no longer matches what
Corvida wrote. Install also removes manifest entries that no longer correspond to an agent, or the
old file left behind by a renamed one. The marker can't live in the file itself: none of the target
tools document what they do with an unrecognised frontmatter key, and a rejected key would cost the
whole agent.

`AgentSlug` derives the file name, folding accents rather than dropping them (`Tervező` →
`tervezo`, not `tervez`) and falling back to the agent id when a name transliterates away to
nothing. Two agents whose names slugify alike get distinct files.

## MCP self-registration (Corvida)

`IMcpInstallerService`/`McpInstallerService` lets the desktop app register the bundled `Corvida.Mcp` server with an external tool's own config, from the **Settings** page's "Install MCP Server" section (needs a path to `Corvida.Mcp.csproj` plus a per-tool config path, e.g. `~/.claude.json`, `~/.config/opencode/opencode.json`, `~/.hermes/config.yaml`):

- Each target (`McpTarget.ClaudeCode`/`OpenCode`/`Hermes`) merges a single `"corvida"` entry into that tool's real config file — `mcpServers`/`mcp` JSON key for Claude Code/OpenCode, `mcp_servers` YAML key for Hermes — without touching any other key in the file, since these are the user's actively-used configs (Claude Code's `~/.claude.json` in particular carries project history unrelated to MCP).
- Uninstall removes just that one entry. Install/uninstall state is re-checked on every path-field edit in `SettingsViewModel` so the toggle buttons stay accurate.
