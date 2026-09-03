# Template Additions

This folder holds project-specific content that `dydo sync` injects into compiled skills via `{{include:name}}` tags.

## How It Works

Templates ship with `{{include:name}}` tags at natural extension points. Each tag resolves to a markdown file in this folder: `{{include:extra-verify}}` reads `extra-verify.md`.

- Missing file = tag resolves to empty string (no trace in output)
- Same file referenced from multiple templates = shared content, zero duplication

## Shipped Hook Points

| Tag | Template | Location |
|-----|----------|----------|
| `{{include:extra-must-reads}}` | All skills | After the Must-Reads list |
| `{{include:extra-test-guidance}}` | implementer, hardener | After the method |
| `{{include:extra-verify}}` | implementer, hardener | After the method |
| `{{include:extra-review-steps}}` | reviewer | After the method |
| `{{include:extra-review-checklist}}` | reviewer | After the method |

## Adding Content

1. Create a `.md` file here named after the tag (e.g., `extra-verify.md`)
2. On the next `dydo sync`, the content appears inline in the compiled skill

## File Naming

- `name.md` — active, resolved by `{{include:name}}`
- `name.md.example` — inactive example, rename to `.md` to activate
