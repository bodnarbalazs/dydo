# Third-Party Notices

## Matt Pocock's Skills

Several dydo skills contain adaptations of material from
[mattpocock/skills](https://github.com/mattpocock/skills), pinned at commit
`6654f6b60cd9d5be8b54c6fafe44346dabeb3b76`. Each adapted file repeats the same attribution in its own
header in the canonical `skills/<category>/<name>/` folders (`roles/officers/`, `roles/crew/`, `engineering/`,
`productivity/`).

| Upstream skill | Adapted in |
|---|---|
| `code-review` | `skills/roles/crew/reviewer/resources/code.md` |
| `codebase-design` | `skills/engineering/codebase-design/SKILL.md`, `skills/engineering/codebase-design/resources/deepening.md`, `skills/engineering/codebase-design/resources/design-it-twice.md` |
| `diagnosing-bugs` | `skills/engineering/diagnosing-bugs/SKILL.md` |
| `domain-modeling` | `skills/engineering/domain-modeling/SKILL.md` |
| `grill-me` | `skills/productivity/grill-me/SKILL.md` |
| `grilling` | `skills/productivity/grilling/SKILL.md` |
| `handoff` | `skills/productivity/handoff/SKILL.md` |
| `improve-codebase-architecture` | `skills/engineering/improve-codebase-architecture/SKILL.md`, `skills/engineering/improve-codebase-architecture/resources/html-report.md` |
| `prototype` | `skills/engineering/prototype/SKILL.md`, `skills/engineering/prototype/resources/logic.md`, `skills/engineering/prototype/resources/ui.md` |
| `research` | `skills/roles/crew/research/SKILL.md` |
| `retro` | `skills/productivity/self-improvement/SKILL.md` |
| `tdd` | `skills/roles/crew/code-writer/SKILL.md`, `skills/roles/crew/code-writer/resources/tests.md`, `skills/roles/crew/reviewer/resources/code.md` |
| `teach` | `skills/productivity/teach/SKILL.md`, `skills/productivity/teach/resources/mission-format.md`, `skills/productivity/teach/resources/glossary-format.md`, `skills/productivity/teach/resources/learning-record-format.md`, `skills/productivity/teach/resources/resources-format.md` |
| `to-spec` | `skills/productivity/to-project/SKILL.md` |
| `to-tickets` | `skills/productivity/to-issue/SKILL.md` |
| `wizard` and its `template.sh` | `skills/engineering/wizard/SKILL.md`, `skills/engineering/wizard/resources/template.md` |
| `wait-what` | `skills/productivity/bro/SKILL.md` |
| `wayfinder` | `skills/productivity/wayfinder/SKILL.md` |
| `writing-for-agents` | `skills/productivity/writing-for-agents/SKILL.md` |
| `writing-for-agents/SKILL-MECHANICS` | `skills/productivity/writing-for-agents/resources/skill-mechanics.md` |

MIT License

Copyright (c) 2026 Matt Pocock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## HumanLayer's Skills

One dydo skill contains an adaptation of material from
[humanlayer/skills](https://github.com/humanlayer/skills), pinned at commit
`6ab9013a10c28f5046f7f999549cd5328a0b30d7`. The adapted file repeats the attribution in its own
header.

| Upstream skill | Adapted in |
|---|---|
| `show-me` | `skills/productivity/show-me/SKILL.md` |

MIT License

Copyright (c) 2026 HumanLayer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Cursor's pstack

One dydo skill contains an adaptation of material from
[cursor/plugins](https://github.com/cursor/plugins), `pstack`, pinned at commit
`7314f723a487ec406b6369fe5865ba034cfed166`. The adapted file repeats the attribution in its own
header.

| Upstream skill | Adapted in |
|---|---|
| `unslop` | `skills/productivity/writing-for-humans/SKILL.md` |

MIT License

Copyright (c) 2026 Lauren Tan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Map viewer bundle

The `dydo map` viewer, built from `viewer/package.json` with the versions pinned in
`viewer/pnpm-lock.yaml`, bundles these packages into the browser code it serves.

| Package | License | Copyright |
|---|---|---|
| [React](https://github.com/facebook/react) (`react`, `react-dom`, `scheduler`) | MIT | Copyright (c) Meta Platforms, Inc. and affiliates. |
| [use-sync-external-store](https://github.com/facebook/react) | MIT | Copyright (c) Meta Platforms, Inc. and affiliates. |
| [React Flow](https://github.com/xyflow/xyflow) (`@xyflow/react`, `@xyflow/system`) | MIT | Copyright (c) 2019-2025 webkid GmbH |
| [zustand](https://github.com/pmndrs/zustand) | MIT | Copyright (c) 2019 Paul Henschel |
| [classcat](https://github.com/jorgebucaran/classcat) | MIT | Copyright (c) Jorge Bucaran |
| [d3-color, d3-dispatch, d3-drag, d3-ease, d3-interpolate, d3-selection, d3-timer, d3-transition, d3-zoom](https://github.com/d3/d3) | ISC | Copyright 2010-2022 Mike Bostock |
| [elkjs](https://github.com/kieler/elkjs) | EPL-2.0 (chosen from EPL-2.0 OR GPL-3.0-or-later) | Copyright (c) 2019 TypeFox and others |

### MIT License (React, use-sync-external-store, React Flow, zustand, classcat)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

### ISC License (d3 modules)

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.

### Eclipse Public License 2.0 (elkjs)

elkjs is distributed under the Eclipse Public License 2.0, whose full text is at
https://www.eclipse.org/legal/epl-2.0/. The bundle carries elkjs unmodified; its source code is
available from https://github.com/kieler/elkjs (release 0.12.0 is the one the lockfile pins) and from
the npm registry at https://www.npmjs.com/package/elkjs.
