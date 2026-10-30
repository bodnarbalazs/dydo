"""Real clean scratch checkout, relevant drift, and output-ownership rejections."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import unittest

sys.dont_write_bytecode = True
from config import MANIFEST, git, preflight, resolve

SOURCE = Path(os.environ['DYD415_PROBE_SOURCE']).resolve()
SCRATCH = Path(os.environ['DYD415_PROBE_ROOT']).resolve()
NORMAL_TEMP = Path('C:/Users/User/AppData/Local/Temp').resolve()
METRIC = SCRATCH / 'metric'
TOOLS = json.loads((SCRATCH / 'tools-map.json').read_bytes())
ROWS = []


class RecipeChecks(unittest.TestCase):
    def check(self, **overrides):
        args = dict(root=SOURCE, work=SCRATCH / 'fresh-work', out=SCRATCH / 'fresh-out',
            normal_temp=NORMAL_TEMP, metric=METRIC, tools=TOOLS)
        args.update(overrides)
        return preflight(**args)

    def commit(self, path):
        git(SOURCE, 'add', '-A', '--', str(path))
        git(SOURCE, '-c', 'user.name=DYD415 scratch proof', '-c',
            'user.email=proof@example.invalid', '-c', 'commit.gpgsign=false',
            'commit', '-m', 'Owned scratch identity probe')

    def test_clean_integrated_source(self):
        result = self.check()
        self.assertTrue(result['clean'])
        self.assertEqual(len(result['checked']), 520)
        self.assertFalse(result['measurementExecuted'])
        ROWS.append(dict(check='clean-integrated', result='accepted', head=result['head']))

    def test_changed_and_missing_each_identity_class(self):
        for group in sorted(set(MANIFEST['classes']) - {'ts-producer', 'retained-input'}):
            row = next(r for r in MANIFEST['files'] if r['group'] == group)
            path = resolve(row, SOURCE, METRIC, TOOLS)
            original = path.read_bytes()
            for mutation in ['changed', 'missing']:
                with self.subTest(group=group, mutation=mutation):
                    try:
                        if mutation == 'changed':
                            path.write_bytes(original + b'\nowned scratch drift\n')
                        else:
                            path.unlink()
                        if row['location'] == 'source':
                            self.commit(path)
                        with self.assertRaisesRegex(AssertionError,
                                ('Changed' if mutation == 'changed' else 'Missing') + ' dependency ' + group):
                            self.check()
                        ROWS.append(dict(check=group, mutation=mutation, path=str(path), result='rejected'))
                    finally:
                        path.write_bytes(original)
                        if row['location'] == 'source':
                            self.commit(path)
                    self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), row['sha256'])

    def test_new_producers_and_direct_inputs_changed_and_missing(self):
        initial_tree = git(SOURCE, 'rev-parse', 'HEAD^{tree}')
        additions = [r for r in MANIFEST['files'] if r['group'] in ['ts-producer', 'retained-input']]
        self.assertEqual(len(additions), 44)
        for row in additions:
            path = resolve(row, SOURCE, METRIC, TOOLS)
            original = path.read_bytes()
            for mutation in ['changed', 'missing']:
                with self.subTest(path=row['path'], mutation=mutation):
                    try:
                        if mutation == 'changed':
                            path.write_bytes(original + b'\nowned I1 corrected probe\n')
                        else:
                            path.unlink()
                        self.commit(path)
                        self.assertFalse(git(SOURCE, 'status', '--porcelain=v1', '--untracked-files=all'))
                        expected = ('Changed' if mutation == 'changed' else 'Missing') + ' dependency ' + row['group'] + ': ' + row['path']
                        with self.assertRaisesRegex(AssertionError, re.escape(expected)) as caught:
                            self.check()
                        ROWS.append(dict(check='new-dependency', group=row['group'], path=row['path'],
                            mutation=mutation, result='rejected', message=str(caught.exception),
                            clean=True, head=git(SOURCE, 'rev-parse', 'HEAD'),
                            expectedSha256=row['sha256'],
                            mutatedSha256=hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None))
                    finally:
                        path.write_bytes(original)
                        self.commit(path)
                    self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), row['sha256'])
                    self.assertEqual(git(SOURCE, 'rev-parse', 'HEAD^{tree}'), initial_tree)

    def test_reviewed_producer_manifest_cannot_be_partially_declared(self):
        original = MANIFEST['files']
        try:
            for row in [r for r in original if r['group'] == 'ts-producer']:
                MANIFEST['files'] = [r for r in original if r is not row]
                with self.assertRaisesRegex(AssertionError, re.escape(
                        'Incomplete reviewed TS producer closure: ' + row['path'])):
                    self.check()
                ROWS.append(dict(check='producer-manifest-completeness', path=row['path'], result='rejected'))
        finally:
            MANIFEST['files'] = original

    def test_committed_unrelated_skill_change_allowed(self):
        path = SOURCE / 'skills/engineering/verify-dydo/SKILL.md'
        original = path.read_bytes()
        try:
            path.write_bytes(original + b'\nOwned unrelated scratch change.\n')
            self.commit(path)
            self.assertTrue(self.check()['clean'])
            ROWS.append(dict(check='unrelated-skill-committed-change', result='accepted'))
        finally:
            path.write_bytes(original)
            self.commit(path)

    def test_dirty_source_rejected(self):
        path = SOURCE / 'THIRD-PARTY-NOTICES.md'
        original = path.read_bytes()
        try:
            path.write_bytes(original + b'\nOwned dirty probe\n')
            with self.assertRaisesRegex(AssertionError, 'Source must be clean'):
                self.check()
            ROWS.append(dict(check='dirty-source', result='rejected'))
        finally:
            path.write_bytes(original)

    def test_stale_work_and_evidence_rejected(self):
        for key, path in [('work', SCRATCH / 'fresh-work'), ('out', SCRATCH / 'fresh-out')]:
            path.mkdir()
            try:
                with self.assertRaisesRegex(AssertionError, 'Fresh output'):
                    self.check(**{key: path})
                ROWS.append(dict(check='stale-' + key, result='rejected'))
            finally:
                path.rmdir()

    def test_foreign_and_overlapping_outputs_rejected(self):
        cases = [dict(work=NORMAL_TEMP.parent / 'outside-normal-temp'),
            dict(out=SOURCE / 'unsafe-output'), dict(out=METRIC / 'unsafe-output'),
            dict(out=SCRATCH / 'fresh-work' / 'nested')]
        for case in cases:
            with self.subTest(case=case), self.assertRaises(AssertionError):
                self.check(**case)
            ROWS.append(dict(check='foreign-or-overlapping-output', arguments={k: str(v) for k, v in case.items()}, result='rejected'))

    def test_exact_checkout_root_rejected(self):
        with self.assertRaisesRegex(AssertionError, 'Exact checkout root'):
            self.check(root=SOURCE / 'skills')
        ROWS.append(dict(check='nested-checkout-root', result='rejected'))


if __name__ == '__main__':
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(RecipeChecks))
    clean = not git(SOURCE, 'status', '--porcelain=v1', '--untracked-files=all')
    assert clean, 'Scratch mutations must be restored'
    # Restore has its own identity check, with fresh paths still absent.
    final = preflight(SOURCE, SCRATCH / 'fresh-work', SCRATCH / 'fresh-out', NORMAL_TEMP, METRIC, TOOLS)
    (SCRATCH / 'recipe-outcomes.json').write_bytes((json.dumps(dict(
        tests=result.testsRun, failures=len(result.failures), errors=len(result.errors),
        outcomes=ROWS, scratchClean=clean, restoredDependencies=len(final['checked']),
        scratchHead=git(SOURCE, 'rev-parse', 'HEAD'), scratchTree=git(SOURCE, 'rev-parse', 'HEAD^{tree}'),
        restoredDependencyHashes=final['checked'],
        measurementExecuted=False), indent=2) + '\n').encode())
    sys.exit(0 if result.wasSuccessful() else 1)
