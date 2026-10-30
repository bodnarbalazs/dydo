"""Payload resource identity and source closure using genuine unittest discovery."""
from pathlib import Path
import importlib.util
import shutil
import tempfile
import unittest
import xml.etree.ElementTree as ET

SOURCE = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location('_dyd415_invocation_prepare', SOURCE /
    'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation2/prepare.py')
prepare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(prepare)


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.scope = tempfile.TemporaryDirectory(prefix='dyd415-prepare-test-')
        self.addCleanup(self.scope.cleanup)
        self.root = Path(self.scope.name)
        names = ['setup-skills.mjs', 'skills/engineering/statechart/LICENSE',
            'skills/engineering/statechart/scripts/statechart', 'skills/engineering/statechart/scripts/run.ts',
            'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/commission/dependency-lock.json']
        for name in names:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(SOURCE / name, target)

    def test_manifest_and_msbuild_items_bind_same_exact_raw_resources(self):
        manifest = prepare.declare(self.root, [])
        target = self.root / 'Resources/Statechart'
        items = ET.parse(target / 'payload.items.props')
        embedded = {item.attrib['Include']: item.attrib['LogicalName'] for item in items.findall('.//EmbeddedResource')}
        self.assertEqual({row['path']: 'Statechart/payload/' + row['path'] for row in manifest['files']}, embedded)
        for row in manifest['files']:
            content = (self.root / row['path']).read_bytes()
            self.assertEqual(len(content), row['bytes'])
            self.assertEqual(prepare.sha(content), row['sha256'])
        before = (target / 'payload.json').read_bytes()
        prepare.declare(self.root, ['--items-only'])
        self.assertEqual(before, (target / 'payload.json').read_bytes())
        with self.assertRaises(FileExistsError):
            prepare.declare(self.root, [])

    def test_changed_missing_and_extra_payload_source_cannot_regenerate_expectations(self):
        prepare.declare(self.root, [])
        target = self.root / 'skills/engineering/statechart/scripts/run.ts'
        original = target.read_bytes()
        target.write_bytes(b'changed')
        with self.assertRaisesRegex(AssertionError, 'from drift'):
            prepare.declare(self.root, ['--items-only'])
        target.unlink()
        with self.assertRaisesRegex(AssertionError, 'from drift'):
            prepare.declare(self.root, ['--items-only'])
        target.write_bytes(original)
        (target.parent / 'extra.ts').write_bytes(b'export const extra = true;')
        with self.assertRaisesRegex(AssertionError, 'from drift'):
            prepare.declare(self.root, ['--items-only'])

    def test_test_corpus_and_config_are_not_runtime_payload(self):
        for name in ['scripts/test/test.ts', 'scripts/corpus/view.ts', 'scripts/tsconfig.json', 'scripts/node_modules/pkg/index.ts']:
            target = self.root / 'skills/engineering/statechart' / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b'not a runtime resource')
        rows = prepare.payload_inputs(self.root)
        self.assertEqual(4, len(rows))
        self.assertIn('setup-skills.mjs', {row['path'] for row in rows})


if __name__ == '__main__':
    unittest.main()
