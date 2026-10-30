import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createInterface } from "node:readline";
import { test } from "node:test";
import { pathToFileURL } from "node:url";

const lifetime = pathToFileURL(path.resolve(import.meta.dirname, "../lifetime.ts")).href;
const nonce = "41504150415041504150415041504150";

for (const operation of ["release", "eof", "wrong-channel", "oversize"])
  test(`Native gate ${operation} preserves the no-work-before-GO barrier`, { timeout: 10000 }, async (context) => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-native-gate-"));
    const marker = path.join(root, "checking");
    const script = `import fs from "node:fs";
      import { gate } from ${JSON.stringify(lifetime)};
      try {
        await gate(process.argv[1]);
        fs.writeFileSync(process.argv[2], "checking", { flag: "wx" });
        process.stdout.write("checking\\n");
      } catch (error) { console.error(String(error)); process.exit(2); }`;
    const child = spawn(process.execPath, ["--disable-warning=ExperimentalWarning", "--input-type=module",
      "--eval", script, nonce, marker], { stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
    context.after(async () => {
      if (child.exitCode === null) {
        child.kill();
        await exit;
      }
      fs.rmSync(root, { recursive: true, force: true });
    });
    const exit = once(child, "close");
    const lines = createInterface({ input: child.stdout })[Symbol.asyncIterator]();
    let errors = "";
    child.stderr.setEncoding("utf8").on("data", (chunk) => { errors += chunk; });
    assert.equal((await lines.next()).value, `ready:1:${nonce}`);
    assert.equal(fs.existsSync(marker), false);
    if (operation === "release") {
      child.stdin.write(`go:1:${nonce}\n`);
      assert.equal((await lines.next()).value, "checking");
      assert.equal(fs.readFileSync(marker, "utf8"), "checking");
      child.stdin.end();
    } else if (operation === "eof") child.stdin.end();
    else child.stdin.write(operation === "oversize" ? "x".repeat(16385) : "go:1:wrong\n");
    assert.deepEqual(await exit, [2, null]);
    if (operation !== "release") {
      assert.equal(fs.existsSync(marker), false);
      assert.match(errors, /closed before GO|gate rejected|Oversize/);
    }
  });
