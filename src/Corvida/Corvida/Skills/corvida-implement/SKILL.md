---
name: corvida-implement
description: Work through a Corvida Kanban board's To Do tasks via the Corvida MCP tools, moving each through To Do -> In Progress -> Done, while diverting anything needing sudo/elevated access, destructive operations, actions on external/shared systems, or user input into the Human group instead of performing it directly. Use when the user asks to implement, execute, or work through Corvida tasks, or invokes /corvida-implement.
---

# Corvida Implement

Executes tasks tracked on a Corvida board, keeping humans in the loop for anything that shouldn't be done unattended.

## Quick start

1. Identify the board (ask the user, or resume the one just planned).
2. `list_tasks` / `get_board` for the `To Do` group.
3. For each task: if it turns out to need human action, `move_task` to `Human` and explain why; otherwise `move_task` to `In Progress`, do the work, `update_task` with the outcome, `move_task` to `Done`.
4. Keep going through the remaining independent tasks even if some land in `Human`.
5. Finish with a summary: done, blocked-in-`Human` (with what's needed), failed.

## Workflow

- **Resolve the board**: the same board used for planning; confirm with the user if ambiguous.
- **Pull work**: get tasks currently in `To Do` (highest priority / oldest first, unless the user specifies an order).
- **Before starting each task**, re-check whether it actually requires human action (criteria below). If so and it isn't already flagged, `move_task` to `Human`, update its description with the specific question or action needed, and move on — don't block the rest of the queue on it.
- **Human-routing criteria** (same as the planning skill): a question only the user can answer; sudo/elevated-permission installs or system changes; destructive/hard-to-reverse operations (force-push, `reset --hard`, deleting data/branches/files); actions touching external or shared systems (PRs, messages, publishing, external APIs).
- **Execute normal tasks**: `move_task` to `In Progress` before starting (so board state reflects reality mid-run), do the implementation work with the project's normal tools, `update_task` with a short summary of what was done and any follow-ups, then `move_task` to `Done`.
- **On failure**: don't move to `Done`. `update_task` with what went wrong and leave it in `In Progress`, or move to `Human` if resolving the failure requires a human decision.
- **Never resolve `Human` tasks yourself** — they exist because the action isn't safe or appropriate to take autonomously. Only move a task out of `Human` once the user has explicitly answered or approved it in the conversation.
- **Finish with a summary**: counts of Done / still In Progress / Human, and for each `Human` task a one-line reason so the user knows exactly what's blocking.

## Notes

- Use the `corvida-plan` skill (or `/corvida-plan`) first if the board doesn't have tasks yet.
- If a `To Do` task is too vague to execute safely, treat it as a question and route it to `Human` rather than guessing.
