# Template Additions

Project-specific text in this folder is injected at matching `{{include:name}}` hooks in authored skill
templates. Missing additions resolve to nothing; generated skill files remain output, never edit targets.

## Active additions

| Tag | Purpose |
|---|---|
| `extra-verify` | This repository's isolated test and coverage commands |
| `extra-review-steps` | The gates a review owes at its own stage, from a hop's focused run to a merge's full set |
| `extra-review-checklist` | Contract- and stage-applicable gate receipts for a review verdict |

Add a new hook only when project-specific guidance cannot live in the shared skill without making that
skill less reusable. Name the file after the hook and keep the addition shorter than the method it
supports.
