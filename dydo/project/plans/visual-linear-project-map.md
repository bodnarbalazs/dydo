---
title: Visual Linear project map
status: reviewed
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/visual-linear-project-map-9894ea3c35b1
---

# Visual Linear project map

`dydo map` opens a local, read-only browser map of one Linear Project: its issues across all statuses,
parent plates holding their sub-issues, blocking arrows, and the pickable work, laid out left to
right with React Flow. It is a projection of Linear's current work graph, fetched when the page
loads, never a second tracker. The Linear Project description holds the brief and the live map;
this plan pins the cross-cutting contract that five Issues on three stacks must share.

## 1. Specification

### Intent

Give the human an at-a-glance picture of how a Project lays out now: what is happening, what is
pickable, what blocks what, and how finished work fits the whole.

### In scope

- The `dydo map` command: key check, a loopback HTTP server, the embedded viewer, the JSON API and
  its Linear GraphQL reads.
- The viewer in `viewer/`: selectors, layout, plates, nodes, edges, external nodes, focus, the
  related-links toggle, error display.
- Decision Record 052 and the narrow corrections to the boundary texts that say dydo never reads
  Linear.
- The DR 048 gate set extended to the new TypeScript stack.
- CI and release building and embedding the bundle; a full-stack e2e through the real binary.

### Out of scope

- Any Linear mutation, cache, poll, subscription, history or snapshot.
- OAuth. Authentication is a personal API key in `LINEAR_API_KEY`.
- Editing, filtering or search beyond what the brief names; Milestones and Cycles.
- A Walkthrough, Inquisition or landing beyond those the admiral's method files after integration.

### Acceptance criteria

The Project brief's five acceptance examples are the destination. Each is proved at the final merge as
follows:

1. **Large Project readable.** Playwright against the fixture of "dydo 3.0 / Consolidate and
   release" (150+ issues, captured from Linear) shows expanded parent plates, completed issues,
   status cues, arrow direction, pan and zoom. Collapse all and Expand all change the plate
   count as expected. Screenshots are posted on the viewer Issue.
2. **Pickable.** Unit tests of the pickable rule, plus e2e on the scenario fixture (below): its
   `Todo`, unassigned, unblocked issue carries the pickable marker. Serving a variant with an
   assignee, or with an open blocker, removes the marker after a reload.
3. **External blocker.** e2e on the scenario fixture: a dashed external node's body changes `team` and `project` in the URL
   and focuses the node; its Linear button has `href` equal to the issue `url` and `target=_blank`.
4. **Stays in the map.** e2e: clicking a regular node's body selects it and the page URL keeps its
   origin; related edges are absent until the toggle is on, and they use a distinct style.
5. **Fresh on F5.** The CLI's `/api/graph` has no cache, which a server test proves: two requests
   make two GraphQL calls (a DYD-264 contract test). The full-stack e2e (DYD-267) changes the fake
   Linear's answer between two reloads and sees the new state.

**Fixtures** (DYD-265, `viewer/fixtures/`, all in the §3 contract shape): the *large* fixture is a capture
of "dydo 3.0 / Consolidate and release" (161 issues, but none in `Todo`), proving AC1. The *scenario*
fixture is a capture of this Project, P-DYD-20, which has `Todo` issues, blocked issues and Merge
sub-issues. It is extended by hand-built variants so it certainly holds a pickable issue, an assigned
`Todo`, an open blocker, a closed blocker (muted edge), an external blocker with a Project, one
without a Project, and a related link. It proves AC2 through AC4.

Live proof against the real Linear API needs the human's key, so it runs in the landing
walkthrough.

### Questions and answers

- **Command name?** `dydo map`, per the human. It amends DR 044's wording through DR 052.
- **Authentication?** `LINEAR_API_KEY`, a personal key (human, 2026-09-25). If it is missing, the
  command exits 2 before any request, because Linear answers keyless requests on a low-limit tier
  ([DYD-261](https://linear.app/bodnar-balazs/issue/DYD-261)).
- **Archived issues?** Excluded, together with every relation touching one (human, 2026-09-25).
- **External blocker without a Project?** A dashed node whose body only focuses it; its Linear
  button still opens it (human, 2026-09-25).
- **Does a resolved blocker move under Related?** No. Live data keeps the relation type `blocks` after
  the blocker is Done ([DYD-261](https://linear.app/bodnar-balazs/issue/DYD-261)). The map
  draws the current `blocks` relation and mutes it when the blocker is closed; it infers nothing.
- **Start before DYD-217 closes?** Yes. 3.0.0 is released (human, 2026-09-25).

## 2. Prior art

- [DR 044](../decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md): Linear owns
  live work; dydo does not mirror it. DR 052 keeps that rule and names this read-only exception.
- [DR 048](../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md): every
  stack meets one gate set, or its gap is recorded. `DynaDocs.Tests/coverage/inventory.py` cannot see
  `.ts`/`.tsx` today, hence DYD-266.
- `DynaDocs.csproj` embeds `Scaffold/**` as resources; the viewer bundle follows the same pattern.
- [DYD-262](https://linear.app/bodnar-balazs/issue/DYD-262) proved an AOT publish of
  `HttpListener` + `HttpClient` with zero warnings, and found that React Flow's docs rule out dagre
  for sub-flows with edges that cross parents.

## 3. Design

### Boundary

`dydo map` reads Linear when the page asks, and only then. No state survives the process. The key
stays in the CLI; the browser talks only to `localhost`. DR 052 records this as a narrow read-only
view under DR 044.

### Components

```text
dydo map ──► MapServer (HttpListener, http://localhost:<free port>/, GET only)
               ├─ /            embedded viewer/dist (index.html; assets by extension)
               └─ /api/*       MapApi ──► LinearGraphClient ──► POST https://api.linear.app/graphql
                                              Authorization: <LINEAR_API_KEY>   (no Bearer)
viewer/ (Vite, React, TypeScript strict, @xyflow/react, elkjs in a Web Worker)
```

- **Port**: a free port is found with `TcpListener(IPAddress.Loopback, 0)`, then
  `http://localhost:{port}/` is bound. Only `localhost` is used, because Windows needs no URL
  reservation for it and it rejects foreign Host headers.
- **Browser**: `UseShellExecute` on Windows, `open` on macOS, `xdg-open` on Linux; the URL is always
  printed; `--no-browser` skips opening.
- **Endpoint seam**: `DYDO_LINEAR_ENDPOINT` overrides the GraphQL URL. It exists for the
  full-stack e2e and is documented as such.
- **Bundle**: `viewer/dist/` is built, never committed. MSBuild embeds it when
  `viewer/dist/index.html` exists; otherwise `/` serves a short "viewer not built" page. CI and
  release always build it first.

### Linear reads

The query set comes from [DYD-261](https://linear.app/bodnar-balazs/issue/DYD-261): `Teams`,
`TeamProjects` and `ProjectIssues`. `ProjectIssues` fetches 25 issues per page, with
`children`, `relations` and `inverseRelations` at `first: 10` each, and reads each far-end issue's
team, project, state and `archivedAt` inline. The `IssueRelationsPage` follow-up runs only when a
nested `hasNextPage` is true. A 150-issue Project costs about 26k complexity points, well
under the 3M per hour budget, and each page stays under the 10k cap. `errors[]` is checked on
every response, including HTTP 200.

### HTTP API contract

This is the only contract between the CLI (DYD-264) and the viewer (DYD-265). Every response is
`application/json; charset=utf-8`. Ids are Linear UUIDs.

```text
GET /api/teams                   → 200 {"teams":[Team]}            sorted by name
GET /api/projects?team=<teamId>  → 200 {"projects":[Project]}      non-archived, sorted by name
GET /api/graph?project=<projId>  → 200 Graph
error                            → 4xx/5xx {"error":{"code":Code,"message":string}}

Team    = {"id","key","name"}
Project = {"id","name","url","status":{"name","type"}}
Graph   = {"project":{"id","name","url"},
           "issues":[Issue],        // the Project's non-archived issues
           "external":[Issue],      // non-archived far ends outside the Project
           "relations":[Relation]}  // deduplicated by id
Issue   = {"id","identifier","title","url",
           "state":{"name","type","color"},       // type: triage|backlog|unstarted|started|completed|canceled|duplicate
           "assignee": string|null,               // display name
           "parentId": string|null,
           "team":{"id","key"},
           "project":{"id","name"}|null}
Relation= {"id","type":"blocks"|"related","from":issueId,"to":issueId}   // blocks: from blocks to
Code    = "missing_parameter"(400) | "not_found"(404) | "linear_auth"(502)
        | "linear_rate_limited"(503) | "linear_error"(502)
```

`duplicate` and `similar` relations are dropped. A relation is kept only when both ends are
present in `issues` or `external`. A `parentId` that names no issue in `issues` renders as top level.

### Viewer behaviour

- **URL state**: `?team=&project=&focus=`. Changing a selector, or navigating to an external node,
  rewrites the URL; the data is fetched on load, so F5 refetches.
- **Layout**: ELK `layered`, direction `RIGHT`, `hierarchyHandling: INCLUDE_CHILDREN`. A parent
  issue that has sub-issues in the Project is a plate (a React Flow group node) holding its children
  as its own node, header included. Plates start expanded. A collapsed plate shows its header only,
  and edges to its hidden children are re-anchored to the plate. The toolbar holds Collapse all,
  Expand all and "Show related".
- **Node**: identifier, title, the exact `state.name`, a colour from `state.color`, an icon per
  `state.type`, the assignee (or "unassigned"), and a small Linear button (`<a href=url
  target=_blank rel=noopener>`). Clicking the body selects the node and centres it; it never
  navigates away.
- **Pickable**: `state.type == "unstarted"` and `assignee == null` and no **open** blocker. An open
  blocker is an incoming `blocks` relation whose `from` issue is not **closed**. An issue is closed
  when its `state.type` is `completed`, `canceled` or `duplicate`; the Dydo `Duplicate` status has
  type `duplicate`. In Dydo the only `unstarted` status is `Todo`, so this is the
  standard's rule, made portable to other teams.
- **Edges**: `blocks` is a solid arrow from blocker to blocked, muted when the blocker is
  closed (same closed rule). `related` is dashed, has no arrowhead, and is hidden until "Show related" is on.
- **External**: a small dashed node. With a `project`, its body sets the URL to that team and
  Project and focuses its id; with none, it only focuses.
- **Emphasis**: closed issues stay visible at reduced opacity; started
  work and open blockers are full strength.

### Stack and tooling (human's standing choices, from LC DR 001, 004, 007, 023, 030)

pnpm 11 with `minimumReleaseAge: 21600` and a committed lockfile. TypeScript strict with
`exactOptionalPropertyTypes`. Vitest 4 with Istanbul coverage. Unit tests are colocated
(`*.test.tsx`); Playwright specs live in `viewer/e2e/`. ESLint has `no-explicit-any`, sonarjs cognitive
complexity ≤ 20 and `no-nested-ternary` as errors. elkjs is used under its EPL-2.0 option, with a
notice. Notices cite committed paths or URLs only; the .NET notice tests check every backticked path.

**No authored script under `viewer/` is JavaScript**: every script is `.ts` or `.tsx`, configs included (`vite.config.ts`,
`eslint.config.ts`, `playwright.config.ts`, and the fake Linear server, run with
`node --experimental-strip-types`). `inventory.py` measures every `.js`/`.cjs`/`.mjs` it sees, so a
stray JavaScript file would put the viewer into the JS gates before DYD-266 gates it properly.

## 4. Implementation Issue map

### First pickable Issues

All three are `Feature` / `AFK` / `Todo` on base `feature/visual-linear-project-map`, run in
parallel on disjoint paths. Each Linear Issue carries its full contract: outcome, owned paths,
blockers, exact gates and base branch.

| Issue | Outcome | Owned paths (summary) |
|---|---|---|
| [DYD-263](https://linear.app/bodnar-balazs/issue/DYD-263) Record dydo map as a read-only Linear view (DR 052) | DR 052 accepted; DR 044 amendment pointer; boundary text corrected | `dydo/project/decisions/{052-*,044-*,_decisions}.md`, `dydo/understand/{architecture,about,work-model}.md`, `dydo/guides/{adding-a-command,troubleshooting}.md`, `dydo/reference/about-dynadocs.md` and its byte-identical `Scaffold/dydo/reference/about-dynadocs.md` |
| [DYD-264](https://linear.app/bodnar-balazs/issue/DYD-264) dydo map command: local server and Linear graph API | the command, server, API contract, GraphQL client, embedding | `Commands/MapCommand.cs`, `Services/Map/**`, `Serialization/MapJsonContext.cs`, `Program.cs`, `Commands/HelpCommand.cs`, `Services/CompletionProvider.cs` (top-level `map`), `Services/ConfigFactory.cs` (`map` in the dotnet-run nudge list), `DynaDocs.csproj`, `DynaDocs.Tests/Map/**` and command-row tests, both `dydo-commands.md` copies, `README.md` and `npm/README.md` command tables, `test-associations.json` rows for the new C# files |
| [DYD-265](https://linear.app/bodnar-balazs/issue/DYD-265) Project map viewer: React Flow graph of a Linear Project | the viewer, the fixture capture, e2e with fixtures | `viewer/**`, `.gitignore`, both `THIRD-PARTY-NOTICES.md` |

### Later bearings

- [DYD-266](https://linear.app/bodnar-balazs/issue/DYD-266) Gate the viewer TypeScript stack in
  gap_check. It is blocked by the merges of DYD-264 and DYD-265, and owns `DynaDocs.Tests/coverage/**` and the
  testing docs.
- [DYD-267](https://linear.app/bodnar-balazs/issue/DYD-267) Build and ship the map viewer in CI
  and release, with the full-stack e2e. It is blocked by the same two merges, and owns `.github/workflows/*`,
  `viewer/e2e/full-stack.spec.ts` and `viewer/e2e/fake-linear/**`.
- After integration come an Inquisition offer in `Backlog`, the landing Merge Issue and a Walkthrough with the
  human, including the first live run with a real key.

### Exact gates

- **.NET**: `~/.dotnet/dotnet build DynaDocs.sln --warnaserror`; `~/.dotnet/dotnet test DynaDocs.Tests`.
- **Docs**: `~/.dotnet/dotnet run --project DynaDocs.csproj -- check`.
- **Viewer**: `pnpm -C viewer install --frozen-lockfile`, then `run typecheck`, `run lint`, `run test`,
  `run coverage`, `run build`, `run e2e` (and `run e2e:full-stack` from DYD-267 on).
- **Published binary** (full-stack e2e): `pnpm -C viewer run build && ~/.dotnet/dotnet publish
  DynaDocs.csproj -c Release -r linux-x64 -o artifacts/map-e2e`. The spec reads the binary path
  from `DYDO_E2E_BIN` (default `../artifacts/map-e2e/dydo` relative to `viewer/`).
- **Whole set**: `$PY DynaDocs.Tests/coverage/gap_check.py all`, with `$PY` the static-gates venv
  interpreter described in [Coverage Tools](../../reference/coverage-tools.md) (`bin/python` on
  Linux). It gains viewer rows from DYD-266 on.

## 5. Ordering and isolation

The first captain opens `feature/visual-linear-project-map` from this plan's reviewed commit. Each Issue
branches from the feature head into its own worktree. The merge order is DYD-268 → DYD-269 → DYD-270 →
DYD-271 → DYD-272 (the Merge Sub-issues of 263 → 264 → 265 → 266 → 267); the admiral rewires an
independent ready PR forward. Hot shared paths are serial: `test-associations.json` goes to DYD-264, then
DYD-266. `viewer/playwright.config.ts` and `viewer/package.json` go to DYD-265, then DYD-267 alone, which adds the `e2e:full-stack` script. DYD-266 edits nothing under `viewer/`: its adapters call the scripts DYD-265 contracts (`typecheck`, `lint`, `test`, `coverage`, `build`, `e2e`). If it needs another script or config, it hands that back to the admiral. `.gitignore` belongs to DYD-265
alone. The API contract in §3 is fixed, so DYD-264 and DYD-265 never wait on each other. A needed
contract change comes back to the admiral as a plan amendment.

## 6. Watch-outs

- Always check `errors[]` on HTTP 200. Rate limiting is HTTP 400 with `RATELIMITED`.
- Relations to archived issues may need `includeArchived` to be seen at all. Since archived far ends are
  dropped anyway, the default is acceptable; the first live run confirms it.
- The same-Project relation appears in both `relations` and `inverseRelations`, so dedupe by id.
- elkjs is about 1.6 MB raw. Run it in a Web Worker so layout never blocks the page.
- A committed `viewer/dist` would enter the gate inventory and fail coverage. Keep it ignored.
- Every viewer merge (DYD-270 on) runs `gap_check.py all` too, so a gate regression lands on the
  Issue that caused it, not on DYD-266.
- A Windows HttpListener on `localhost` without admin rights is unmeasured. Release smoke on Windows is
  the human's walkthrough item.
- Fixtures come from real Linear data. Keep only the contract fields; the assignee is a display name.

## Not yet specified

- Whether ELK's layered layout reads well at 150+ issues with plates and crossing blockers. The
  fixture screenshots answer that first; the human's live look settles it. A poor result reopens
  layout tuning as a new Issue, not a redesign.

## Review evidence — 2026-09-25

Four fresh `reviewer(project-plan)` passes ran, each posted on the Linear Project. FAILs at `e807339`
(7 findings), `575b4a5` (2) and `d7fa9cb` (2) were corrected. The PASS is at `3e88cc6`. The human delegated
approval to the admiral for this AFK run (Project Notes, 2026-09-25); the admiral approved the
route at that PASS. This section and the `reviewed` status are the only changes since.
