import fs from "node:fs";
fs.watch("/output/janitor-events/janitor-proof-TPlyTW", () => {});
fs.writeFileSync("/output/janitor-events/janitor-proof-TPlyTW/ready", "ready");
await new Promise(() => {});
