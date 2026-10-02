---
area: general
type: changelog
date: 2026-10-02
---

# dydo 3.1.2 — Map labels

dydo 3.1.2 is a refinement release for the `dydo map` viewer: each card now shows its issue's Linear
labels, the identifier doubles as the Linear link, and the pickable slot reads more plainly.

## Added

- Each card now shows up to three of its issue's Linear labels as chips in the top-left: a neutral
  rounded pill with a small dot in the label's own Linear colour and the name in the card's
  secondary text colour, styled for both the light and dark palettes. Chips sort alphabetically
  (case-insensitive); a chip that doesn't fit whole on the row is hidden rather than clipped, and
  only a lone first chip too wide for the row truncates with an ellipsis. There's no `+N` counter —
  the chip row's tooltip lists every label on the issue.
- The identifier now doubles as the Linear link: the top-right of the card shows `DYD-69 ↗` in
  monospace, styled like the former `Linear ↗` button and sharing its `aria-label`. It opens Linear
  in a new tab, and clicking it doesn't select the card. This replaces the separate identifier text
  and the old `Linear ↗` button.
- The bottom-right slot, previously the assignee or an `unassigned` label, now shows a `Pickable`
  badge instead of `unassigned` when the issue is pickable — an issue is pickable only when
  unassigned, so this is a straight swap. Assigned issues still show the assignee there, and the
  pickable ring/glow is unchanged.

## Changed

- `dydo map`'s CLI surface, Linear API access and read-only behaviour are unchanged from 3.1.1;
  everything in the Added section above is viewer-side card rendering.
- Refresh now treats a label change (by name and colour, set-wise) as a changed field like any
  other, so a label edit in Linear is picked up and counted in the Refresh change notice, same as
  other field changes.
- Label colours come from Linear's own API — there's no hard-coded palette.

## Upgrade

Install or update dydo to 3.1.2. No project files change, and no new configuration is required to
see the label chips, the identifier link or the Pickable badge.

## Related

- [dydo 3.1.1 — Map polish](../2026-09-27/dydo-3-1-1-map-polish.md) — the release this follows.
