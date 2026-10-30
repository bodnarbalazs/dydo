# Pending release prerequisites

## Sub-features

P-DYD-46's statechart and safe feature-branch merge journeys depend on DYD-415 and DYD-417.
Their exact merged candidates are not present in the commissioned base. DYD-406's guard work
also needs integrated proof. These are explicit missing prerequisites, not passing coverage.

## How to get to it (user POV)

Use the delivered statechart and safe-merge workflows once their actual merged implementations
and acceptance scenarios are available. The current driver exposes no substitute route.

## Driving it with control-dydo

No supported journey yet. Return `blocked: merged DYD-415/DYD-417 prerequisite absent` for a
request to drive these areas. Extend the finite map and driver on the integrated candidate,
with new owned fixtures and observed effects. Never perform a real master merge or publish.

## Gotchas

Copying unmerged code or describing intended behavior does not establish runtime proof.
One init/guard demonstration cannot close the release inquisition or prove these pending areas.
