---
title: Refresh positioning, restore MIT, and prepare v2.2.6
sprint: lean-wayfinder-adoption
seq: 3
status: done
blocked-by: [lean-wayfinder-adoption-2-pm-harmony]
area: general
type: context
---

# Slice 3 — Refresh positioning, restore MIT, and prepare v2.2.6

Align public positioning and package metadata with the leaner, MIT-licensed project.

## Spec fragment

Refresh dydo's public identity, credit the inspiration, restore MIT consistently, and prepare the
next patch version without changing release automation.

Acceptance: public text reflects a personal, evolving, opinionated harness rather than a stable
product promise; Matt Pocock's repository is linked; license and version surfaces are consistent;
package/release checks pass.

## Implementation detail

Edit these coupled public surfaces in lockstep:

- `README.md`
- `Templates/about-dynadocs.template.md`
- `dydo/reference/about-dynadocs.md`
- `npm/README.md`

Place this exact paragraph near the opening of all four surfaces:

> This project is my attempt to maintain my agent-harness customizations, project-management
> primitives, and coding tools. It evolves constantly: I no longer treat it as a product, and I
> promise no backward compatibility beyond what my own projects need. It is customizable where I
> need it to be and opinionated by default. Some features were ahead of their time and were later
> retired out of wisdom; more recently I have taken inspiration from similar collections such as
> [Matt Pocock's skills](https://github.com/mattpocock/skills).

After updating the root README, replace `npm/README.md` with a byte-for-byte copy of `README.md`.
Do not imply Matt's endorsement or make his repository a runtime dependency. In the root README,
keep every existing section and command example; only insert the exact positioning paragraph and
change the License section described below. In the about template and generated about document,
make those same two bounded edits and preserve all other text.

Replace root `LICENSE` and `npm/LICENSE` with the standard MIT text using exactly
`Copyright (c) 2026 Balazs Bodnar`.
Delete `CLA.md`: it exists only to support the former AGPL/commercial dual-license model and would
otherwise leave a false licensing claim in the repository.
Set:

- `DynaDocs.csproj` version to `2.2.6` and `PackageLicenseExpression` to `MIT`;
- `npm/package.json` version to `2.2.6` and license to `MIT`.

In `dydo/understand/about.md`, replace the two opening paragraphs with exactly:

> DynaDocs (dydo) is a documentation-driven context, project-management, skill-authoring, and
> guardrail framework for AI coding assistants. AI tools have memory features, but that memory is
> unstructured, opaque, and not under your control. dydo makes project context explicit and
> versioned, then compiles its durable guidance for native coding-agent runtimes.
>
> This is the dydo project itself. This documentation tree is both the project's knowledge base
> and a living example of the system. dydo authors and synchronizes context and skills; Claude Code
> and Codex own runtime identity, permissions, process lifecycle, and native subagent coordination.

In its `What DyDo Does` list, replace `Native orchestration` with exactly:

> **Native-runtime compilation** — `dydo sync` compiles shared role and skill sources into native
> Claude Code and Codex artifacts; the host runtime coordinates execution

Keep all other bullets and sections byte-for-byte unchanged. Keep `Program.cs`, NuGet description,
and npm description byte-for-byte unchanged; no marketing rewrite is authorized there.

Make the `## License` section byte-identical across all four README/about surfaces and concise:
`MIT — see LICENSE.` Remove AGPL/commercial-license prose. Keep README and about/template heading and
shared-section parity required by `CommandDocConsistencyTests`.

Extend `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs` to assert both license files begin
with `MIT License`, the project package expression is `MIT`, npm metadata is `MIT`, and `CLA.md` is
absent.

Verify `.github/workflows/release.yml` needs no edit: it publishes NuGet/npm/GitHub releases from
the pushed tag and derives package version from `v2.2.6`.

## Out of scope for this slice

Release workflow changes, publishing/tagging itself, prompt/PM edits, generated runtime artifacts,
or new commercial promises.

## Gate

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~CommandDocConsistencyTests" --no-restore
dotnet build DynaDocs.sln --no-restore --nologo
```

## Result

PASS — the focused `CommandDocConsistencyTests` gate and solution build passed. Exact-content
checks also proved the positioning paragraph and MIT section across all four public surfaces,
byte-identical README/about/license pairs, unchanged NuGet and npm descriptions, and
`dydo check` reported 0 errors (13 orphan warnings). The broader coverage run reached the
concurrently owned `FolderScaffolderTests.Scaffold_CreatesDydoGlossaryMd` failure before coverage
evaluation; no out-of-slice files were changed.
