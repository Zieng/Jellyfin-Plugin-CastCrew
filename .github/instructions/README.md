# Copilot Path-Scoped Instructions

This directory contains path-scoped instruction files for GitHub Copilot.

Each `*.instructions.md` file has YAML frontmatter with an `applyTo` glob so Copilot can load more specific guidance based on edited files.

Current instruction sets:

- `backend-plugin.instructions.md` for C# plugin/backend work.
- `web-ui.instructions.md` for embedded HTML/JS pages.
- `tests.instructions.md` for unit tests and runtime test behavior.
- `docs.instructions.md` for project documentation synchronization.
- `skills-authoring.instructions.md` for maintaining `.github/skills/**/SKILL.md`.
