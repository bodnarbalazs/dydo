import { type ChildProcess, spawn } from "node:child_process";
import path from "node:path";

// A Job Object owns the worker and every Java child, including on abrupt CLI death.
// The worker waits on stdin until the watchdog has assigned it to the job.
export async function guardWorker(worker: ChildProcess, stage: string, tmp: string) {
  const guard = spawn("powershell.exe", [
    "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
    "-File", path.join(import.meta.dirname, "windows-job.ps1"),
    String(worker.pid),
  ], { windowsHide: true, stdio: ["pipe", "pipe", "inherit"] });
  // Windows may terminate an attached PowerShell host with its console owner.
  // The job still kills its members; a detached Node janitor removes their files.
  const janitor = spawn(process.execPath, [
    ...process.execArgv, path.join(import.meta.dirname, "windows-cleanup.ts"),
    String(worker.pid), stage, tmp,
  ], { detached: true, windowsHide: true, stdio: ["pipe", "ignore", "inherit"] });
  const cleaned = new Promise<void>((resolve, reject) => {
    janitor.once("error", reject);
    janitor.once("close", code => code === 0 ? resolve() : reject(new Error(`Windows cleanup exited ${code}.`)));
  });
  const closed = new Promise<void>((resolve) => guard.once("close", () => resolve()));
  try {
    await new Promise<void>((resolve, reject) => {
      guard.once("error", reject);
      guard.once("exit", (code) => reject(new Error(`Windows job guard exited ${code} before ready.`)));
      guard.stdout!.once("data", (data: Buffer) => {
        if (data.toString().trim() === "ready") resolve();
        else reject(new Error(`Windows job guard: ${data}`));
      });
    });
    worker.stdin!.end("go");
  } catch (error) {
    worker.kill();
    guard.stdin!.end();
    await closed;
    janitor.stdin!.end();
    await cleaned;
    throw error;
  }
  return async () => {
    if (!guard.stdin!.writableEnded) guard.stdin!.end();
    await closed;
    if (!janitor.stdin!.writableEnded) janitor.stdin!.end();
    await cleaned;
  };
}
