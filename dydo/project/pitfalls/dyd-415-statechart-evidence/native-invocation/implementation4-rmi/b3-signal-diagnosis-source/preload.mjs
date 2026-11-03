// Scratch-only observation; never installed in the product or used to fabricate signal delivery.
import cp from "node:child_process";
import fs from "node:fs";
import { syncBuiltinESMExports } from "node:module";

const cli = "/toolchain/repo/skills/engineering/statechart/scripts/statechart.ts";
if (process.argv[1] === cli) {
  const log = (value) => fs.appendFileSync(process.env.STATECHART_DIAG_EVENTS,
    JSON.stringify({ monotonicNs: String(process.hrtime.bigint()), observer: "cli", parent: process.pid, ...value }) + "\n");
  const stat = (pid) => {
    try {
      const raw = fs.readFileSync(`/proc/${pid}/stat`, "utf8");
      const fields = raw.slice(raw.lastIndexOf(") ") + 2).split(" ");
      return { raw, starttime: fields[19], ppid: Number(fields[1]), pgid: Number(fields[2]), session: Number(fields[3]) };
    } catch (error) { return { unavailable: error.code }; }
  };
  const on = process.on;
  process.on = function (name, listener) {
    if (name !== "SIGINT" && name !== "SIGTERM") return on.call(this, name, listener);
    log({ event: "handler-attached", signal: name, identity: stat(process.pid) });
    return on.call(this, name, function (...args) {
      log({ event: "handler-entry", signal: name, identity: stat(process.pid) });
      return listener.apply(this, args);
    });
  };
  const spawn = cp.spawn;
  const remove = fs.rmSync;
  fs.rmSync = (file, options) => {
    const result = remove(file, options);
    log({ event: "cleanup-return", file: String(file) });
    return result;
  };
  cp.spawn = (file, args, options) => {
    const child = spawn(file, args, options);
    const role = args?.includes("/toolchain/repo/skills/engineering/statechart/scripts/worker.ts") ? "worker"
      : file === "/bin/sh" && args?.length === 6 && args[2] === "sh" ? "janitor" : "other";
    log({ event: "spawn", role, pid: child.pid, file, args, identity: child.pid ? stat(child.pid) : null });
    child.on("error", (error) => log({ event: "error", role, pid: child.pid, code: error.code, message: error.message }));
    child.once("exit", (code, signal) => log({ event: "exit", role, pid: child.pid, code, signal }));
    child.once("close", (code, signal) => log({ event: "close", role, pid: child.pid, code, signal }));
    return child;
  };
  process.once("exit", (code) => log({ event: "cli-exit", code }));
  syncBuiltinESMExports();
}
