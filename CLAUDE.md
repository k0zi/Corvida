# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All commands run from `src/` (where `Corvida.sln` lives) or from `src/Corvida/Corvida/` (where `Corvida.csproj` lives).

```bash
dotnet build        # compile
dotnet run          # run the desktop app
dotnet clean        # clean build artifacts
dotnet publish      # produce release output
```

There is no test project; the solution has no xUnit/NUnit setup.

## Architecture

Corvida is a cross-platform desktop Kanban board app targeting .NET 10 with Avalonia UI.

### Data model

Three core entities form a hierarchy: `Board → KanbanGroup → KanbanTask`. Boards hold groups (columns like "To-Do", "In Progress"), and groups hold tasks. Tasks carry title, description (markdown), priority, and created-at timestamp.

### Storage

Data is written to `~/CorvidaData/` with this layout:

```
boards/
  {boardId}/
    board.json          ← Board + KanbanGroups serialised as JSON
    tasks/
      {taskId}.md       ← KanbanTask as Markdown with YAML frontmatter
```

App configuration lives at `%APPDATA%\Corvida\settings.json` (managed by `SettingsService`).

### Layers

| Layer | Location | Role |
|---|---|---|
| Models | `Models/` | Plain data classes (`Board`, `KanbanGroup`, `KanbanTask`, `AppSettings`) |
| Services | `Services/` | Business logic; each has an interface (e.g. `IBoardService`) and an implementation. Registered in `App.axaml.cs` via `Microsoft.Extensions.DependencyInjection`. |
| ViewModels | `ViewModels/` | UI logic using CommunityToolkit.Mvvm observable properties/commands. `MainWindowViewModel` owns the page stack and theme toggle. `BoardsPageViewModel` drives navigation between `BoardsListViewModel`, `BoardEditorViewModel`, and `TaskEditorViewModel`. |
| Views | `Views/` | Avalonia XAML bound to ViewModels. `ViewLocator.cs` maps VM types to View types automatically. |
| Controls | `Controls/` | Custom `MarkdownEditor` Avalonia control. |
| Converters | `Converters/` | `PriorityToEmojiConverter` for display. |

### Key services

- **BoardService** – CRUD for boards; serialises `board.json`.
- **TaskService** – CRUD for tasks; persists each task as a `.md` file with YAML frontmatter.
- **SettingsService** – Reads/writes `settings.json` in AppData.
- **DialogService** – Shows `InputDialog`, `ConfirmDialog`, and `PickerDialog` modals.

### UI framework details

- **Avalonia 11** with the **Fluent** theme and **SukiUI 6** component library for theming.
- **Material.Icons.Avalonia** for icons; **Markdown.Avalonia** for task description rendering.
- Theme (light/dark) is toggled via `MainWindowViewModel` and persisted in settings.