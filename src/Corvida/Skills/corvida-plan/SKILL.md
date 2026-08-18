---
name: corvida-plan
description: Break down a task or feature request into a Corvida Kanban plan, creating tasks via the Corvida MCP tools and routing approval-needed items (questions for the user, sudo/elevated installs, destructive operations, actions touching external/shared systems) into a dedicated Human group. Use when the user asks to plan work with Corvida, wants a project tracked as Corvida tasks, or invokes /corvida-plan.
---

# Corvida Plan

Turns a task/feature request into tracked Corvida Kanban tasks, separating anything that requires human action into its own group so implementation can later proceed autonomously on everything else.

## Quick start

1. Ask the user which Corvida board to use (existing board name, or a new one to create).
2. Ensure the board has these groups, creating any missing ones: `To Do`, `In Progress`, `Done`, `Human`.
3. Break the request into discrete, actionable tasks.
4. For each task, decide: normal work → `To Do`; needs human action → `Human` (see criteria below).
5. Create all tasks via `create_task`, then report the plan back to the user.

## Workflow

- **Resolve the board**: call `list_boards`. If the user's board exists, use it; otherwise `create_board`.
- **Ensure groups**: call `get_board`; `add_group` for any of `To Do` / `In Progress` / `Done` / `Human` that's missing. Never rename or remove existing groups you didn't just create.
- **Decompose the work**: split the request into tasks small enough to implement and verify independently. Give each a clear title, a description with enough context to execute later without re-deriving it (why + what "done" looks like), and a priority.
- **Route to `Human`** instead of `To Do` when a task involves any of:
  - a question that only the user can answer (missing requirement, ambiguous choice)
  - installing or configuring something that needs sudo or elevated/system-level permissions
  - a destructive or hard-to-reverse operation (force-push, `reset --hard`, dropping data, deleting branches/files)
  - an action visible to or affecting shared/external systems (opening a PR, sending a message, publishing, calling a paid/external API)

  Write these task descriptions as a direct, answerable ask: what decision or action is needed, and why it's blocking.
- **Create tasks**: `create_task` into the resolved group. Keep titles and descriptions in English regardless of the conversation language.
- **Report back**: list what was planned (`To Do` count) and what needs the user's attention (`Human` tasks, each with a one-line reason).

## Notes

- This skill only plans — it does not execute tasks. Use the `corvida-implement` skill (or `/corvida-implement`) to work through the board afterward.
- If the board/groups already exist from a prior planning pass, add to them rather than duplicating — check existing task titles via `list_tasks` before creating near-duplicates.
