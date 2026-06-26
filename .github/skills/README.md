# GitHub Copilot Agent Skills

This repository uses Copilot Agent Skills under `.github/skills/`.

Each skill is a focused playbook that Copilot can load for repeated tasks in this Jellyfin plugin:

- `castcrew-feature-development`: end-to-end feature work across C# backend + embedded web UI + docs/tests.
- `castcrew-route-compatibility`: person-detail route fallback logic across jellyfin-web variants.
- `castcrew-test-runtime-compatibility`: diagnosing and fixing local .NET runtime/testhost mismatches.

## Structure

- Skill folder: `.github/skills/<skill-name>/`
- Required file: `.github/skills/<skill-name>/SKILL.md`
- `name` in frontmatter matches folder name.

## Guidance

- Prefer updating an existing skill over adding overlapping skills.
- Keep skill descriptions specific so Copilot can discover the right one.
- Keep examples runnable in this repo.
