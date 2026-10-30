# DynaDocs feature map

Finite initial map for Project P-DYD-46, coordinated DYD-430/DYD-433. The candidate source and
its executable acceptance scenarios own expected values; this map owns the driving route.

- [Initialize and check documentation](init-docs.md)
- [Guard tool calls](guard.md)
- [Activate and end overnight mode](machine-mode.md)
- [Pending release prerequisites](release-prerequisites.md)

Baseline: launch a fresh candidate with `verify-dydo`, save RUN, and doctor it before driving.
All material commands use the shared FIFO. All fixtures are owned disposable repositories,
never host-bound agent checkouts. RUN/evidence survives cleanup. Use
`maintain-verification-skill`'s Proof: exact candidate, action, observable effects, raw exits and
limitations. One successful journey is an initial demonstration, not full 3.6.0 release coverage.

The CLI needs no login. Viewer/Linear journeys need separately commissioned fixtures and service
access; they are not covered here. Host approval requires an actual host and user: synthetic hook
JSON cannot prove it. The public CLI has no machine-state isolation option.
