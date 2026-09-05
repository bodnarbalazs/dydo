# Specify a Merge

One operation, one record, one merge review. Pin source and target branches and SHAs, the source's
PASS, the governing contract and the combined gates. Read both sides before naming a resolution.

1. Map conflicts, shared seams and changes since the source review. State each resolution's intended
   behavior; a new product choice is a hand-raise. Done when the implementer can merge and resolve
   without choosing policy.
2. Require a merge commit preserving both parents and hop SHAs, then the combined gates and a fresh
   `reviewer(merge)`. Declare hardening empty unless resolution refactors. Done when the integrated
   candidate, rather than either parent alone, is what the gates and review judge.
3. Name failure routing: an integration defect gets a fix hop here and fresh review. A source-work
   defect is reverted here, this Merge closes `Canceled` with the reason, and the source returns
   `Ready to Merge` → `Implementing`. If a later merge depends on it, a following fix Issue replaces
   the revert. The Captain owns those records and transitions.

For landing, the crew merges main into the feature and obtains acceptance proof before offering
the feature-to-main PR. The human's merge-commit click is a separate gate; a Merge Sub-issue itself
never waits in `Ready to Merge`.
