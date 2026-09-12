---
area: general
type: reference
---

# Glossary

Terms specific to this repository's documentation. The dydo product's locked Linear, knowledge, and
execution vocabulary lives in the [dydo glossary](./reference/dydo-glossary.md).

## HCRAP

Hybrid CRAP, the per-method change-risk score of
[Decision 048](./project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md):
`CC² × (1 − line coverage)³ + cognitive complexity`. The coverage term uses cyclomatic complexity,
the floor uses cognitive complexity, so at full coverage the score is the cognitive complexity. The
one threshold is 20.

## Hub File

An authored navigation page, often named `_<folder>.md`, that makes a documentation folder navigable where
the documentation model requires one.

## JITI

Just-In-Time Information: agents navigate to the context needed for the current Issue instead of reading
the entire repository upfront.

## Off-Limits

Patterns in `dydo/files-off-limits.md` that block agent access to secrets, credentials, and protected
system files. Explicit allow rules can carve narrowly scoped exceptions.

## Template Addition

Retired with the compiler by Decision 049. Customization is a direct edit to the skill folder; a project document linked under ## Must-Reads carries shared context.

## Related

- [dydo Glossary](./reference/dydo-glossary.md)
