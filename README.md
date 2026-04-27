# Corvida

A cross-platform desktop Kanban board application built with C# and Avalonia UI.

## Features

- Create and manage multiple boards
- Organize tasks into customizable columns (e.g. To-Do, In Progress, Done)
- Write task descriptions in Markdown
- Task priorities with visual indicators
- Light and dark theme support

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Getting Started

```bash
git clone https://github.com/k0zi/Corvida
cd Corvida/src
dotnet run --project Corvida/Corvida
```

## Build

```bash
cd src
dotnet build
```

## Data Storage

Boards and tasks are stored locally under `~/CorvidaData/`. Each board is a folder containing a `board.json` file and a `tasks/` directory of Markdown files.

## License

See [LICENSE](LICENSE).
