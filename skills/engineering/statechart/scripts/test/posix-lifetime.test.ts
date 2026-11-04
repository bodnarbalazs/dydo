import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { Duplex } from "node:stream";
import { test } from "node:test";
import { PosixControl, publishPosix } from "../posix-lifetime.ts";

function channel() {
  const sent: string[] = [];
  const stream = new Duplex({ read() {}, write(chunk, _encoding, done) { sent.push(chunk.toString()); done(); } });
  return { stream, sent, control: new PosixControl(stream) };
}

test("POSIX control waits for the complete matching native acknowledgment", async (context) => {
  const { stream, sent, control } = channel();
  context.after(() => control.close());
  let released = false;
  const attach = control.request("attach:nonce:415", "attached:nonce:415").then(() => { released = true; });
  assert.deepEqual(sent, ["attach:nonce:415\n"]);
  stream.push("attached:nonce:");
  await new Promise<void>((resolve) => setImmediate(resolve));
  assert.equal(released, false);
  stream.push("415\n");
  await attach;
  assert.equal(released, true);
});

for (const failure of ["wrong", "extra", "oversize", "eof"])
  test(`POSIX control ${failure} cannot authorize continuation`, async (context) => {
    const { stream, control } = channel();
    context.after(() => control.close());
    const request = control.request("drain:nonce", "drained:nonce");
    const rejected = assert.rejects(request, /rejected|Oversize|channel lost/);
    if (failure === "wrong") stream.push("drained:other\n");
    else if (failure === "extra") stream.push("drained:nonce\ncleaned:nonce\n");
    else if (failure === "oversize") stream.push("x".repeat(16385));
    else stream.push(null);
    await rejected;
    await assert.rejects(control.lost);
    await assert.rejects(control.request("complete:nonce", "cleaned:nonce"));
  });

test("POSIX control rejects concurrent transitions and accepts EOF only after terminal acknowledgment", async (context) => {
  const { stream, control } = channel();
  context.after(() => control.close());
  const pending = control.request("complete:nonce", "cleaned:nonce", true);
  await assert.rejects(control.request("drain:nonce", "drained:nonce"), /transition/);
  stream.push("cleaned:nonce\n");
  await pending;
  stream.push(null);
  await new Promise<void>((resolve) => setImmediate(resolve));
  await assert.rejects(control.request("attach:nonce:415", "attached:nonce:415"), /transition/);
});

test("POSIX publication preserves a concurrent nonempty destination and the staged report", (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-posix-publish-"));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const done = path.join(root, "done");
  const destination = path.join(root, "chart");
  fs.mkdirSync(done);
  fs.writeFileSync(path.join(done, "report.md"), "complete report");
  fs.mkdirSync(destination);
  fs.writeFileSync(path.join(destination, "caller-file"), "foreign");
  assert.throws(() => publishPosix(done, destination), /nonempty/);
  assert.equal(fs.readFileSync(path.join(destination, "caller-file"), "utf8"), "foreign");
  assert.equal(fs.readFileSync(path.join(done, "report.md"), "utf8"), "complete report");
});

test("POSIX publication accepts a missing destination and rejects a file destination", (context) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-posix-publish-"));
  context.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const done = path.join(root, "done");
  const destination = path.join(root, "nested", "chart");
  fs.mkdirSync(done);
  fs.writeFileSync(path.join(done, "report.md"), "complete report");
  const file = path.join(root, "caller-file");
  fs.writeFileSync(file, "foreign");
  assert.throws(() => publishPosix(done, file), /foreign or linked/);
  assert.equal(fs.readFileSync(file, "utf8"), "foreign");
  publishPosix(done, destination);
  assert.equal(fs.existsSync(done), false);
  assert.equal(fs.readFileSync(path.join(destination, "report.md"), "utf8"), "complete report");
});
