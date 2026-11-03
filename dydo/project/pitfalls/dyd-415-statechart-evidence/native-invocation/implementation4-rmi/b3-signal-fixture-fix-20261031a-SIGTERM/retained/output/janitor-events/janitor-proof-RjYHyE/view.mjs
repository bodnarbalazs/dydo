import fs from "node:fs";
fs.watch("/output/janitor-events/janitor-proof-RjYHyE", () => {});
fs.writeFileSync("/output/janitor-events/janitor-proof-RjYHyE/ready", "janitor-proof-RjYHyE", { flag: "wx" });
await new Promise(() => {});
