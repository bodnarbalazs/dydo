# Bug

1. Name the expected and observed behaviour at the highest stable seam, and the behaviour the fix
   must not change.
2. Reproduce it red: a scenario when the defect shows at the product's boundary, else a test at
   the seam. An existing red test, an inquisition's among them, is the reproduction; adopt it.
   Done when one observation could refute the reproduction.
3. Fix it, then revert the fix, watch the reproduction fail, and restore. The fix claims success
   only with that red line in the return, and with the unchanged behaviour still green.
