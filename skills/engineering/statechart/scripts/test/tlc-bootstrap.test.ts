import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { pathToFileURL } from "node:url";
import childProcess from "node:child_process";
import { syncBuiltinESMExports } from "node:module";
import { pinnedTlc } from "../run.ts";

const replayJar = pinnedTlc(path.resolve(process.env.TLA2TOOLS ?? "missing-pinned-TLC-fixture.jar"));

// These are launch-resolution tests with synthetic helper bytes. They never claim
// to compile Java, exercise RMI or establish real TLC/model semantics.
async function fixture() {
  const scope = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-bootstrap-test-"));
  const scripts = path.join(scope, "canonical/scripts");
  fs.cpSync(path.resolve(import.meta.dirname, ".."), scripts, {
    recursive: true,
    filter: (name) => !["node_modules", "test", "java"].includes(path.basename(name)),
  });
  fs.writeFileSync(path.join(scope, "canonical/package.json"), '{"type":"module"}\n');
  const java = path.join(scripts, "java");
  fs.mkdirSync(java);
  const helper = path.join(java, "statechart-tlc-bootstrap.jar");
  fs.writeFileSync(helper, "synthetic resolution fixture; never executed");
  const lock = { schema: 1, artifact: { state: "sealed-build", sha256: createHash("sha256").update(fs.readFileSync(helper)).digest("hex") } };
  const writeLock = () => fs.writeFileSync(path.join(java, "build-lock.json"), JSON.stringify(lock));
  writeLock();
  const jar = path.join(scope, "pinned TLC with spaces.jar");
  fs.copyFileSync(replayJar, jar);
  const { tlcInvocation, check } = await import(pathToFileURL(path.join(scripts, "run.ts")).href);
  const { prepareChecker } = await import(pathToFileURL(path.join(scripts, "checker.ts")).href);
  const owned = path.join(scope, "registered-test-owner");
  fs.mkdirSync(owned);
  const checker = prepareChecker(jar, owned);
  return { scope, java, helper, lock, writeLock, tlcInvocation, check, jar, checker, prepareChecker };
}

test("TLC resolves its sealed helper from the canonical script and uses only the isolated official checker", async () => {
  const f = await fixture();
  try {
    assert.deepEqual(f.tlcInvocation(f.checker), ["-cp", [f.helper, f.checker.jar].join(path.delimiter), "dydo.statechart.LoopbackTlc"]);
    assert.ok(!f.tlcInvocation(f.checker).includes("tlc2.TLC"));
    assert.deepEqual(fs.readFileSync(f.checker.jar), fs.readFileSync(f.jar));
  } finally { fs.rmSync(f.scope, { recursive: true, force: true }); }
});

for (const changed of [false, true]) {
  test(`${changed ? "changed" : "missing"} helper fails without raw TLC fallback`, async () => {
    const f = await fixture();
    try {
      if (changed) fs.appendFileSync(f.helper, "changed");
      else fs.unlinkSync(f.helper);
      assert.throws(() => f.tlcInvocation(f.checker));
    } finally { fs.rmSync(f.scope, { recursive: true, force: true }); }
  });
}

test("an unbuilt source lock cannot become a runnable helper identity", async () => {
  const f = await fixture();
  try {
    f.lock.artifact.state = "unbuilt";
    f.writeLock();
    assert.throws(() => f.tlcInvocation(f.checker), /no sealed build identity/);
  } finally { fs.rmSync(f.scope, { recursive: true, force: true }); }
});

test("a missing build lock fails before a checker command exists", async () => {
  const f = await fixture();
  try {
    fs.unlinkSync(path.join(f.java, "build-lock.json"));
    assert.throws(() => f.tlcInvocation(f.checker));
  } finally { fs.rmSync(f.scope, { recursive: true, force: true }); }
});

for (const discovery of [false, true]) {
  for (const changed of [false, true]) {
    test(`${discovery ? "discovered" : "explicit"} ${changed ? "changed" : "missing"} TLC is rejected before any Java child`, async t => {
      const f = await fixture();
      t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
      const jarRoot = path.join(f.scope, "tools");
      fs.mkdirSync(jarRoot);
      const invalid = path.join(jarRoot, "tla2tools.jar");
      const sentinel = Buffer.from("foreign jar must remain unchanged");
      if (changed) fs.writeFileSync(invalid, sentinel);
      const model = path.join(f.scope, "Input.tla");
      const config = path.join(f.scope, "Input.cfg");
      fs.writeFileSync(model, "---- MODULE Input ----\nVARIABLE x\nInit == x = 0\nNext == x' = x\n====\n");
      fs.writeFileSync(config, "INIT Init\nNEXT Next\nCHECK_DEADLOCK FALSE\n");
      const originalModel = fs.readFileSync(model);
      const originalConfig = fs.readFileSync(config);
      const previous = { JAVA: process.env.JAVA, TLA2TOOLS: process.env.TLA2TOOLS };
      process.env.JAVA = "JAVA_MUST_NOT_START";
      if (discovery) delete process.env.TLA2TOOLS;
      else process.env.TLA2TOOLS = invalid;
      const launches: string[] = [];
      t.mock.method(childProcess, "execFile", (file: string, args: string[], _options: unknown, callback: Function) => {
        launches.push(file);
        assert.equal(file, "mise", "A Java/SANY/TLC child must never be reached");
        assert.ok(args[0] === "where" || args[0] === "which", "No install after successful discovery");
        callback(null, args[0] === "where" ? jarRoot : "JAVA_MUST_NOT_START", "");
      });
      t.mock.method(childProcess, "spawn", () => { throw new Error("Java/SANY/TLC spawn was reached"); });
      syncBuiltinESMExports();
      try {
        await assert.rejects(f.check(model, config), /requires the pinned TLC1\.7\.4 jar/);
        assert.equal(launches.length, discovery ? 2 : 0);
        const owned = path.join(f.scope, "rejected-owner");
        fs.mkdirSync(owned);
        assert.throws(() => f.prepareChecker(invalid, owned), /requires the pinned TLC1\.7\.4 jar/);
        assert.deepEqual(fs.readFileSync(model), originalModel);
        assert.deepEqual(fs.readFileSync(config), originalConfig);
        if (changed) assert.deepEqual(fs.readFileSync(invalid), sentinel);
        else assert.equal(fs.existsSync(invalid), false);
        assert.equal(fs.existsSync(path.join(f.scope, "chart.mmd")), false);
        assert.equal(fs.existsSync(path.join(f.scope, "report.md")), false);
      } finally {
        for (const key of ["JAVA", "TLA2TOOLS"] as const) {
          if (previous[key] === undefined) delete process.env[key];
          else process.env[key] = previous[key];
        }
        t.mock.restoreAll();
        syncBuiltinESMExports();
      }
    });
  }
}

for (const discovery of [false, true]) {
  test(`${discovery ? "discovered" : "explicit"} companion fails before a Java/SANY/TLC child`, async t => {
    const f = await fixture();
    t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
    const tools = path.join(f.scope, "tools");
    fs.mkdirSync(tools);
    const jar = path.join(tools, "tla2tools.jar");
    fs.copyFileSync(replayJar, jar);
    const companion = path.join(tools, "CommunityModules.jar");
    fs.writeFileSync(companion, "foreign dependency, never opened as a jar");
    const model = path.join(f.scope, "Input.tla");
    const config = path.join(f.scope, "Input.cfg");
    fs.writeFileSync(model, "---- MODULE Input ----\nVARIABLE x\nInit == x = 0\nNext == x' = x\n====\n");
    fs.writeFileSync(config, "INIT Init\nNEXT Next\nCHECK_DEADLOCK FALSE\n");
    const previous = { JAVA: process.env.JAVA, TLA2TOOLS: process.env.TLA2TOOLS };
    process.env.JAVA = "JAVA_MUST_NOT_START";
    if (discovery) delete process.env.TLA2TOOLS;
    else process.env.TLA2TOOLS = jar;
    t.mock.method(childProcess, "execFile", (file: string, args: string[], _options: unknown, callback: Function) => {
      assert.equal(file, "mise");
      assert.ok(args[0] === "where" || args[0] === "which");
      callback(null, args[0] === "where" ? tools : "JAVA_MUST_NOT_START", "");
    });
    t.mock.method(childProcess, "spawn", () => { throw new Error("Java child reached"); });
    syncBuiltinESMExports();
    try {
      await assert.rejects(f.check(model, config), /Unsupported unpinned dependency CommunityModules\.jar/);
      assert.equal(fs.readFileSync(companion, "utf8"), "foreign dependency, never opened as a jar");
    } finally {
      for (const key of ["JAVA", "TLA2TOOLS"] as const) {
        if (previous[key] === undefined) delete process.env[key];
        else process.env[key] = previous[key];
      }
      t.mock.restoreAll();
      syncBuiltinESMExports();
    }
  });
}

test("SANY keeps original includes and absolute model input from a different caller directory with spaces", async t => {
  const f = await fixture();
  t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
  const folder = path.join(f.scope, "model folder with spaces");
  fs.cpSync(path.join(import.meta.dirname, "corpus/CounterInstance"), folder, { recursive: true });
  const spec = path.join(folder, "CounterInstance.tla");
  const config = path.join(folder, "CounterInstance.cfg");
  const previous = { JAVA: process.env.JAVA, TLA2TOOLS: process.env.TLA2TOOLS };
  process.env.JAVA = "JAVA_MUST_NOT_START";
  process.env.TLA2TOOLS = f.jar;
  let observed = false;
  t.mock.method(childProcess, "spawn", (file: string, args: string[], options: { cwd: string }) => {
    assert.equal(file, "JAVA_MUST_NOT_START");
    assert.ok(args.includes(`-DTLA-Library=${folder}`));
    assert.deepEqual(args.slice(-5), ["tla2sany.xml.XMLExporter", "-o", "-I", folder, spec]);
    const isolated = args[args.indexOf("-cp") + 1];
    assert.equal(isolated, path.join(options.cwd, "checker/tla2tools.jar"));
    assert.deepEqual(fs.readFileSync(isolated), fs.readFileSync(f.jar));
    assert.deepEqual(fs.readdirSync(path.dirname(isolated)), ["tla2tools.jar"]);
    assert.notEqual(options.cwd, folder);
    assert.notEqual(process.cwd(), folder);
    observed = true;
    throw new Error("SOURCE_ONLY_ARGV_BOUNDARY");
  });
  syncBuiltinESMExports();
  try {
    await assert.rejects(f.check(spec, config), /SOURCE_ONLY_ARGV_BOUNDARY/);
    assert.equal(observed, true);
    assert.deepEqual(fs.readFileSync(spec), fs.readFileSync(path.join(import.meta.dirname, "corpus/CounterInstance/CounterInstance.tla")));
  } finally {
    for (const key of ["JAVA", "TLA2TOOLS"] as const) {
      if (previous[key] === undefined) delete process.env[key];
      else process.env[key] = previous[key];
    }
    t.mock.restoreAll();
    syncBuiltinESMExports();
  }
});
