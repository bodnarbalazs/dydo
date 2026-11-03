import fs from "node:fs";
fs.watch("/output/janitor-events/janitor-proof-DnvIBL", () => {});
fs.writeFileSync("/output/janitor-events/janitor-proof-DnvIBL/ready", "ready");
await new Promise(() => {});
