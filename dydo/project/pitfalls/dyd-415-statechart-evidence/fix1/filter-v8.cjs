// Retain native V8 counters/maps for maintained runtime modules, not renderer dependencies.
const fs = require('node:fs');
const path = require('node:path');
const [input, output, inventory] = process.argv.slice(2);
const names = ['frontends/dot.ts', 'run.ts', 'statechart.ts', 'worker.ts', 'windows.ts', 'windows-cleanup.ts'];
const selected = url => names.some(name => url.endsWith('/skills/engineering/statechart/scripts/' + name));
fs.mkdirSync(output);
let seen = 0, kept = 0;
for (const file of fs.readdirSync(input).filter(name => name.endsWith('.json'))) {
  const data = JSON.parse(fs.readFileSync(path.join(input, file), 'utf8'));
  seen++;
  data.result = data.result.filter(row => selected(row.url));
  data['source-map-cache'] = Object.fromEntries(Object.entries(data['source-map-cache'] ?? {}).filter(([url]) => selected(url)));
  if (data.result.length) {
    fs.writeFileSync(path.join(output, file), JSON.stringify(data));
    kept++;
  }
}
fs.writeFileSync(inventory, JSON.stringify({ inputFiles: seen, retainedFiles: kept, modules: names }, null, 2) + '\n');
