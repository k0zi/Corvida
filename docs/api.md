# Corvida REST API

This is the reference for writing your own tools against `Corvida.Api`: scripts, importers, bots,
integrations. Everything the desktop app, the web app and the MCP server do in server-hosted mode goes
through the endpoints described here, so a tool can do the same.

- [Base URL](#base-url)
- [Conventions](#conventions)
- [Authentication](#authentication)
- [Data model](#data-model)
- [Endpoints](#endpoints)
- [Working with boards and tasks](#working-with-boards-and-tasks)
- [Realtime events (SignalR)](#realtime-events-signalr)
- [Examples](#examples)

## Base URL

| How the server runs | API base URL |
|---|---|
| `docker compose up` / the Podman Quadlet units | `http://<host>:5083` |
| Through the web UI's nginx (same origin as the web app) | `http://<host>:8183` or `https://<host>:8283`. nginx proxies `/api`, `/connect` and `/hubs` to the API |
| `dotnet run --project Corvida.Api` | `http://localhost:5007` (see `Properties/launchSettings.json`) |
| Aspire AppHost | The port shown in the Aspire dashboard |

The desktop app stores the address it uses as `ServerUrl` in `{AppData}/Corvida/settings.json`
(default `http://localhost:5083`).

An OpenAPI document is served at `/openapi/v1.json`, **only in the Development environment**. This page
is the reference for production servers.

## Conventions

- **JSON, PascalCase in responses.** The API sends property names as they are in C#: `"Id"`, `"Groups"`,
  `"TaskIds"`. Request bodies are read case-insensitively, so `"id"` and `"Id"` both work. The only
  snake_case endpoint is the OAuth token endpoint `/connect/token`.
- **IDs are strings.** The server generates them for boards (`{name}-brd-{8 hex}`), groups
  (`{name}-grp-{8 hex}`) and agents created with `POST`. **Tasks get their ID from the client**: a task
  is created by a `PUT` to the ID you choose. The apps use 12 hex characters (`Guid.NewGuid().ToString("N")[..12]`).
  Any string that is safe in a URL path segment works.
- **Times** are ISO 8601. Send UTC (`2026-11-25T09:00:00Z`).
- **`PUT` replaces the whole object.** A field you leave out gets its default value (an empty list, `null`),
  so read the object first, change it, and send it back whole. See
  [Concurrent writers](#concurrent-writers).
- **Errors.** The status code is what to rely on. The bodies are not uniform yet:

  | Status | When | Body |
  |---|---|---|
  | `400` | Invalid input, dependency cycle | A JSON string (`"Dependency cycle: ..."`) or `{"Error": "...", "Details": [...]}` |
  | `401` | Login is required and no valid credential was sent | Empty, `WWW-Authenticate: Bearer` |
  | `403` | Authenticated, but not an Admin (admin endpoints) | Empty |
  | `404` | The board / task / agent / user does not exist | Empty |
  | `409` | The board is archived, the agent still has tasks, last admin, auth already enabled | A JSON string or `{"Error": "..."}` (agent: `{"error": "..."}`) |
  | `429` | Rate limit (token and enable endpoints) | `{"error":"slow_down","error_description":"..."}`, `Retry-After` header |

## Authentication

Login is **switchable at runtime and off by default**. Ask the server first:

```http
GET /api/auth/status
```

```json
{ "AuthRequired": false, "TokenEndpoint": "/connect/token" }
```

- **`AuthRequired: false`**: every endpoint except the admin ones (`/api/users`, `/api/apikeys`,
  `/api/auth/disable`) works without credentials. Send none.
- **`AuthRequired: true`**: send `Authorization: Bearer <token>` with every request, using one of the
  two token types below. Without it the API answers `401`. The admin endpoints also require the
  Admin role, otherwise `403`.

The setting can change while your tool runs. On `401`, check `/api/auth/status` again.

### Personal access tokens: the choice for tools

An administrator creates a personal access token (PAT) for a non-interactive client. In the desktop app
this is Settings → General → Access keys; through the API it is `POST /api/apikeys`. The token:

- starts with `corv_pat_` and is shown **once**, when it is created;
- acts as the user who created it, with that user's roles;
- does not expire unless it was created with `ExpiresInDays`;
- is revoked with `DELETE /api/apikeys/{id}`.

```http
Authorization: Bearer corv_pat_...
```

The MCP server authenticates this way. Its token is the `ApiKey` in `settings.json`.

### Password login (OAuth 2.0 password grant)

For a tool that signs in as a person:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=ada%40example.com&password=...&scope=offline_access
```

```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 1799,
  "refresh_token": "..."
}
```

- There is no `client_id`; the server accepts anonymous clients.
- The access token lives **30 minutes**. `scope=offline_access` (the only scope) also returns a refresh token
  that lives **30 days**.
- To get a new access token, send `grant_type=refresh_token&refresh_token=...`. **Every refresh returns a new
  refresh token; store it and use that one next time.** For 30 seconds the old one still works (so two
  requests that race each other both succeed). After that it answers `400 invalid_grant`, and the server
  treats it as stolen: it revokes the whole chain, so the newest refresh token stops working too and the
  user has to sign in again. Refresh from one place only. The apps
  refresh behind a lock for this reason.
- A wrong password answers `400` with `{"error":"invalid_grant", ...}`. Five failures lock the account for
  a while.
- `/connect/token` allows **10 requests per minute per IP address**.

`GET /api/auth/me` returns the caller: `{"UserId", "UserName", "DisplayName", "Roles": ["Admin"]}`.

### Plain HTTP

The server accepts tokens over plain HTTP unless it runs with `Auth:AllowInsecureTransport=false`. Do not
send passwords or tokens over plain HTTP outside localhost. Use the HTTPS port of the web container
(`8283`) or your own TLS proxy.

## Data model

The same classes as `Corvida.Core/Models`. A .NET tool can reference `Corvida.Core` and use them, or
the ready-made `HttpBoardService` / `HttpTaskService` / `HttpAgentService` clients.

### Board

```json
{
  "Id": "Webshop-brd-3f2a9c1e",
  "Name": "Webshop",
  "Groups": [
    { "Id": "To-Do-grp-0b1c2d3e", "Name": "To-Do", "TaskIds": ["a1b2c3d4e5f6"] },
    { "Id": "In-Progress-grp-4f5a6b7c", "Name": "In-Progress", "TaskIds": [] },
    { "Id": "Done-grp-8d9e0f1a", "Name": "Done", "TaskIds": [] }
  ],
  "IsArchived": false,
  "AgentIds": ["builtin-developer"],
  "CellOrders": [
    { "GroupId": "To-Do-grp-0b1c2d3e", "AgentId": null, "TaskIds": ["a1b2c3d4e5f6"] }
  ]
}
```

| Field | Meaning |
|---|---|
| `Groups` | The columns, in display order. **`TaskIds` is the list of the column's tasks**: a task whose ID is in no group does not appear on the board. |
| `IsArchived` | An archived board is read-only: `PUT` to it, and to its tasks, answers `409`. Change it only with the archive/restore endpoints. |
| `AgentIds` | Board members, the swimlanes of the board editor (agent IDs, see [Agent](#agent)). |
| `CellOrders` | The order of cards within one cell of the swimlane grid (one group × one agent; `AgentId: null` is the Unassigned lane). Optional: the apps rebuild missing or stale entries, so a tool may leave it alone. If you do change it, keep one task in at most one cell. |

### Task

```json
{
  "Id": "a1b2c3d4e5f6",
  "Title": "Backend API",
  "Description": "Markdown text",
  "GroupId": "To-Do-grp-0b1c2d3e",
  "BoardId": "Webshop-brd-3f2a9c1e",
  "Created": "2026-11-25T09:00:00Z",
  "Priority": "High",
  "PlannedStart": null,
  "PlannedEnd": null,
  "AssignedAgentId": "builtin-developer",
  "DependsOn": ["f6e5d4c3b2a1"],
  "EstimateOptimistic": 3,
  "EstimateMostLikely": 5,
  "EstimatePessimistic": 10
}
```

| Field | Meaning |
|---|---|
| `GroupId` | The column the task is in. Keep it in step with the board's `Groups[].TaskIds`, see [below](#working-with-boards-and-tasks). |
| `Priority` | `Low`, `Medium` (default) or `High`. |
| `Description` | Markdown. |
| `AssignedAgentId` | The agent the task is assigned to, or `null`. It decides the swimlane. |
| `DependsOn` | IDs of tasks **on the same board** that must be finished first. See [Dependencies](#dependencies). |
| `Estimate*` | A three-point PERT estimate in days, each optional. |

### Agent

```json
{
  "Id": "builtin-developer",
  "Name": "Developer",
  "Description": "Implements tasks",
  "Personality": "Markdown: the agent's system prompt",
  "Color": "#4C6EF5",
  "AvatarDataUri": null
}
```

Agents exist independently of the boards. A board refers to them through `AgentIds`, a task through `AssignedAgentId`.

## Endpoints

"User" means: anyone while login is off, any signed-in user while it is on. "Admin" means login must be
on and the caller must have the Admin role.

### Boards

| Method and path | Access | Description |
|---|---|---|
| `GET /api/boards` | User | Active (not archived) boards. |
| `GET /api/boards/archived` | User | Archived boards. |
| `POST /api/boards` | User | Body `{"Name": "Webshop"}`. Creates the board with the groups `To-Do`, `In-Progress`, `Done`. `201` with the board. |
| `GET /api/boards/{id}` | User | One board, archived or not. |
| `PUT /api/boards/{id}` | User | Replaces `Name`, `Groups`, `AgentIds` and `CellOrders` with the body. `409` on an archived board. Returns the saved board. |
| `DELETE /api/boards/{id}` | User | Deletes the board **with all its tasks**. `204`. |
| `POST /api/boards/{id}/archive` | User | Makes the board read-only. Returns the board. |
| `POST /api/boards/{id}/restore` | User | Makes it editable again. |

### Tasks

| Method and path | Access | Description |
|---|---|---|
| `GET /api/boards/{boardId}/tasks/{taskId}` | User | One task. |
| `PUT /api/boards/{boardId}/tasks/{taskId}` | User | Creates or replaces the task. Unknown IDs and the task's own ID are dropped from `DependsOn`; a cycle answers `400`. `409` on an archived board. Returns the saved task. |
| `DELETE /api/boards/{boardId}/tasks/{taskId}` | User | Deletes the task and removes it from the other tasks' `DependsOn`. `204`. It does **not** remove the ID from the board's `Groups[].TaskIds`; do that yourself. |

There is no endpoint that lists the tasks of a board: read the board and fetch the IDs in its
`Groups[].TaskIds`. The task endpoints do not check that the board exists. Always use a `boardId` you
have just read: a `PUT` to a board that does not exist fails with `500`.

### Agents

| Method and path | Access | Description |
|---|---|---|
| `GET /api/agents` | User | All agents. |
| `GET /api/agents/{id}` | User | One agent. |
| `POST /api/agents` | User | Body `{"Name": "Ada"}`. `201` with the agent and its generated ID. |
| `PUT /api/agents/{id}` | User | Creates or replaces the agent at that ID. |
| `DELETE /api/agents/{id}` | User | `409` while any task is assigned to it. Otherwise it also removes the agent from every board's `AgentIds` and moves its `CellOrders` cells to Unassigned. `204`. |

### Authentication and users

| Method and path | Access | Description |
|---|---|---|
| `GET /api/auth/status` | Anyone | `{"AuthRequired", "TokenEndpoint"}`. |
| `POST /connect/token` | Anyone | Token endpoint, see [Password login](#password-login-oauth-20-password-grant). |
| `GET /api/auth/me` | User | The caller. |
| `POST /api/auth/enable` | Anyone, 3 requests / 5 minutes / IP | Body `{"Email", "Password", "DisplayName"}`. Turns login on and creates the first Admin. `409` if login is already on. |
| `POST /api/auth/disable` | Admin | Body `{"Password", "Confirm": "DELETE ALL USERS"}`. Turns login off and **deletes every user and token**. |
| `GET /api/users` | Admin | `[{"Id", "UserName", "Email", "DisplayName", "Role", "IsLockedOut"}]`. |
| `POST /api/users` | Admin | Body `{"Email", "Password", "DisplayName", "Role": "User"\|"Admin"}`. Password: at least 8 characters. |
| `PUT /api/users/{id}` | Admin | Body `{"DisplayName", "Role"}`. `409` when it would demote the last Admin. |
| `POST /api/users/{id}/password` | Admin | Body `{"NewPassword"}`; for your own account also `"CurrentPassword"`. |
| `DELETE /api/users/{id}` | Admin | `409` for your own account and the last Admin. |
| `GET /api/apikeys` | Admin | `[{"Id", "Name", "CreatedAt", "LastUsedAt", "ExpiresAt"}]`. |
| `POST /api/apikeys` | Admin | Body `{"Name", "ExpiresInDays": null}`. `201` with `"Key"`: the token, **shown only this once**. |
| `DELETE /api/apikeys/{id}` | Admin | Revokes the token. |

Enabling login is a one-time step for whoever installs the server. Tools should not call `enable` or `disable`.

### Health

`GET /health` (ready) and `GET /alive` (process is running), **only in the Development environment**. In
production, use `GET /api/auth/status` as a readiness probe, as `install.sh` does: it needs no credentials.

## Working with boards and tasks

The board and its tasks are stored separately. **The board is the index**: its `Groups[].TaskIds` say
which tasks are on it and in which column, while each task also carries its own `GroupId`. The API does
not keep the two in step, so a tool that creates, moves or deletes tasks writes both. That is what the
desktop app and the MCP server do.

### Creating a task

1. `GET /api/boards/{boardId}`.
2. `PUT /api/boards/{boardId}/tasks/{newId}` with `GroupId` = the target column and `BoardId` = the board.
3. Add `newId` to that group's `TaskIds` and `PUT /api/boards/{boardId}`.

If step 3 is skipped, the task exists but appears nowhere.

### Moving a task to another column

1. Remove the ID from the old group's `TaskIds`, append it to the new group's, `PUT` the board.
2. Set the task's `GroupId` to the new group and `PUT` the task.

To assign the task to an agent (a swimlane), set `AssignedAgentId` and `PUT` the task. The agent should
be a board member (`AgentIds`), or the board editor shows the task in Unassigned.

### Deleting a task

`DELETE` the task, then remove its ID from `Groups[].TaskIds` (and from `CellOrders`) and `PUT` the board.

### Deleting a column

Delete its tasks first, then `PUT` the board without the group. Tasks whose group no longer exists stay
in the database but appear nowhere.

### Dependencies

- `DependsOn` refers only to tasks on the same board. The API drops unknown IDs and the task's own ID.
- A dependency cycle (A waits for B, B waits for A, possibly through others) answers `400`.
- The **Done column** is the group named `Done` (ignoring case). A task is *blocked* while any of its
  prerequisites is outside that column. The apps show this, but nothing prevents moving the task.
- The PERT schedule (expected time `(O + 4M + P) / 6`, earliest and latest start and finish, slack, critical
  path, phases) is not an endpoint. A .NET tool can compute it with `PertCalculator.Calculate` from
  `Corvida.Core`. An LLM tool can call the MCP server's `get_pert_schedule`.

### Concurrent writers

`PUT` replaces the whole object and there is no version check: the last writer wins. Read the board
right before changing it, change only what you need, and write it back at once. A tool that holds a
board for a long time should follow the [realtime events](#realtime-events-signalr) instead of writing
back a stale copy.

## Realtime events (SignalR)

Every change made through the API is broadcast to every connected client. The hub is at **`/hubs/kanban`**
(ASP.NET Core SignalR, JSON protocol). It has the same access rule as the API: while login is on, pass
the token in the `access_token` query string. WebSockets cannot send headers; the official clients
do this for you via `AccessTokenProvider`.

| Event | Arguments |
|---|---|
| `BoardChanged` | `board` (Board): created, updated, archived or restored |
| `BoardDeleted` | `boardId` |
| `TaskChanged` | `boardId`, `task` (Task): created or updated, including when another task's deletion removed an entry from its `DependsOn` |
| `TaskDeleted` | `boardId`, `taskId` |
| `AgentChanged` | `agent` (Agent) |
| `AgentDeleted` | `agentId` |
| `AuthStateChanged` | `authRequired` (bool): sent right before the server drops every connection because login was switched on or off |

The hub only sends; it has no methods for clients to call. Your own changes come back to you as events too.

## Examples

### curl: create a board and a task

```bash
API=http://localhost:5083
AUTH=()   # while login is on: AUTH=(-H "Authorization: Bearer corv_pat_...")

# 1. Create a board, remember its ID and the To-Do column.
BOARD=$(curl -s "${AUTH[@]}" -H 'Content-Type: application/json' \
  -d '{"Name":"Webshop"}' "$API/api/boards")
BOARD_ID=$(jq -r .Id <<<"$BOARD")
TODO_ID=$(jq -r '.Groups[] | select(.Name=="To-Do") | .Id' <<<"$BOARD")

# 2. Create the task at an ID of your choice.
TASK_ID=$(openssl rand -hex 6)
curl -s "${AUTH[@]}" -X PUT -H 'Content-Type: application/json' \
  -d "{\"Id\":\"$TASK_ID\",\"BoardId\":\"$BOARD_ID\",\"GroupId\":\"$TODO_ID\",\"Title\":\"Backend API\",\"Priority\":\"High\",\"EstimateMostLikely\":5}" \
  "$API/api/boards/$BOARD_ID/tasks/$TASK_ID"

# 3. Put it on the board.
jq --arg t "$TASK_ID" --arg g "$TODO_ID" \
  '(.Groups[] | select(.Id==$g) | .TaskIds) += [$t]' <<<"$BOARD" |
curl -s "${AUTH[@]}" -X PUT -H 'Content-Type: application/json' -d @- "$API/api/boards/$BOARD_ID"
```

### Python: list every task of every board

```python
import requests

API = "http://localhost:5083"
session = requests.Session()
# session.headers["Authorization"] = "Bearer corv_pat_..."   # while login is on

for board in session.get(f"{API}/api/boards").json():
    print(board["Name"])
    for group in board["Groups"]:
        for task_id in group["TaskIds"]:
            r = session.get(f"{API}/api/boards/{board['Id']}/tasks/{task_id}")
            if r.status_code == 404:
                continue    # the board lists a task that was deleted without updating it
            task = r.json()
            print(f"  [{group['Name']}] {task['Title']} ({task['Priority']})")
```

### C#: follow changes live

Needs the `Microsoft.AspNetCore.SignalR.Client` package; `KanbanTask` comes from `Corvida.Core`.

```csharp
using Corvida.Models;
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5083/hubs/kanban", o =>
        o.AccessTokenProvider = () => Task.FromResult<string?>(null))   // or "corv_pat_..."
    .WithAutomaticReconnect()
    .Build();

connection.On<string, KanbanTask>("TaskChanged", (boardId, task) =>
    Console.WriteLine($"{boardId}: {task.Title} is now in {task.GroupId}"));
connection.On<string, string>("TaskDeleted", (boardId, taskId) =>
    Console.WriteLine($"{boardId}: {taskId} deleted"));

await connection.StartAsync();
Console.ReadLine();
```

A .NET tool can also skip raw HTTP: register `HttpBoardService`, `HttpTaskService` and `HttpAgentService`
from `Corvida.Core` the way `Corvida.Mcp/Program.cs` does. `ApiKeyTokenProvider` reads the PAT from
`settings.json` for them.
