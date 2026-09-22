const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const { spawnSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '..', '..', '..');
const script = path.join(repoRoot, 'setup-skills.mjs');

function writeSkill(root, name, { category = 'cat', withBody = true } = {}) {
  const skillDir = path.join(root, 'skills', category, name);
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

test('link creation for both host roots from a canonical skills tree, walking categories to any depth by the SKILL.md rule', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    writeSkill(root, 'beta', { category: 'productivity' });
    writeSkill(root, 'gamma', { category: 'roles/crew' });
    // A skill's own subfolders are never walked: resources/ inside a skill is not a category.
    fs.mkdirSync(path.join(root, 'skills', 'engineering', 'alpha', 'resources'), { recursive: true });

    const result = runSetup('--root', root);

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /3 skills, 6 projections created/);
    const categories = { alpha: 'engineering', beta: 'productivity', gamma: 'roles/crew' };
    for (const hostRoot of ['.claude', '.agents']) {
      assert.deepEqual(fs.readdirSync(path.join(root, hostRoot, 'skills')).sort(), ['alpha', 'beta', 'gamma'],
        `${hostRoot}/skills must hold one flat entry per skill and no category entry`);
      for (const name of ['alpha', 'beta', 'gamma']) {
        const target = path.join(root, hostRoot, 'skills', name);
        const stats = fs.lstatSync(target);
        assert.ok(stats.isSymbolicLink(), `${target} was not created as a link`);
        assert.equal(
          fs.realpathSync(target),
          fs.realpathSync(path.join(root, 'skills', categories[name], name)),
          `${target} did not resolve to its canonical skill, projected FLAT with no category level`,
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

test('migrates a dangling link left by the old flat layout to the current canonical category path', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    const hostSkillsClaude = path.join(root, '.claude', 'skills');
    const hostSkillsAgents = path.join(root, '.agents', 'skills');
    fs.mkdirSync(hostSkillsClaude, { recursive: true });
    fs.mkdirSync(hostSkillsAgents, { recursive: true });
    // What the old, pre-category setup would have created: a link straight at skills/alpha, which
    // no longer exists now that the skill lives under skills/engineering/alpha (DYD-219), leaving
    // both links dangling.
    fs.symlinkSync(path.join(root, 'skills', 'alpha'), path.join(hostSkillsClaude, 'alpha'), 'junction');
    fs.symlinkSync(path.join(root, 'skills', 'alpha'), path.join(hostSkillsAgents, 'alpha'), 'junction');

    const result = runSetup('--root', root);

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /1 skills, 2 projections created/);
    for (const hostRoot of ['.claude', '.agents']) {
      const target = path.join(root, hostRoot, 'skills', 'alpha');
      assert.ok(fs.lstatSync(target).isSymbolicLink(), `${target} was not migrated to a link`);
      assert.equal(
        fs.realpathSync(target),
        fs.realpathSync(path.join(root, 'skills', 'engineering', 'alpha')),
        `${target} did not resolve to the current canonical category path`,
      );
    }
  });
});

test('refuses a foreign link whose target lies entirely outside the canonical skills tree', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha');
    const foreign = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd225-setup-skills-foreign-'));
    try {
      const hostSkills = path.join(root, '.claude', 'skills');
      fs.mkdirSync(hostSkills, { recursive: true });
      fs.symlinkSync(foreign, path.join(hostSkills, 'alpha'), 'junction');

      const result = runSetup('--root', root);

      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /Refusing to replace human-owned or conflicting path/);
      assert.ok(!fs.existsSync(path.join(root, '.agents', 'skills', 'alpha')), 'setup created a projection despite the refusal');
    } finally {
      fs.rmSync(foreign, { recursive: true, force: true });
    }
  });
});

test('a rerun after migrating a dangling flat-layout link creates nothing further and stays idempotent', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    const hostSkills = path.join(root, '.claude', 'skills');
    fs.mkdirSync(hostSkills, { recursive: true });
    fs.symlinkSync(path.join(root, 'skills', 'alpha'), path.join(hostSkills, 'alpha'), 'junction');

    const first = runSetup('--root', root);
    assert.equal(first.status, 0, first.stderr);
    assert.match(first.stdout, /projections created/);

    const second = runSetup('--root', root);

    assert.equal(second.status, 0, second.stderr);
    assert.match(second.stdout, /1 skills, 0 projections created/);
    const target = path.join(root, '.claude', 'skills', 'alpha');
    assert.ok(fs.lstatSync(target).isSymbolicLink(), `${target} was not left as a link after the second run`);
    assert.equal(
      fs.realpathSync(target),
      fs.realpathSync(path.join(root, 'skills', 'engineering', 'alpha')),
      `${target} did not still resolve to the correct canonical category path after the second run`,
    );
  });
});

test('re-points a link left by the previous category layout to the skill\'s nested category, then stays idempotent', () => {
  inRoot((root) => {
    // alpha's old category is gone entirely; beta's old category still exists, holding gamma.
    writeSkill(root, 'alpha', { category: 'roles/crew' });
    writeSkill(root, 'beta', { category: 'roles/officers' });
    writeSkill(root, 'gamma', { category: 'engineering' });
    const former = { alpha: path.join(root, 'skills', 'orchestration', 'alpha'), beta: path.join(root, 'skills', 'engineering', 'beta') };
    for (const hostRoot of ['.claude', '.agents']) {
      fs.mkdirSync(path.join(root, hostRoot, 'skills'), { recursive: true });
      for (const [name, target] of Object.entries(former)) fs.symlinkSync(target, path.join(root, hostRoot, 'skills', name), 'junction');
    }

    const first = runSetup('--root', root);

    assert.equal(first.status, 0, first.stderr);
    assert.match(first.stdout, /3 skills, 6 projections created/);
    const categories = { alpha: 'roles/crew', beta: 'roles/officers', gamma: 'engineering' };
    for (const hostRoot of ['.claude', '.agents']) {
      for (const [name, category] of Object.entries(categories)) {
        const target = path.join(root, hostRoot, 'skills', name);
        assert.ok(fs.lstatSync(target).isSymbolicLink(), `${target} was not left as a link`);
        assert.equal(fs.realpathSync(target), fs.realpathSync(path.join(root, 'skills', category, name)),
          `${target} did not resolve to its nested canonical category`);
      }
    }

    const second = runSetup('--root', root);

    assert.equal(second.status, 0, second.stderr);
    assert.match(second.stdout, /3 skills, 0 projections created/);
  });
});

test('refuses a folder that holds neither SKILL.md nor a subfolder, as an empty category', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { withBody: false });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /No canonical skills found in .*cat[\\/]alpha/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the missing SKILL.md');
    assert.ok(!fs.existsSync(path.join(root, '.agents')), 'setup created host output despite the missing SKILL.md');
  });
});

test('refuses an empty nested category, even when its sibling category is populated', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'roles/crew' });
    fs.mkdirSync(path.join(root, 'skills', 'roles', 'officers'), { recursive: true });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /No canonical skills found in .*roles[\\/]officers/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the empty category');
  });
});

test('refuses the same skill name in a nested category and a flat one, naming both category paths', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    writeSkill(root, 'alpha', { category: 'roles/crew' });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Duplicate skill name across categories: alpha \(engineering and roles\/crew\)/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the duplicate name');
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

test('refuses a category directory that holds no skills, even when another category is populated', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    fs.mkdirSync(path.join(root, 'skills', 'empty-category'), { recursive: true });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /No canonical skills found in .*empty-category/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the empty category');
  });
});

test('refuses two categories that both claim the same skill name', () => {
  inRoot((root) => {
    writeSkill(root, 'alpha', { category: 'engineering' });
    writeSkill(root, 'alpha', { category: 'productivity' });

    const result = runSetup('--root', root);

    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Duplicate skill name across categories: alpha/);
    assert.ok(!fs.existsSync(path.join(root, '.claude')), 'setup created host output despite the duplicate name');
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
    // A sibling tmpdir stands in for "somewhere else": proving nothing leaked there is the same
    // property as proving nothing leaked into the real checkout, but it holds regardless of
    // whether this checkout happens to have .claude/skills or .agents/skills installed already
    // (asserting against repoRoot itself made this test fail in any checkout where the documented
    // `node setup-skills.mjs` had already been run).
    const elsewhere = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd219-setup-skills-elsewhere-'));
    try {
      const result = runSetup('--root', root);

      assert.equal(result.status, 0, result.stderr);
      assert.ok(fs.existsSync(path.join(root, '.claude', 'skills', 'alpha')), 'the explicit root received no projection');
      assert.ok(!fs.existsSync(path.join(os.tmpdir(), 'skills')), 'setup projected relative to the working directory');
      assert.deepEqual(fs.readdirSync(elsewhere), [], 'setup leaked something into a directory other than the explicit root');
    } finally {
      fs.rmSync(elsewhere, { recursive: true, force: true });
    }
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

test('with no arguments the root is the script\'s own directory, never the working directory', () => {
  inRoot((root) => {
    // Running the canonical file with no arguments would project into this checkout, so the default
    // is exercised on a copy. The working directory below carries its own canonical skills tree: a
    // root taken from cwd would succeed there and project 'decoy' instead of 'alpha'.
    fs.copyFileSync(script, path.join(root, 'setup-skills.mjs'));
    writeSkill(root, 'alpha');
    const elsewhere = path.join(root, 'workdir');
    writeSkill(elsewhere, 'decoy');

    const result = spawnSync(process.execPath, [path.join(root, 'setup-skills.mjs')], {
      cwd: elsewhere, encoding: 'utf8', timeout: 10000,
    });

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /1 skills, 2 projections created/);
    for (const hostRoot of ['.claude', '.agents']) {
      const target = path.join(root, hostRoot, 'skills', 'alpha');
      assert.ok(fs.existsSync(target) && fs.lstatSync(target).isSymbolicLink(), `${target} was not created beside the script`);
      assert.equal(fs.realpathSync(target), fs.realpathSync(path.join(root, 'skills', 'cat', 'alpha')));
      assert.ok(!fs.existsSync(path.join(elsewhere, hostRoot)), `setup rooted ${hostRoot} at the working directory`);
    }
  });
});
