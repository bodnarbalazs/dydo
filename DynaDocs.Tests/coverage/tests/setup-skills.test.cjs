const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const { spawnSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '..', '..', '..');
const scriptSource = path.join(repoRoot, 'setup-skills.mjs');

function makeFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd91-setup-skills-'));
  fs.copyFileSync(scriptSource, path.join(root, 'setup-skills.mjs'));
  return root;
}

function writeSkill(root, name, { withBody = true } = {}) {
  const skillDir = path.join(root, 'skills', name);
  fs.mkdirSync(skillDir, { recursive: true });
  if (withBody) fs.writeFileSync(path.join(skillDir, 'SKILL.md'), `---\nname: ${name}\ndescription: fixture\n---\nbody\n`);
}

function runSetup(root) {
  return spawnSync(process.execPath, ['setup-skills.mjs'], { cwd: root, encoding: 'utf8', timeout: 10000 });
}

test('link creation for both host roots from a canonical skills tree', () => {
  const root = makeFixture();
  try {
    writeSkill(root, 'alpha');
    writeSkill(root, 'beta');

    const result = runSetup(root);

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /2 skills, 4 projections created/);
    for (const hostRoot of ['.claude', '.agents']) {
      for (const name of ['alpha', 'beta']) {
        const target = path.join(root, hostRoot, 'skills', name);
        const stats = fs.lstatSync(target);
        assert.ok(stats.isSymbolicLink(), `${target} was not created as a link`);
        assert.equal(
          fs.realpathSync(target),
          fs.realpathSync(path.join(root, 'skills', name)),
          `${target} did not resolve to its canonical skill`,
        );
      }
    }
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('a rerun is idempotent: it creates nothing more and still exits zero', () => {
  const root = makeFixture();
  try {
    writeSkill(root, 'alpha');
    const first = runSetup(root);
    assert.equal(first.status, 0, first.stderr);

    const second = runSetup(root);

    assert.equal(second.status, 0, second.stderr);
    assert.match(second.stdout, /1 skills, 0 projections created/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('refuses a non-zero exit when a target already exists as a human-owned real directory, leaving it untouched and creating nothing', () => {
  const root = makeFixture();
  try {
    writeSkill(root, 'alpha');
    const humanDir = path.join(root, '.claude', 'skills', 'alpha');
    fs.mkdirSync(humanDir, { recursive: true });
    const humanFile = path.join(humanDir, 'human-owned.txt');
    fs.writeFileSync(humanFile, 'do not touch');

    const result = runSetup(root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Refusing to replace human-owned or conflicting path/);
    assert.ok(fs.lstatSync(humanDir).isDirectory() && !fs.lstatSync(humanDir).isSymbolicLink(), 'human-owned directory was replaced');
    assert.equal(fs.readFileSync(humanFile, 'utf8'), 'do not touch');
    assert.ok(!fs.existsSync(path.join(root, '.agents', 'skills', 'alpha')), 'setup created a projection despite the refusal');
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('refuses with a non-zero exit when a canonical skill folder has no SKILL.md', () => {
  const root = makeFixture();
  try {
    writeSkill(root, 'alpha', { withBody: false });

    const result = runSetup(root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Canonical skill is missing SKILL\.md: alpha/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the missing SKILL.md');
    assert.ok(!fs.existsSync(path.join(root, '.agents')), 'setup created host output despite the missing SKILL.md');
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
