const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const { spawnSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '..', '..', '..');
const script = path.join(repoRoot, 'setup-skills.mjs');

function writeSkill(root, name, { withBody = true } = {}) {
  const skillDir = path.join(root, 'skills', name);
  fs.mkdirSync(skillDir, { recursive: true });
  if (withBody) fs.writeFileSync(path.join(skillDir, 'SKILL.md'), `---\nname: ${name}\ndescription: fixture\n---\nbody\n`);
}

function runSetup(...args) {
  return spawnSync(process.execPath, [script, ...args], { cwd: os.tmpdir(), encoding: 'utf8', timeout: 10000 });
}

function inRoot(callback) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd216-setup-skills-'));
  try {
    callback(root);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

test('link creation for both host roots from a canonical skills tree', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    writeSkill(root, 'beta');

    const result = runSetup('--root', root);

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
  });
});

test('a rerun is idempotent: it creates nothing more and still exits zero', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    const first = runSetup('--root', root);
    assert.equal(first.status, 0, first.stderr);

    const second = runSetup('--root', root);

    assert.equal(second.status, 0, second.stderr);
    assert.match(second.stdout, /1 skills, 0 projections created/);
  });
});

test('refuses a non-zero exit when a target already exists as a human-owned real directory, leaving it untouched and creating nothing', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    const humanDir = path.join(root, '.claude', 'skills', 'alpha');
    fs.mkdirSync(humanDir, { recursive: true });
    const humanFile = path.join(humanDir, 'human-owned.txt');
    fs.writeFileSync(humanFile, 'do not touch');

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Refusing to replace human-owned or conflicting path/);
    assert.ok(fs.lstatSync(humanDir).isDirectory() && !fs.lstatSync(humanDir).isSymbolicLink(), 'human-owned directory was replaced');
    assert.equal(fs.readFileSync(humanFile, 'utf8'), 'do not touch');
    assert.ok(!fs.existsSync(path.join(root, '.agents', 'skills', 'alpha')), 'setup created a projection despite the refusal');
  });
});

test('refuses a link that already points somewhere other than its canonical skill', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    const elsewhere = path.join(root, 'elsewhere');
    fs.mkdirSync(elsewhere, { recursive: true });
    const hostSkills = path.join(root, '.claude', 'skills');
    fs.mkdirSync(hostSkills, { recursive: true });
    fs.symlinkSync(elsewhere, path.join(hostSkills, 'alpha'), 'junction');

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Refusing to replace human-owned or conflicting path/);
  });
});

test('refuses with a non-zero exit when a canonical skill folder has no SKILL.md', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { withBody: false });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Canonical skill is missing SKILL\.md: alpha/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the missing SKILL.md');
    assert.ok(!fs.existsSync(path.join(root, '.agents')), 'setup created host output despite the missing SKILL.md');
  });
});

test('refuses a root that has no canonical skills directory', () => {
  inRoot((root) => {
    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Missing canonical skill directory/);
  });
});

test('refuses a canonical skills directory that holds no skills', () => {
  inRoot((root) => {
    fs.mkdirSync(path.join(root, 'skills'), { recursive: true });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /No canonical skills found in/);
  });
});

test('refuses a host skill root that is not a directory', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    fs.mkdirSync(path.join(root, '.claude'), { recursive: true });
    fs.writeFileSync(path.join(root, '.claude', 'skills'), 'not a directory');

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Host skill root is not a directory/);
  });
});

test('an explicit --root confines every projection to that root', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');

    const result = runSetup('--root', root);

    assert.equal(result.status, 0, result.stderr);
    assert.ok(fs.existsSync(path.join(root, '.claude', 'skills', 'alpha')), 'the explicit root received no projection');
    assert.ok(!fs.existsSync(path.join(os.tmpdir(), 'skills')), 'setup projected relative to the working directory');
    assert.ok(!fs.existsSync(path.join(repoRoot, '.claude', 'skills')), 'setup projected into the repository');
  });
});

test('accepts a relative --root, resolving it against the working directory', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');

    const result = spawnSync(process.execPath, [script, '--root', path.basename(root)], {
      cwd: path.dirname(root), encoding: 'utf8', timeout: 10000,
    });

    assert.equal(result.status, 0, result.stderr);
    assert.ok(fs.existsSync(path.join(root, '.agents', 'skills', 'alpha')), 'the relative root received no projection');
  });
});

test('rejects an unknown flag, an incomplete --root, and any extra argument', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');

    for (const args of [['--nonsense', root], ['--root'], ['--root', ''], [root], ['--root', root, '--root', root]]) {
      const result = runSetup(...args);

      assert.notEqual(result.status, 0, `setup accepted ${JSON.stringify(args)}`);
      assert.match(result.stderr, /usage: setup-skills\.mjs \[--root <existing-directory>\]/);
    }
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'a rejected argument still produced projections');
  });
});

test('rejects a root that does not exist or is not a directory', () => {
  inRoot((root) => {
    const missing = runSetup('--root', path.join(root, 'absent'));
    assert.notEqual(missing.status, 0);
    assert.match(missing.stderr, /Root is not a directory/);

    const file = path.join(root, 'plain.txt');
    fs.writeFileSync(file, 'not a root');
    const notDirectory = runSetup('--root', file);
    assert.notEqual(notDirectory.status, 0);
    assert.match(notDirectory.stderr, /Root is not a directory/);
  });
});

test('the default root is the directory holding the script itself', () => {
  // Executing the default would project into this repository, so the default is pinned by
  // source instead: every caller (CanonicalSkillSteps, run-host-canaries) invokes the
  // script with no arguments and depends on this exact expression.
  const source = fs.readFileSync(script, 'utf8');
  assert.match(source, /if \(argv\.length === 0\) return path\.dirname\(fileURLToPath\(import\.meta\.url\)\);/);
});
