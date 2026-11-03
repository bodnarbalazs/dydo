import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { checkerJar, pinnedTlc, prepareChecker } from "../checker.ts";

// Real pinned checker DATA, never a JVM. No synthetic hash replaces the product pin.
const input = pinnedTlc(path.resolve(process.env.TLA2TOOLS ?? "missing-pinned-TLC-fixture.jar"));
function fixture() {
  const scope = fs.realpathSync.native(fs.mkdtempSync(path.join(os.tmpdir(), "statechart-checker-")));
  const source = path.join(scope, "input with spaces");
  const owned = path.join(scope, "owned");
  fs.mkdirSync(source);
  fs.mkdirSync(owned);
  const jar = path.join(source, "original.jar");
  fs.copyFileSync(input, jar);
  return { scope, jar, owned, companion: path.join(source, "CommunityModules.jar") };
}

test("an owned checker copy retains every original jar byte including its manifest", t => {
  const f = fixture();
  t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
  const before = fs.readFileSync(f.jar);
  const checker = prepareChecker(f.jar, f.owned);
  assert.equal(checkerJar(checker), path.join(f.owned, "checker/tla2tools.jar"));
  assert.deepEqual(fs.readFileSync(checker.jar), before);
  assert.deepEqual(fs.readFileSync(f.jar), before);
  assert.deepEqual(fs.readdirSync(path.dirname(checker.jar)), ["tla2tools.jar"]);
});

for (const kind of ["file", "directory", "dangling-link", "file-link", "directory-link"]) {
  test(`${kind} companion is rejected and all foreign inputs retained`, t => {
    const f = fixture();
    t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
    const target = path.join(f.scope, "foreign");
    if (kind === "file") fs.writeFileSync(f.companion, "foreign companion");
    else if (kind === "directory") fs.mkdirSync(f.companion);
    else {
      if (kind === "file-link") fs.writeFileSync(target, "foreign target");
      if (kind === "directory-link") fs.mkdirSync(target);
      fs.symlinkSync(target, f.companion, kind === "directory-link" ? "dir" : "file");
    }
    const before = fs.readFileSync(f.jar);
    assert.throws(() => pinnedTlc(f.jar), /Unsupported unpinned dependency CommunityModules\.jar.*original\.jar/);
    assert.throws(() => prepareChecker(f.jar, f.owned), /CommunityModules\.jar/);
    assert.deepEqual(fs.readdirSync(f.owned), []);
    assert.deepEqual(fs.readFileSync(f.jar), before);
    assert.ok(fs.lstatSync(f.companion));
    if (kind === "file") assert.equal(fs.readFileSync(f.companion, "utf8"), "foreign companion");
    if (kind === "file-link") assert.equal(fs.readFileSync(target, "utf8"), "foreign target");
  });
}

test("linked file and directory inputs resolve the actual jar and inspect its companion", t => {
  const f = fixture();
  t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
  const alias = path.join(f.scope, "alias.jar");
  const directory = path.join(f.scope, "alias-directory");
  fs.symlinkSync(f.jar, alias, "file");
  fs.symlinkSync(path.dirname(f.jar), directory, "dir");
  for (const jar of [alias, path.join(directory, "original.jar")]) assert.equal(pinnedTlc(jar), f.jar);
  fs.writeFileSync(f.companion, "foreign");
  for (const jar of [alias, path.join(directory, "original.jar")]) assert.throws(() => pinnedTlc(jar), /CommunityModules\.jar/);
  assert.equal(fs.readFileSync(f.companion, "utf8"), "foreign");
});

for (const change of ["source", "destination", "missing", "extra", "destination-link", "directory-link", "ancestor-link", "late-companion"]) {
  test(`${change} invalidates an already prepared checker before launch`, t => {
    const f = fixture();
    t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
    const checker = prepareChecker(f.jar, f.owned);
    const directory = path.dirname(checker.jar);
    if (change === "source") fs.appendFileSync(f.jar, "changed");
    if (change === "destination") fs.appendFileSync(checker.jar, "changed");
    if (change === "missing") fs.unlinkSync(checker.jar);
    if (change === "extra") fs.writeFileSync(path.join(directory, "foreign"), "sentinel");
    if (change === "late-companion") fs.writeFileSync(f.companion, "sentinel");
    if (change === "destination-link") { fs.unlinkSync(checker.jar); fs.symlinkSync(f.jar, checker.jar, "file"); }
    if (change === "directory-link" || change === "ancestor-link") {
      const original = change === "directory-link" ? directory : f.owned;
      fs.renameSync(original, original + "-retained");
      fs.symlinkSync(original + "-retained", original, "dir");
    }
    assert.throws(() => checkerJar(checker));
    if (change === "extra") assert.equal(fs.readFileSync(path.join(directory, "foreign"), "utf8"), "sentinel");
  });
}

test("stale output and a linked owned root cannot be reused or overwritten", t => {
  const f = fixture();
  t.after(() => fs.rmSync(f.scope, { recursive: true, force: true }));
  fs.mkdirSync(path.join(f.owned, "checker"));
  const sentinel = path.join(f.owned, "checker/foreign");
  fs.writeFileSync(sentinel, "retain");
  assert.throws(() => prepareChecker(f.jar, f.owned));
  assert.equal(fs.readFileSync(sentinel, "utf8"), "retain");
  const alias = path.join(f.scope, "owned-alias");
  fs.symlinkSync(f.owned, alias, "dir");
  assert.throws(() => prepareChecker(f.jar, alias), /linked/);
});
