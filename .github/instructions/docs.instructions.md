---
description: Documentation sync rules for README, DESIGN, and Copilot guidance files.
applyTo:
  - "README.md"
  - "DESIGN.md"
  - ".github/copilot-instructions.md"
---

# Documentation Instructions

## Keep these docs aligned

- `README.md`: current behavior, commands, defaults, and API contract.
- `DESIGN.md`: architecture/status/milestones and acceptance criteria.
- `.github/copilot-instructions.md`: Copilot execution guidance and conventions.

## Update requirements

- If behavior changes, update all impacted docs in the same PR.
- Keep commands copy-paste runnable.
- Prefer concise, implementation-accurate descriptions over aspirational text.
