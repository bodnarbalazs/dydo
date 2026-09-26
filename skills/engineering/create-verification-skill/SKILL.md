---
name: create-verification-skill
description: Generate a project-local verification skill that drives the app the way a user does, through a deterministic control CLI and a feature map.
disable-model-invocation: true
---

<!-- Adapted from cursor/plugins pstack/create-verification-skill at ecc249f1e306fc64ddf83c7bed16cacf7c2239db (MIT). -->

# Create a verification skill

Every serious project needs a scripted way to drive the real app and prove behaviour: launch it, exercise a feature the way a user would, and capture evidence. This skill generates that as a project-local skill, `skills/engineering/verify-<app>/`, tailored to the repository. You write the generator's output for the next agent, not for a human: it will be read cold, mid-task, by an agent that has never seen the app.

## 1. Interview the repo, not the human

Answer these from the codebase and ask the human only what you cannot observe:

- **Surface:** what does a user actually touch? A web UI, a CLI/TUI, a desktop app, an API, a mobile app, a library? A repo can have several; pick the primary one and note the rest.
- **Run:** how does the app start locally? Prefer the repo's own documented dev command (package scripts, Makefile, README quickstart, an orchestrator such as Aspire or Compose). Note ports, env vars and seed data.
- **Sign in:** how does an agent get past the auth gate? Prefer a development-only quick login or a pre-signed-in disposable profile over typed credentials. Where a flow needs credentials, they belong to a disposable test account, come from the environment, and stay out of every artifact. Name how an expired session shows itself and how the driver recovers.
- **Drive:** how can an agent interact with it programmatically? Existing harnesses first: Playwright/Cypress specs, expect scripts, PTY helpers, curl-able endpoints, a debug port. Only then pick a generic recipe: browser/CDP for web and Electron, a tmux/PTY harness for CLI/TUI, plain HTTP for services.
- **Observe:** what evidence can be captured? Screenshots, accessibility snapshots, terminal transcripts, response bodies, logs, exit codes, DB state.
- **Isolate:** can two instances run side by side (ports, data dirs, profiles)? If not, say so in the generated skill: refusing to double-drive a shared instance beats corrupting the human's session.

If the checkout doesn't build or start as-is, fix that first (or report it precisely) before generating; a skill written against a broken base teaches wrong steps. When an irrelevant missing asset blocks startup (a static dir the API never serves, a sample config), the generated skill may create it, clearly marked as verification scaffolding, and remove it in cleanup.

## 2. Write the control CLI

The drive surface is one deterministic command-line tool in the repository, `control-<app>`, written in the project's own tooling language on the harness the interview found. Any agent with a shell calls it on any host, and it is ordinary code with its own tests.

- **Generic verbs** pass straight through to the harness: snapshot, screenshot, click, fill, press, wait-for, console, network log.
- **Project verbs** carry what an agent would otherwise rediscover every run: launch, doctor, sign in as a preset, expire the session, a read-only API or state check, cleanup.
- **State survives between calls.** Each command is a fresh process, so launch starts the browser or app with a debug port this run owns and every later command reconnects to it.
- **Guardrails live in code:** it refuses to drive an instance this run did not start, keeps secrets out of its output, and writes evidence to one named folder.

## 3. Generate the skill

Write `skills/engineering/verify-<app>/SKILL.md` with YAML frontmatter: `name: verify-<app>` and a `description` that names the app, the surface, and when to reach for it. Leave it model-invoked: implementers and reviewers load it mid-task. Then run `node setup-skills.mjs` so both hosts discover it. Give it these sections, each grounded in what the interview actually found (no placeholders left):

- **Launch:** the exact command that starts the app for verification, and how to tell it's ready (a log line, a port answering, a prompt). Include teardown. For a short-lived CLI or TUI there is no server to keep alive: launch means build the binary (or install deps) once, then start each drive in its own isolated PTY or tmux session.
- **Doctor:** one read-only check that answers "is this instance worth driving?": process up, right version/build, port owned by us, auth valid. An agent runs this first whenever anything looks off.
- **Drive:** the control CLI with real selectors/commands from this repo, not examples. Prefer stable handles (ARIA roles and names, data attributes, prompt strings, route paths) over coordinates and tab order.
- **Evidence:** what to capture for a proof and where it goes. State the proof standards: exercise the real user path, not internal setters or test-only endpoints; capture the action and the resulting state, not just the final screen; verify side effects (files written, rows inserted, messages sent) alongside what's visible; mocks only where a production boundary already isolates the external system. When the safe path is a dry-run or test mode, verify what it actually skips by observing (files, network, git refs) rather than trusting its name: some dry-runs still touch the network or open a browser.
- **Cleanup:** how to tear down instances the run created. Kill what you started, by the handle launch recorded, never by process name. Cleanup removes instances and scratch state, never the evidence: proof artifacts survive the teardown, in a location the skill names.
- **Helpers:** any script the skill ships is executable and its invocation is shown in the skill body. A helper the reader has to reverse-engineer is not a helper.

## 4. Seed the feature map

Create `skills/engineering/verify-<app>/resources/features/README.md` plus one file per user-facing area you can identify (aim for the top 3-5 to start, from routes, commands, menus, or docs), following [the example map](resources/feature-map-example/README.md). The README carries the index, the baseline preconditions, the driving conventions and the proof bar. Each feature file answers, from the user's point of view: what the feature is, how to reach it, how to drive it with the control CLI, and what observable end state proves it works. The four H2s are `Sub-features`, `How to get to it (user POV)`, `Driving it with <control CLI>`, and `Gotchas`.

- **One file per area a user thinks in**, not per component.
- **Nest a big area:** when an area's files share a baseline (a seeded record, an open editor), it gets its own folder whose README carries that baseline.
- **Scenarios own claims.** Where the project has executable acceptance scenarios, the map points to the scenario for the expected values and owns only the route to them and the traps on the way.

The map is the repo's maintained verification source; a proof that drives one convenient entry point is incomplete when the map lists others.

## 5. Prove the generated skill before handing it over

Run its own instructions end to end once: launch, doctor, drive ONE mapped feature (one is enough; the map exists so later runs can cover the rest), capture evidence, clean up. After cleanup, confirm the evidence still exists at the named location: a cleanup that eats the proof fails this step. Fix what fails, and run the generated cleanup after every failed iteration too, so broken attempts don't strand processes and ports. A generated skill that was never executed is a draft, not a deliverable.

## 6. Hand over the maintenance loop

Name `maintain-verification-skill` in the generated skill's Evidence section as the upkeep loop an agent runs when a drive contradicts the map. Suggest a cadence only if the human asks.
