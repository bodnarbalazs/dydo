# Bug

1. Name the expected and observed behaviour at the highest stable seam, and the behaviour the fix
   must not change.
2. Reproduce it red: a scenario when the defect shows at the product's boundary, else a test at
   the seam. An existing red test, an inquisition's among them, is the reproduction; adopt it.
   An unused member, export or field needs no test: its definition at `file:line` and a reference
   search showing no caller outside the tests are its reproduction. Its fix deletes the member with
   its test-only callers, and step 3's revert does not apply.
   Done when one observation could refute the reproduction.
3. Fix it, then revert the fix, watch the reproduction fail, and restore. The fix claims success
   only with that red line in the return, and with the unchanged behaviour still green.
