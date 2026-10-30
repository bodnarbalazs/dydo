// Merge source-location counters only after c8 converted each native platform independently.
const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const load = createRequire('C:/Users/User/AppData/Local/Temp/dyd415-fix1/assurance/package.json');
const { createCoverageMap } = load('istanbul-lib-coverage');
const { createContext } = load('istanbul-lib-report');
const reports = load('istanbul-reports');
const out = __dirname;
const coverage = createCoverageMap({});
const originals = [];
for (const platform of ['windows', 'linux']) {
  const data = JSON.parse(fs.readFileSync(path.join(out, `coverage-${platform}/coverage-final.json`), 'utf8'));
  const map = createCoverageMap(data);
  originals.push(map);
  coverage.merge(map);
}
const checked = [];
for (const file of coverage.files()) {
  const merged = coverage.fileCoverageFor(file).getLineCoverage();
  for (const original of originals) {
    const lines = original.fileCoverageFor(file).getLineCoverage();
    for (const [line, hits] of Object.entries(lines)) {
      if (hits > 0 && !(merged[line] > 0)) throw new Error(`Merge lost covered line ${file}:${line}`);
    }
  }
  checked.push({ file, lines: Object.keys(merged).length });
}
const dir = path.join(out, 'coverage-final');
const context = createContext({ dir, coverageMap: coverage });
for (const reporter of ['json', 'lcovonly', 'text']) reports.create(reporter).execute(context);
fs.writeFileSync(path.join(out, 'coverage-merge-audit.json'), JSON.stringify({ algorithm: 'istanbul-lib-coverage merge',
  rule: 'Every covered source line in either native platform remains covered', checked }, null, 2) + '\n');
