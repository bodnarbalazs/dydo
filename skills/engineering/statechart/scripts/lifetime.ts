import process from "node:process";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { once } from "node:events";

export async function worker(script: string, configuration: string): Promise<number> {
  const nonce = randomBytes(16).toString("hex");
  const child = spawn(process.execPath, [...process.execArgv, script, nonce, configuration, "worker"],
    { stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
  const closed = once(child, "close");
  child.stderr.pipe(process.stderr, { end: false });
  await new Promise<void>((resolve, reject) => {
    let frame = "";
    const timeout = setTimeout(() => reject(new Error("Statechart worker readiness timed out.")), 30000);
    const fail = () => { clearTimeout(timeout); reject(new Error("Statechart worker exited before READY.")); };
    const data = (chunk: Buffer) => {
      frame += chunk.toString("utf8");
      if (frame.length > 16384) { clearTimeout(timeout); reject(new Error("Oversize statechart worker READY.")); }
      if (!frame.includes("\n")) return;
      clearTimeout(timeout);
      child.stdout.off("data", data);
      child.off("close", fail);
      if (frame !== `ready:1:${nonce}\n`) reject(new Error("Statechart worker channel rejected."));
      else resolve();
    };
    child.once("close", fail);
    child.once("error", (error) => { clearTimeout(timeout); reject(error); });
    child.stdout.on("data", data);
  });
  child.stdout.pipe(process.stderr, { end: false });
  child.stdin.write(`go:1:${nonce}\n`);
  const [code] = await closed;
  return typeof code === "number" ? code : 2;
}

export async function gate(nonce: string): Promise<void> {
  if (!/^[a-f0-9]{32}$/.test(nonce)) throw new Error("Invalid native statechart channel.");
  process.stdout.write(`ready:1:${nonce}\n`);
  await new Promise<void>((resolve, reject) => {
    let frame = "";
    const data = (chunk: Buffer) => {
      frame += chunk.toString("utf8");
      if (frame.length > 16384) reject(new Error("Oversize native statechart control frame."));
      if (!frame.includes("\n")) return;
      process.stdin.off("data", data);
      if (frame !== `go:1:${nonce}\n`) reject(new Error("Native statechart ownership gate rejected."));
      else resolve();
    };
    process.stdin.on("data", data);
    process.stdin.once("end", () => reject(new Error("Native owner closed before GO.")));
  });
  // Only the native owner keeps this writer; EOF is never installation consent.
  process.stdin.once("end", () => process.exit(2));
  process.stdin.on("data", () => process.exit(2));
}
