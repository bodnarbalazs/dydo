---
area: guides
type: guide
---

# Orchestration Pitfalls

Failure modes observed while agents execute Linear Issues against this repository. The common theme is
that Linear owns coordination, Git owns isolation and proof, and neither makes shared technical state
disappear.

## 1. An isolated worktree starts from the wrong commit

**Symptom:** a worker reports that required code or doctrine is missing.

**Mechanism:** worktree isolation starts from a selected Git revision, not from another checkout's
uncommitted state. A branch created from a stale base cannot see newer prerequisites.

**Rule:** record the exact governing commit on the Issue and create its branch or worktree from that
commit or a verified descendant. If a dependency has not landed, keep the Issue blocked in Linear.

## 2. File ownership is not dependency isolation

**Symptom:** one Issue's build or documentation gate fails on another worker's incomplete change.

**Mechanism:** solution builds, documentation validation, generated artifacts, and coverage are shared
gates. Disjoint owned paths can still depend on the same compiled or generated surface.

**Rule:** express the dependency between Issues and sequence their landings. Parallelize only when both
file ownership and gates are independent.

## 3. Workers commit shared-tree changes

**Symptom:** a commit includes another worker's unfinished edits or omits a concurrent change.

**Mechanism:** a shared checkout can contain several authors' unstaged work. Broad staging turns that
temporary union into permanent history.

**Rule:** workers return changed paths and evidence without committing unless their Issue explicitly
owns an isolated branch and authorizes the landing. The integrator verifies and stages exact paths.

## 4. Passing tests are not enough when provenance is wrong

**Symptom:** a green result cannot be tied to the Issue's actual diff or governing contract.

**Mechanism:** tests prove the checkout they ran against. They do not prove which commit was reviewed,
which worktree supplied the diff, or whether a later edit invalidated the result.

**Rule:** link the exact governing commit before execution, record the tested commit or diff, and run an
independent review against the same material. Project completion adds an integrated audit.

## 5. Live knowledge remains trapped in Linear

**Symptom:** future workers must search old comments or repeat a design discussion.

**Mechanism:** Linear is excellent for volatile coordination but is not the repository's durable product
memory.

**Rule:** extract accepted invariants and reusable lessons into a Decision, guide, Project plan, audit,
or assimilation brief, then link that artifact from the Issue. Do not mirror the Issue body into dydo.

## 6. A FutureFeature is treated as scheduled work

**Symptom:** an idea gains assignees, blockers, or delivery status in Git, or appears in Linear without a
human decision.

**Mechanism:** idea provenance and delivery coordination have been conflated.

**Rule:** keep the FutureFeature as `status: idea` until human promotion. Promotion creates one Linear
target and terminal repository provenance; all later delivery state remains in Linear.

## Related

- [Work Model](../understand/work-model.md)
- [Writing Good Briefs](./writing-good-briefs.md)
- [Testing Strategy](./testing-strategy.md)
- [DR 044](../project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
