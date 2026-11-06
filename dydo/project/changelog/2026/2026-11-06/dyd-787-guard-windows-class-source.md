---
area: general
type: changelog
date: 2026-11-06
---

# dydo 3.6.0 - Guard Windows documentation points to the CI class list (unreleased)

## Summary

The Testing Strategy's `guard-windows` paragraph now points to `GUARD_CLASSES` in the
`guard-windows` job of `.github/workflows/ci.yml`, the one list of selected classes. Its former
five-class copy omitted `GuardMergeRouteTests`
([DYD-787](https://linear.app/bodnar-balazs/issue/DYD-787)).

## Changed

The paragraph keeps the job's behavior and failure conditions while removing the duplicate list.
The Scaffold guide has no corresponding paragraph and does not change for this ticket. No tests
or CI selection change.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`dydo/guides/testing-strategy.md` and this entry.

## Related

- [Testing Strategy](../../../../guides/testing-strategy.md) - Hosted platform proof
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
