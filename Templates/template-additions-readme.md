# Template Additions

This folder contains project-specific content that gets injected into agent mode files via `{{include:name}}` tags.

## How It Works

Templates ship with `{{include:name}}` tags at natural extension points. Each tag resolves to a markdown file in this folder: `{{include:extra-verify}}` reads `extra-verify.md`.

- Missing file = tag resolves to empty string (no trace in output)
- Same file referenced from multiple templates = shared content, zero duplication

## Shipped Hook Points

| Tag | Template | Location |
|-----|----------|----------|
| `{{include:extra-must-reads}}` | All modes | After must-reads list |
| `{{include:extra-verify}}` | implementer | After verify step |
| `{{include:extra-review-steps}}` | reviewer | After "Run tests" step |
| `{{include:extra-review-checklist}}` | reviewer | End of review checklist |

## Adding Content

1. Create a `.md` file here named after the tag (e.g., `extra-verify.md`)
2. Next time an agent claims, the content appears inline in their mode file

## File Naming

- `name.md` — active, resolved by `{{include:name}}`
- `name.md.example` — inactive example, rename to `.md` to activate
