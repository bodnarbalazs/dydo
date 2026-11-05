import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createInterface } from "node:readline";
import type { Duplex } from "node:stream";
import { test, type TestContext } from "node:test";
import { skip as noTlc } from "./tlc.ts";

const entry = path.resolve(import.meta.dirname, "../native-entry.ts");
const guide = path.resolve(import.meta.dirname, "../../references/render.md");
const corpus = path.join(import.meta.dirname, "corpus");
const pele = process.env.STATECHART_PELE;
const noPele = !pele && "set STATECHART_PELE to the managed Pele module entry to render";
const noOwner = process.platform === "win32" && "dydo hands the POSIX owner channel over on POSIX only";
const nonce = "41504150415041504150415041504150";

function scratch(context: TestContext) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), "statechart-native-entry-")));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.mkdirSync(path.join(root, "stage"));
  return root;
}

// The request dydo saves for one corpus model, with the model's view when it has one.
function request(root: string, name = "Login") {
  const model = path.join(corpus, name);
  const view = path.join(model, "view.mjs");
  return {
    Spec: { Path: path.join(model, `${name}.tla`) },
    Config: { Path: path.join(model, `${name}.cfg`) },
    ...(fs.existsSync(view) ? { View: { Path: view } } : {}),
    Output: path.join(root, "out"),
    Caller: root,
  };
}

const native = (root: string, mode: string, renderer = pele, name = "Login") =>
  ({ mode, stage: path.join(root, "stage"), renderer, renderGuide: guide, request: request(root, name) });

function collect(child: ReturnType<typeof spawn>) {
  const streams = { stdout: "", stderr: "" };
  child.stdout!.setEncoding("utf8").on("data", (chunk: string) => { streams.stdout += chunk; });
  child.stderr!.setEncoding("utf8").on("data", (chunk: string) => { streams.stderr += chunk; });
  return streams;
}

// dydo's bootstrap protocol: READY on stdout, GO on stdin, and stdin held open until the entry exits.
async function gated(root: string, configuration: object) {
  const file = path.join(root, "stage", "node.json");
  fs.writeFileSync(file, JSON.stringify(configuration));
  const child = spawn(process.execPath, ["--disable-warning=ExperimentalWarning", entry, nonce, file],
    { cwd: root, stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
  const closed = once(child, "close");
  const lines = createInterface({ input: child.stdout! })[Symbol.asyncIterator]();
  let stderr = "";
  child.stderr!.setEncoding("utf8").on("data", (chunk: string) => { stderr += chunk; });
  assert.equal((await lines.next()).value, `ready:1:${nonce}`);
  child.stdin!.write(`go:1:${nonce}\n`);
  const [code] = await closed;
  child.stdin!.end();
  return { code, stderr };
}

// dydo's POSIX owner: it answers each control frame on the inherited channel, as PosixStatechartOwner does.
async function owned(root: string, configuration: object, handoff = ["3", nonce, "1"]) {
  const file = path.join(root, "stage", "posix-cli.json");
  fs.writeFileSync(file, JSON.stringify(configuration));
  const child = spawn(process.execPath, ["--disable-warning=ExperimentalWarning", entry, "posix-cli", file, ...handoff],
    { cwd: root, stdio: ["ignore", "pipe", "pipe", "pipe"] });
  const closed = once(child, "close");
  const streams = collect(child);
  const frames: string[] = [];
  const control = child.stdio[3] as Duplex;
  createInterface({ input: control }).on("line", (frame) => {
    frames.push(frame);
    const [kind, , pid] = frame.split(":");
    const replies: Record<string, string> = {
      attach: `attached:${nonce}:${pid}`, drain: `drained:${nonce}`, result: `result:${nonce}`, complete: `cleaned:${nonce}`,
    };
    control.write(`${replies[kind]}\n`);
  });
  const [code] = await closed;
  return { code, frames: frames.map((frame) => frame.replace(/^(attach:\w+):\d+$/, "$1:PID")), ...streams };
}

// The POSIX child dydo would start joins the owned group first; the shell stands in for that step.
const ownedCheck = (root: string, completedRequest: string | null, name = "Login") => ({
  ...native(root, "check", pele, name),
  nativeExecutable: "/bin/sh",
  nativeArguments: ["-c", 'shift 2; exec "$@"', "sh"],
  completedRequest,
});

test("a gated render-ready probe renders through the managed Pele", { skip: noPele }, async (context) => {
  const root = scratch(context);

  const { code, stderr } = await gated(root, native(root, "render-ready"));

  assert.equal(code, 0, stderr);
});

test("a gated entry refuses a renderer that is not an absolute managed module", async (context) => {
  const root = scratch(context);

  const { code, stderr } = await gated(root, native(root, "render-ready", "pele.js"));

  assert.equal(code, 2);
  assert.match(stderr, /absolute verified managed module/);
});

// A renderer module standing in for Pele, written where the entry can import it by absolute path.
function renderer(root: string, body: string) {
  const file = path.join(root, "renderer.mjs");
  fs.writeFileSync(file, body);
  return file;
}

for (const [name, body, error] of [
  ["has no render function", "export const draw = () => '<svg/>';\n", /has no render function/],
  ["renders no SVG", "export const render = () => ({ svg: '<p/>' });\n", /did not produce an SVG/],
] as const)
  test(`a gated render-ready probe refuses a renderer that ${name}`, async (context) => {
    const root = scratch(context);

    const { code, stderr } = await gated(root, native(root, "render-ready", renderer(root, body)));

    assert.equal(code, 2);
    assert.match(stderr, error);
  });

test("a gated check whose chart cannot render keeps the report, the Mermaid and why", { skip: noTlc },
  async (context) => {
    const root = scratch(context);
    const failing = renderer(root, "export const render = () => { throw new Error('no layout'); };\n");
    const { View: _view, ...withoutView } = request(root);

    const { code, stderr } = await gated(root, { ...native(root, "check", failing), request: withoutView });

    const done = path.join(root, "stage", "done");
    assert.equal(code, 2);
    assert.match(stderr, /Statechart render error; valid report and Mermaid retained: Error: no layout/);
    assert.deepEqual(JSON.parse(fs.readFileSync(path.join(done, "render-error.json"), "utf8")),
      { state: "render-error", message: "Error: no layout" });
    assert.deepEqual(fs.readdirSync(done).filter((name) => /\.(?:md|mmd|svg)$/.test(name)).sort(),
      ["chart.mmd", "report.md"]);
  });

test("a gated entry refuses an operation it does not know", { skip: noPele }, async (context) => {
  const root = scratch(context);

  const { code, stderr } = await gated(root, native(root, "install"));

  assert.equal(code, 2);
  assert.match(stderr, /Unsupported native statechart operation/);
});

test("a gated check runs its worker and stages the verdict, the report and an SVG for each chart",
  { skip: noTlc || noPele }, async (context) => {
    const root = scratch(context);

    const { code, stderr } = await gated(root, native(root, "check"));

    const done = path.join(root, "stage", "done");
    assert.equal(code, 0, stderr);
    assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, "stage", "verdict.json"), "utf8")), { verdict: 0 });
    assert.deepEqual(fs.readdirSync(done).filter((name) => /\.(?:md|mmd|svg)$/.test(name)).sort(),
      ["chart.mmd", "chart.svg", "report.md"]);
    assert.match(fs.readFileSync(path.join(done, "chart.svg"), "utf8"), /^<svg[^>]*><style>/);
  });

for (const [name, verdict, completed] of [["Login", 0, false], ["CounterJump", 1, true]] as const)
  test(`the POSIX owner handoff of ${name} attaches, drains, publishes${completed ? ", reports its verdict" : ""} and completes`,
    { skip: noOwner || noTlc || noPele }, async (context) => {
      const root = scratch(context);
      const completedRequest = completed ? path.join(root, "completed.json") : null;

      const { code, frames, stdout, stderr } = await owned(root, ownedCheck(root, completedRequest, name));

      assert.equal(code, verdict, stderr);
      assert.deepEqual(frames, [`attach:${nonce}:PID`, `drain:${nonce}`,
        ...(completed ? [`result:${nonce}:${verdict}`] : []), `complete:${nonce}`]);
      const report = fs.readFileSync(path.join(root, "out", "report.md"), "utf8");
      assert.equal(stdout, `${report}\nWrote ${path.join(root, "out")}\n`);
      assert.equal(fs.existsSync(path.join(root, "stage", "done")), false);
    });

test("the POSIX owner handoff refuses an inherited channel it cannot trust", { skip: noOwner }, async (context) => {
  const root = scratch(context);

  const { code, frames, stderr } = await owned(root, ownedCheck(root, null), ["3", "not-a-nonce", "1"]);

  assert.equal(code, 2);
  assert.deepEqual(frames, []);
  assert.match(stderr, /Invalid inherited statechart POSIX ownership handoff/);
});

test("the POSIX owner handoff refuses a configuration other than a check from its caller", { skip: noOwner },
  async (context) => {
    const root = scratch(context);

    const { code, frames, stderr } = await owned(root, { ...ownedCheck(root, null), mode: "render-ready" });

    assert.equal(code, 2);
    assert.deepEqual(frames, []);
    assert.match(stderr, /Invalid statechart POSIX checking configuration/);
  });
