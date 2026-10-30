"""The CoS procedure must resolve activation friction before saying goodnight."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


class OvernightPreflightTests(unittest.TestCase):
    def test_both_transitions_preflight_then_readback_then_announce(self):
        text = (ROOT / "skills/productivity/overnight-mode/SKILL.md").read_text(encoding="utf-8")
        for heading, value, next_heading in (
            ("## Muster", "on", "## Standing orders"),
            ("## Morning brief", "off", None),
        ):
            section = text.split(heading, 1)[1]
            if next_heading:
                section = section.split(next_heading, 1)[0]
            with self.subTest(value=value):
                preflight = section.index("[approval preflight]")
                transition = section.index(f"dydo mode set overnight {value}")
                readback = section.index("dydo mode show", transition)
                announcement = section.index("announce", readback)
                self.assertLess(preflight, transition)
                self.assertLess(transition, readback)
                self.assertLess(readback, announcement)
                self.assertIn(f"overnight: {value}", section[readback:announcement])

    def test_preflight_has_early_terminal_route_and_no_ui_claim(self):
        path = ROOT / "skills/productivity/overnight-mode/resources/approval-preflight.md"
        text = path.read_text(encoding="utf-8")
        for expected in ("Codex", "default", "acceptEdits", "plan", "auto",
                         "dydo mode set overnight on", "dydo mode set overnight off",
                         "installed", "user's own terminal", "UI", "unknown"):
            self.assertIn(expected, text)
        muster = (ROOT / "skills/roles/officers/chief-of-staff/resources/muster.md").read_text(encoding="utf-8")
        self.assertLess(muster.index("approval preflight"), muster.index("Request each admiral"))
