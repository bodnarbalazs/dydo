import fs from "node:fs";
import { setTimeout } from "node:timers/promises";

const [worker, stage, scratch] = process.argv.slice(2);
for await (const _ of process.stdin) { /* EOF follows normal close or parent death. */ }
for (let attempt = 0; attempt < 100; attempt++) {
  try {
    process.kill(Number(worker), 0);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ESRCH") throw error;
    for (const dir of [stage, scratch]) fs.rmSync(dir, { recursive: true, force: true, maxRetries: 10, retryDelay: 50 });
    process.exit(0);
  }
  await setTimeout(50);
}
console.error("Windows worker did not terminate; scratch files were retained.");
process.exit(2);
