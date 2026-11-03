import fs from "node:fs";
fs.watch("/output/janitor-events/janitor-proof-0SyEwL", () => {});
fs.writeFileSync("/output/janitor-events/janitor-proof-0SyEwL/ready", "janitor-proof-0SyEwL", { flag: "wx" });
await new Promise(() => {});
