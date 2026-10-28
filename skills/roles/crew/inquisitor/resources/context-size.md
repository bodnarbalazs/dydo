# Context size

An inquisition crew agent reads its own context size after each area in its queue, and hands the
rest of the queue back once it is past its limit. The limit is 110k tokens unless the commission
set another, and your brief states it. The rule is for inquisition crew only: you run long and
autonomous, and nothing compacts your context for you. The threshold is a tunable for today's
models, not a law.

## Install the command once

Save this script to a temporary file outside the repository, for example
`<temp dir>/context-size.mjs`, so it never enters a commit:

```js
import fs from 'node:fs'; import path from 'node:path'; import os from 'node:os';
const e = process.env, last = (f, pick) => fs.readFileSync(f, 'utf8').trim().split('\n').reverse().map(l => { try { return pick(JSON.parse(l)); } catch { return undefined; } }).find(v => v !== undefined);
const find = (dir, id) => { for (const d of fs.readdirSync(dir, { withFileTypes: true })) { const p = path.join(dir, d.name); if (d.isDirectory()) { const r = find(p, id); if (r) return r; } else if (d.name.includes(id) && d.name.endsWith('.jsonl')) return p; } };
if (e.CLAUDE_CODE_SESSION_ID) {
  const root = path.join(e.CLAUDE_CONFIG_DIR || path.join(os.homedir(), '.claude'), 'projects');
  const u = last(find(root, e.CLAUDE_CODE_SESSION_ID), j => j.message?.usage);
  console.log(u.input_tokens + u.cache_creation_input_tokens + u.cache_read_input_tokens);
} else if (e.CODEX_THREAD_ID) {
  const root = path.join(e.CODEX_HOME || path.join(os.homedir(), '.codex'), 'sessions');
  console.log(last(find(root, e.CODEX_THREAD_ID), j => j.payload?.type === 'token_count' ? j.payload.info.last_token_usage.input_tokens : undefined));
} else console.log('unknown host');
```

Claude Code: the script reads the session transcript that `CLAUDE_CODE_SESSION_ID` names under
`CLAUDE_CONFIG_DIR`, and adds the last assistant entry's input, cache-creation and cache-read
tokens. Codex: it reads the rollout log whose file name carries `CODEX_THREAD_ID` under
`CODEX_HOME`, and prints the last `token_count` event's input tokens.

## Run it after each area

```bash
node <temp dir>/context-size.mjs
```

The number is the model's input at its last call, so it trails your newest work by one call: read
it after each area, never once at the start. Then:

- **Under the limit:** start the next area.
- **Over the limit:** stop. The area you just documented is your last; return with every area not
  yet started listed as handed back. The captain sends a fresh agent for the rest.
- **`unknown host`, or an error:** say so in the return's context field and keep working the queue;
  the captain sizes your next queue smaller.
