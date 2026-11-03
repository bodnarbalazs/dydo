import cp from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { syncBuiltinESMExports } from "node:module";
if (process.argv[1] === "/toolchain/repo/skills/engineering/statechart/scripts/statechart.ts") {
  const mode = "worker-spawn-error";
  const record = (value) => fs.appendFileSync("/output/janitor-events/janitor-proof-UtsVqd/events.jsonl", JSON.stringify({ parent: process.pid, ...value }) + "\n");
  const spawn = cp.spawn;
  cp.spawn = (file, args, options) => {
    const role = args?.includes("/toolchain/repo/skills/engineering/statechart/scripts/worker.ts") ? "worker"
      : file === "/bin/sh" && args?.length === 6 && args[2] === "sh" ? "janitor" : undefined;
    const missing = role === "worker" && mode === "worker-spawn-error"
      || role === "janitor" && mode.startsWith("janitor-spawn-error");
    const child = spawn(missing ? "/statechart-proof-missing-executable" : file, args, options);
    if (role) {
      record({ event: "spawn", role, pid: child.pid, file: missing ? "/statechart-proof-missing-executable" : file });
      child.on("error", (error) => record({ event: "error", role, pid: child.pid, code: error.code }));
      child.once("exit", (code, signal) => record({ event: "exit", role, pid: child.pid, code, signal }));
      child.once("close", (code, signal) => record({ event: "close", role, pid: child.pid, code, signal }));
      if (role === "janitor" && mode === "early-close") child.kill("SIGKILL");
      if (role === "janitor" && mode === "kill-error") {
        const kill = child.kill.bind(child);
        child.kill = (signal) => { kill(signal); throw new Error("injected janitor kill error"); };
      }
    }
    return child;
  };
  const remove = fs.rmSync;
  fs.rmSync = (file, options) => {
    if (mode.includes("cleanup-error") && path.basename(String(file)).startsWith(".out.statechart-")) {
      record({ event: "cleanup-error", file: String(file) });
      throw new Error("injected stage cleanup error");
    }
    const result = remove(file, options);
    record({ event: "cleanup-return", file: String(file) });
    return result;
  };
  const rename = fs.renameSync;
  fs.renameSync = (from, to) => {
    if (path.basename(String(from)) === "done") {
      if (mode === "output-collision") {
        fs.mkdirSync(to);
        fs.writeFileSync(path.join(to, "report.md"), "caller owned\n");
      }
      record({ event: "publish", file: String(to) });
    }
    return rename(from, to);
  };
  const write = process.stdout.write.bind(process.stdout);
  process.stdout.write = (...args) => { record({ event: "stdout" }); return write(...args); };
  process.once("exit", (code) => record({ event: "cli-exit", code }));
  syncBuiltinESMExports();
}
