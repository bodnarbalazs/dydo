---
area: guides
type: guide
---

# Adding a dydo Command: The Doc-Consistency Surfaces

Adding a new top-level `dydo` command (or a new required flag) is never a one-file change: `DynaDocs.Tests/Commands/CommandDocConsistencyTests` discovers commands from the System.CommandLine tree and asserts they are documented everywhere, so the gate stays red until a fixed set of surfaces all move together. This guide lists those surfaces, plus the README clone-sync family the same test class enforces.

---

## The Six Surfaces

`CommandDocConsistencyTests` (and with it `gap_check`) fails until **all** of these are updated:

| Surface | Kind | What the test asserts |
|---------|------|-----------------------|
| `Commands/HelpCommand.cs` | Code | The command path appears in the help text (`AllCommands_AppearInHelpText`) |
| `DynaDocs.Tests/Commands/CommandSmokeTests.cs` | Test | `XCommand.Create` is in the factory array (`AllCommandFactories_InSmokeTests`) |
| `dydo/reference/dydo-commands.md` | Docs | A `### dydo <path>` section naming every option, with a code example using each **required** flag (Tests 2 + 4) |
| `Templates/dydo-commands.template.md` | Docs | Same sections and flags — reference must equal template (Test 3) |
| `dydo/reference/about-dynadocs.md` | Docs | The leaf command listed under `## Command Reference` (Test 6) |
| `Templates/about-dynadocs.template.md` | Docs | Byte-identical to `about-dynadocs.md` (Test 10) |

**Work split for multi-writer trees:** the two code/test surfaces belong to the code-writer's slice; the four markdown surfaces are "command docs" and can be owned by a separate docs pass. Whichever way it's split, the surfaces must land together — a command merged without its docs reddens *every* agent's gate run (see [Orchestration Pitfalls](./orchestration-pitfalls.md), pitfall 3).

---

## The README Clone-Sync Family

Editing the dydo README is really editing a coupled four-file set, enforced by the same test class:

- **Test 6** — every CLI command (dynamically discovered) must appear in `dydo/reference/about-dynadocs.md` **and** `Templates/about-dynadocs.template.md`. It checks *inclusion, not existence*: removing a command from the docs is safe; forgetting to add a new one fails the build. It checks about-dynadocs, not the root README.
- **Test 10** — `README.md` and `about-dynadocs.md` must share the exact same `##` heading set; `## Agent Roles`, `## For Teams`, and `## Self-Documentation` must be byte-identical (modulo image-path normalization); and `about-dynadocs.md` must equal `Templates/about-dynadocs.template.md` byte-for-byte.
- **Test 8** — `## License` must be identical across `README.md`, `npm/README.md`, `about-dynadocs.md`, and the template.
- **Tests 2/3/4** — `dydo/reference/dydo-commands.md` (+ its template) must document every command's options, and the two must match.

So: rewrite `README.md` ↔ `about-dynadocs.md` ↔ `about-dynadocs.template.md` in lockstep (matching headings, identical shared sections, complete command list). `npm/README.md` only has to match the License section — it can be trimmed and should avoid Mermaid (npmjs.com doesn't render it; GitHub does).

**Who may write which copy:** per-role path RBAC was deliberately removed in [Decision 024](../project/decisions/024-dydo-2-native-pivot.md) §2, so the "Read-only paths" list that `dydo agent status` prints (`Templates/**`, root and npm READMEs) is advisory display, not enforcement — a docs-writer *can* write `Templates/**` directly. Actual write enforcement is the universal off-limits list, nudges, and the no-cross-agent-workspace rule. Don't request a guard lift for role-path reasons; that won't be the blocker. Coordination of who edits which copy is a scheduling concern (avoid racing an in-flight sprint that owns those files), not a permissions one.

---

## Related

- [Testing Strategy](./testing-strategy.md) — The tiers and gates these tests run under
- [Orchestration Pitfalls](./orchestration-pitfalls.md) — Why a half-landed command reddens other agents' gates
- [Coding Standards](./coding-standards.md) — Code conventions
- [Decision 024](../project/decisions/024-dydo-2-native-pivot.md) — dydo 2.0 native pivot (RBAC removal)
