import fs from "node:fs";
fs.watch("/output/signal-SIGINT/proof", () => {});
fs.writeFileSync("/output/signal-SIGINT/proof/ready", "ready");
fs.appendFileSync("/output/signal-SIGINT/events.jsonl", JSON.stringify({ observer: "view", event: "ready-created", pid: process.pid, monotonicNs: String(process.hrtime.bigint()) }) + "\n");
await new Promise(() => {});
