// Scope-specific use of the repository's unchanged ESLint/SonarJS producer.
const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const { execFileSync } = require('node:child_process');
const root = process.cwd();
const out = path.join(root, 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1');
const skill = path.join(root, 'skills/engineering/statechart');
const upstream = 'C:/Users/User/AppData/Local/Temp/dyd415-upstream/plugins-1ba91fc25c0977741dd0e267849e6234b037ed0a/pstack/skills/statechart';
const toolRoot = 'C:/Users/User/AppData/Local/Temp/dyd415-fix1/assurance';
const loadTool = createRequire(path.join(toolRoot, 'package.json'));
const { analyze, moduleMetrics } = loadTool('./js_metrics.cjs');
const manifest = JSON.parse(fs.readFileSync(path.join(out, '../source-manifest.json'), 'utf8'));
const files = [...manifest.vendored_files.filter(r =>
  require('node:crypto').createHash('sha256').update(fs.readFileSync(path.join(skill, r.path))).digest('hex') !== r.upstream_sha256), ...manifest.added_files]
  .map(r => r.path).filter(p => p.endsWith('.ts'));
for (const file of fs.readdirSync(path.join(skill, 'scripts/test'))) {
  if (file.startsWith('adaptation') && file.endsWith('.ts')) files.push(`scripts/test/${file}`);
}
const rows = [];
function moduleSource(source, methods) {
  let offset = 0;
  const parts = [];
  for (const method of [...methods].sort((a, b) => a.start - b.start || b.end - a.end)) {
    if (method.start < offset) continue;
    parts.push(source.slice(offset, method.start), '<callable>');
    offset = method.end;
  }
  return parts.join('') + source.slice(offset);
}
for (const file of [...new Set(files)]) {
  const source = fs.readFileSync(path.join(skill, file), 'utf8');
  const original = fs.existsSync(path.join(upstream, file)) ? fs.readFileSync(path.join(upstream, file), 'utf8') : '';
  const walked = analyze(source, 'typescript');
  const old = original ? analyze(original, 'typescript') : null;
  for (const method of walked.methods) {
    method.vendorIdentical = !!old?.methods.some(other => original.slice(other.start, other.end) === source.slice(method.start, method.end));
  }
  walked.tokenCount = walked.tokens.length;
  delete walked.tokens;
  rows.push({ path: file, authored: !original, ...walked,
    moduleVendorIdentical: !!old && moduleSource(source, walked.methods) === moduleSource(original, old.methods),
    module: moduleMetrics(source, 'typescript'),
    upstreamModule: original ? moduleMetrics(original, 'typescript') : null });
}
fs.writeFileSync(path.join(out, 'walked.json'), JSON.stringify(rows, null, 2) + '\n');
const before = execFileSync('git', ['show', '53df22e:skills/engineering/statechart/scripts/statechart.ts'], { encoding: 'utf8', windowsHide: true });
fs.writeFileSync(path.join(out, 'complexity-red.json'), JSON.stringify({
  source: '53df22e002a0e5c5e4429cc6908e0b01d4fb6ab3', path: 'scripts/statechart.ts',
  before: moduleMetrics(before, 'typescript'), after: rows.find(r => r.path === 'scripts/statechart.ts').module,
  threshold: 20,
}, null, 2) + '\n');
for (const row of rows) {
  const local = row.methods.filter(m => !m.vendorIdentical);
  console.log(JSON.stringify({ path: row.path, methods: row.methods.length, local: local.length, module: row.module,
    findings: local.filter(m => m.cognitive > 20 || m.parameters > 7), diagnostics: row.diagnostics,
    maxCognitive: Math.max(0, ...local.map(m => m.cognitive)), maxParameters: Math.max(0, ...local.map(m => m.parameters)) }));
}
