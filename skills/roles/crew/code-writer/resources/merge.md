# Merge

One operation on one record, judged by one fresh merge review. Pin source and target branches at
their SHAs, the source's PASS, the governing contract and the combined gates; read both sides before
naming a resolution.

1. Map the conflicts, the shared seams and what changed since the source review. State each
   resolution's intended behaviour; a new product choice goes back to the captain. Done when you can
   merge without choosing policy.
2. Merge with a merge commit that keeps both parents and every hop SHA, apply the resolutions, and
   run the combined gates. A resolution that refactored leaves its code no worse than either side.
   Return both parents and the merge SHA.
