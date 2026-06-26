---
description: Authoring guidance for Copilot skills in this repository.
applyTo:
  - ".github/skills/**/SKILL.md"
  - ".github/skills/README.md"
---

# Skills Authoring Instructions

## Skill format

- Every skill lives at `.github/skills/<skill-name>/SKILL.md`.
- Include YAML frontmatter with:
  - `name`: lowercase-hyphenated and matching folder name
  - `description`: explicit trigger conditions and task scope

## Content rules

- Keep each skill focused on one repeatable workflow.
- Include concrete repo file paths and commands.
- Prefer extending existing skills over creating overlapping ones.
- Keep skill text concise enough to avoid unnecessary token load.
