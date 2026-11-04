namespace DynaDocs.Tests;

// The exact commands the 3.6.0 session retro found the guard misreading (DYD-406), copied byte for byte
// from their original transcripts: Claude's Bash or PowerShell tool input, or the cmd a Codex exec ran.
// GS-0N names the retro item each one reproduces.
public static class GuardRetroCommands
{
    // GS-01, Codex on Windows (PowerShell): a plan read through $p=Get-Content, then sliced.
    public const string Gs01SlicedRead = "$p=Get-Content dydo/project/plans/ripple-selection.md; $p[210..460]; Get-Content dydo/guides/working-tree-contract.md; Get-Content dydo/understand/about.md; Get-Content dydo/understand/architecture.md; $s=Get-Content dydo/reference/linear-workspace-standard.md; $n=($s|Select-String '^## Communication').LineNumber; $s[($n-1)..([Math]::Min($s.Length-1,$n+85))]; git rev-parse HEAD";

    // GS-01, Codex on Windows (PowerShell): a standard read through $lines=Get-Content, then Select-String and a slice.
    public const string Gs01SelectStringRead = "Get-Content dydo/understand/about.md; Get-Content dydo/understand/architecture.md; Get-Content dydo/guides/coding-standards.md; $lines=Get-Content dydo/reference/linear-workspace-standard.md; $start=($lines | Select-String '^## Communication and evidence').LineNumber; $lines[($start-1)..($lines.Length-1)]";

    // GS-02, Codex (PowerShell): Set-Content -LiteralPath with a literal here-string, round 1b evidence.
    public const string Gs02SetContentEvidence =
        "Set-Content -LiteralPath 'C:\\Users\\User\\AppData\\Local\\Temp\\dyd-385\\evidence-r1b-codex.txt' -Encoding utf8 -Value @'\n" +
        "DYD-385 docs review, round 1b\n" +
        "Reviewer/model: gpt-6.1-sol\n" +
        "Contract: DYD-385 description as of 2026-10-26T12:52:32.544Z, quoted in brief-r1.md.\n" +
        "Candidate: 8ccc1c3cccd707d1837e690e8c5eb6669ac28b5d\n" +
        "Base: d55af133904909ca6b35aa3e22a7093dae78c61c\n" +
        "\n" +
        "Read-only review used the ticket worktree and git show/git diff against the pinned commits. Loaded reviewer (docs), writing-for-agents and skill-mechanics, required project context and writing-docs. Reviewed all seven changed paths. No candidate files or Linear objects were changed.\n" +
        "\n" +
        "git diff --check d55af133 8ccc1c3c: exit 0.\n" +
        "Invocation metadata, README classification, both glossary copies, and control-flow User commands row agree. Candidate skill is 147 lines, below the 168-line base. Four comparison categories remain. README Install the skills names skills/, setup-skills.mjs and THIRD-PARTY-NOTICES.md. setup-skills.mjs defines both projection folders and preserves foreign entries. Changelog has the requested wording changes and one DYD-384 / PR #181 Changed bullet.\n" +
        "\n" +
        "Finding 1 source, candidate:\n" +
        "skills/productivity/update-dydo/SKILL.md, step 4: dydo init all in an empty temp directory 'writes only there and asks nothing'.\n" +
        "Commands/InitCommand.cs: ExecuteInit invokes PrintInitSummary, which invokes ShellCompletionInstaller.Install().\n" +
        "Services/ShellCompletionInstaller.cs: DetectShell returns the user's .bashrc/.bash_profile, .zshrc, or Documents/PowerShell/Microsoft.PowerShell_profile.ps1. InstallToProfile appends completions or migrates an existing PowerShell completion block in that profile. Therefore the empty-directory condition does not constrain all writes to the temp directory. Source inspection establishes the claim is false without modifying a user profile.\n" +
        "\n" +
        "Finding 2 source, candidate:\n" +
        "skills/productivity/update-dydo/SKILL.md, step 3: 'Old equals new ... still run steps 7 and 8.'\n" +
        "Step 4 creates the reference installation. What to compare explicitly defines its reference as step 4's install; step 8 requires every comparison category to be walked. Thus an already-current invocation can skip reference creation and still reach proof/adopt with an undefined comparison baseline. This branch must create the reference before activation/proof.\n" +
        "\n" +
        "Finding 3 source, comparison:\n" +
        "git show d55af133:skills/productivity/update-dydo/SKILL.md contains explicit host minimum-depth/concurrency settings, full per-host hook matchers, memory switches including memories.generate_memories, and a memory-write guard denial proof.\n" +
        "Candidate skill removes these details. Candidate changelog ## Upgrade steps (steps 1-12, lines 200-242) includes the host marker, native-memory off, Stop-hook removal, entry migration, memory-folder cleanup, trust and activation, but omits those minimum-depth/concurrency checks, complete matchers and memory-switch/denial checks. Contract item 2 requires removed release-specific items to be retained in the 3.5.0 Upgrade steps, not lost or placed only under Changed.\n" +
        "\n" +
        "Runtime gate status:\n" +
        "Created own detached worktree C:/Users/User/AppData/Local/Temp/dyd-385-rv-codex-r1b at the exact candidate. git rev-parse HEAD confirmed the full SHA above.\n" +
        "Attempted cheap discovery before focused execution:\n" +
        "dotnet test DynaDocs.Tests --filter 'FullyQualifiedName~CanonicalSkill' --list-tests --verbosity quiet\n" +
        "PreToolUse guard rejected it before execution:\n" +
        "BLOCKED: Use the worktree-isolated test runner instead: python DynaDocs.Tests/coverage/run_tests.py DynaDocs.Tests --filter 'FullyQualifiedName~CanonicalSkill' --list-tests --verbosity quiet\n" +
        "This avoids DLL lock contention when multiple agents run tests concurrently.\n" +
        "(Run the same command again to proceed anyway.).\n" +
        "Per the brief's explicit stop-on-guard-block instruction, did not retry, change runner or execute further gates. Independent CanonicalSkill and dydo check gates: 0/2 completed. Writer's reported 33/33 and check exit 0 remain writer evidence, not independently rerun proof.\n" +
        "Removed the detached worktree with git worktree remove, exit 0. No edit probes ran.\n" +
        "'@";

    // GS-02, Codex (PowerShell): Set-Content -LiteralPath with a literal here-string, round 2 verdict.
    public const string Gs02SetContentVerdict =
        "Set-Content -LiteralPath 'C:\\Users\\User\\AppData\\Local\\Temp\\dyd-385\\verdict-r2-codex.md' -Encoding utf8 -Value @'\n" +
        "FAIL - rubric/type: docs; contract DYD-385 description as of 2026-10-26T12:52:32.544Z; candidate ef9779dd; base d55af133; reviewer/model: gpt-6.1-sol; gates 0/2 independently verified.\n" +
        "\n" +
        "1. Where: Review verification; wrong: the pinned candidate, whole diff, round-1 fix diff, and ledger against master's skill could not be independently verified. Literal absolute-path Git commands against the ticket worktree returned \"fatal: cannot change to 'C:\\Users\\User\\Desktop\\Projects\\DynaDocs\\.claude\\worktrees\\dyd-385': Permission denied\". I stopped that probe without routing around the restriction; no detached worktree was created and neither gate was rerun. Why: readable working-tree files and writer-reported CanonicalSkill 33/33 / dydo check exit 0 cannot establish the required exact-candidate review, base comparison, or complete preservation of master's lessons. Required correction: complete the pinned Git comparisons and ledger reconciliation, and verify candidate-bound gate evidence in an environment where literal absolute-path Git access succeeds; use a separate detached worktree for any editing probes. Owner: invoking officer / next reviewer.\n" +
        "\n" +
        "Evidence: [round-2 brief](C:/Users/User/AppData/Local/Temp/dyd-385/brief-r2.md), [writer ledger](C:/Users/User/AppData/Local/Temp/dyd-385/ledger-r1.md), and direct reads of the ticket worktree's skill, metadata, catalogs, changelog, README, setup script, InitCommand, ScaffoldTree, and ShellCompletionInstaller. The source audit found no additional content defect: invocation controls and catalogs agree, the skill is 154 lines, required changelog bullets are present, and the readable changes address all six round-1 findings. This FAIL records incomplete verification, not a demonstrated candidate defect. Candidate files and Linear were untouched; no edit probes or review worktrees were created.\n" +
        "'@";

    // GS-02, Claude Bash: a slot script written through cat and a quoted heredoc, then bash -n.
    public const string Gs02CatSlotScript =
        "cat > /c/Users/User/AppData/Local/Temp/p-lc-45-watch/slot-lc1270.sh <<'EOF'\n" +
        "#!/usr/bin/env bash\n" +
        "# LC-1270 slot: backend and microservices fast suites with gap_check, in t3-2e43a1f2.\n" +
        "cd /c/Users/User/.t3/worktrees/LC/t3-2e43a1f2 || exit 99\n" +
        "echo \"START $(date -u +%FT%TZ) sha=$(git rev-parse HEAD) dirty=$(git status --porcelain | wc -l)\"\n" +
        "t0=$(date +%s)\n" +
        "python tools/coverage/run_tests.py --stack=backend\n" +
        "echo \"BACKEND_RUN_TESTS_EXIT=$? at $(date -u +%FT%TZ) elapsed=$(( $(date +%s)-t0 ))s\"\n" +
        "python tools/coverage/gap_check.py --strict --skip-tests --stack=backend\n" +
        "echo \"BACKEND_GAP_CHECK_EXIT=$? at $(date -u +%FT%TZ)\"\n" +
        "t1=$(date +%s)\n" +
        "python tools/coverage/run_tests.py --stack=microservices\n" +
        "echo \"MICRO_RUN_TESTS_EXIT=$? at $(date -u +%FT%TZ) elapsed=$(( $(date +%s)-t1 ))s\"\n" +
        "python tools/coverage/gap_check.py --strict --skip-tests --stack=microservices\n" +
        "echo \"MICRO_GAP_CHECK_EXIT=$? at $(date -u +%FT%TZ)\"\n" +
        "echo \"END $(date -u +%FT%TZ) total=$(( $(date +%s)-t0 ))s dirty_after=$(git status --porcelain | wc -l)\"\n" +
        "git status --porcelain\n" +
        "EOF\n" +
        "bash -n /c/Users/User/AppData/Local/Temp/p-lc-45-watch/slot-lc1270.sh && echo ok";

    // GS-02, Claude Bash: sed -i with backticks in single quotes, then a multi-line commit message.
    public const string Gs02SedBacktickCommit =
        "sed -i 's/periodic suite (a campaign `--stage` run, unfiltered/periodic suite (unfiltered/' skills/roles/officers/issue-captain/SKILL.md && git diff --stat && git commit -qam \"P-LC-31: drop the retired campaign --stage run from the issue-captain skill's periodic-suite list\n" +
        "\n" +
        "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\" && git log -1 --format=%h";

    // GS-02, Claude Bash, same session: single-line sed -i with backticks in single quotes.
    public const string Gs02SedBacktickSingleLine = "sed -i 's#repository path `tools/ci_runner/host/README.md` points#repository path `infra/ci/README.md` points#' dydo/guides/ripple-e1-activation.md; git diff dydo/guides/ripple-e1-activation.md | grep '^[+-]' ; git add infra/ci/README.md infra/ci/lc1108-linux-preflight.sh infra/ci/lc1108-windows-preparation.sh dydo/guides/ripple-e1-activation.md; git status --short | head";

    // GS-02, Claude Bash: evidence appended through cat >> and a quoted heredoc holding backticks.
    public const string Gs02CatAppendEvidence =
        "cat >> C:/Users/User/.t3/worktrees/LC/LC.worktrees/LC-749-typed-admission/dydo/agents/workspace/LC-749/fix13/evidence.md << 'EOF'\n" +
        "\n" +
        "| Mutant | First run (tests of `db28423de`) | Second run (`933977aad`), targeted reruns (`45f7fa912`) |\n" +
        "|---|---|---|\n" +
        "| no computed key named (finding 1 reverted) | route oracle imprecise 10, member oracle imprecise 40; table 40 | same |\n" +
        "| the route reads a computed member by its own type only | table 2 (literal members by a key) | table 2 |\n" +
        "| a key's events not evaluated after its read | table 2 | member oracle **unsound 3**; table 2 |\n" +
        "| a path alias whose keys wrote its receiver is a view | table 2 | member oracle **unsound 8**; table 2 |\n" +
        "| a key that calls or converts an object is named | member oracle counts only | table 4 (operand calls, operand comma conversion) |\n" +
        "| a union key named by its first member | member oracle **unsound 14**; table 4 | member oracle **unsound 10**; table 4 |\n" +
        "| a key named by its outer layer only | table 4 | table 6 |\n" +
        "| B3 reads only the key's outer layer | table 1 | table 2 (as string, as any) |\n" +
        "| B3 sees no wrapper around the callee | table 1 (position test) | table 4 (wrapped callees) |\n" +
        "| B3 refuses only a key naming one handler | table 8 | table 8 |\n" +
        "| B3 decided before the route | D1 1; table 1 | D1 1; table 1 |\n" +
        "\n" +
        "Every mutant is caught; the compiler directory is identical to HEAD after each run.\n" +
        "\n" +
        "### Oracle (`listModel.test.ts`)\n" +
        "\n" +
        "- List algebra: unchanged, 0 unsound, 0 imprecise.\n" +
        "- Route oracle: 3,020 programs (was 2,480), 129 with no concrete run, 19,649 V8 runs, 1,000 programs a run writes a\n" +
        "  refused name on, 2,854 checked exactly: **0 unsound, 0 imprecise, 0 failed**. Added (seed 1253, after every earlier\n" +
        "  family, which stays as it was): lists read twice through a member or an element of what holds them by a computed key\n" +
        "  that names it, in each of ten forms (a constant, a literal-typed let, an as-const object's member, parentheses,\n" +
        "  satisfies, `!`, a restating as, a comma's last operand after a literal and after a write of another binding, an alias):\n" +
        "  checked exactly through a member 26 + 89, an element 31 + 77; per form 13–40.\n" +
        "- Member oracle (new, code r11's shape): a union-typed binding or this, each alternative a callee f and an operand a, the\n" +
        "  call reading each in place, by a literal key, a parameter key, each computed form above, or a key that writes the\n" +
        "  binding another value of its type after reading it; directly or through a path alias; V8 runs every object of every\n" +
        "  alternative (and every object as the value a key writes). 572 programs, 2,322 runs, 332 writing: **0 unsound,\n" +
        "  0 imprecise, 0 failed**; 15 R-d programs (a call in the key, a key an assertion retypes) refused with no run writing,\n" +
        "  checked sound only. Every proved read in each position 14 times beside each other one, per writer; of this 31 + 36;\n" +
        "  through a path alias 95 + 73; a path alias of a key that writes the binding, over every set of alternatives, 30 + 30.\n" +
        "- On 11dffaca2's compiler: route oracle 0 unsound, 10 imprecise; member oracle 0 unsound, 40 imprecise (`oracle-red-\n" +
        "  11dffaca2.txt`).\n" +
        "\n" +
        "### Red and green (`redgreen-final.log`, `redgreen-final-summary.txt`)\n" +
        "\n" +
        "HEAD's test files against the compiler of 11dffaca2 and of base through `fix13/swap.mjs` (sources restored after each,\n" +
        "test files never written), Vitest JSON reports:\n" +
        "- Unit (`compileCreatorScript`, `listModel`, `routeRules`, `instantiationLimit`): 1,212 discovered on each; passing base\n" +
        "  773, 11dffaca2 1,140, candidate 1,212. Red on 11dffaca2 and green now: **72** (r11's twelve writer witnesses and the\n" +
        "  wider key forms, both writers; the literal members by a key; the handler table: r11's twelve checks, union keys,\n" +
        "  receivers, wrappers, optional forms, assertions; the position test; both oracles).\n" +
        "- R1 (`tease-runtime-primitives`, `-sandbox`, `-step-transform`): 361 discovered on each; passing base 198, 11dffaca2\n" +
        "  335, candidate 361. Red on 11dffaca2 and green now: **26** (R1.32's ten admitted programs, results shown under real\n" +
        "  SES, and its R-b row, which 11dffaca2 refuses; R1.33's fifteen handler refusals with no worker).\n" +
        "- The controls (revealing keys, barriers, R-a keys, the LC-1285 and r6–r10 rows) pass on both.\n" +
        "\n" +
        "### Regressions\n" +
        "\n" +
        "- `lib/tease-runtime` suites with Istanbul: 3,186/3,186 on the hop SHA (`coverage-lib-final.log`): the adversarial\n" +
        "  table (r6–r10, LC-1285 rows included), conformance (`routeRules.test.ts`), the binding oracle (`runtimeBindings.test.ts`,\n" +
        "  1,545 cases), D1 (`instantiationLimit.test.ts`, 20, one added), the lib table and creator lib, boundary.\n" +
        "- r11's matrix: 53/55 (the two left are its destructured (c) note); its plain rewrites 28/28 admitted.\n" +
        "- `tsc -p tsconfig.app.json`: 0 errors. Strict ESLint over every changed file under `src/`: 0.\n" +
        "\n" +
        "### Prechecks (on the hop SHA)\n" +
        "\n" +
        "- `hop6/hcrap-precheck.mjs` over 950 compiler callables: none over HCRAP or cognitive 20 (highest 19.0, `routes.ts`\n" +
        "  `visitInOrder`, unchanged; the changed `typedRefusal` 11.0, `readOf` 11.0, `readEvents` 10.0).\n" +
        "- LC052 ownership replay (`fix10/owned-check-all.mjs` on `collect-product-complexity.mjs`'s output): the 378 callables\n" +
        "  of the three changed files each own a statement or collapse.\n" +
        "- Coverage: `readIdentity.ts` 99.1 % statements / 97.8 % branches, `routes.ts` 99.5 / 97.2, `admission.ts` 99.1 / 99.6.\n" +
        "\n" +
        "### Cost against 11dffaca2\n" +
        "\n" +
        "- `fix10/cost-pair.mjs` (2,646 sources, interleaved per source, best of five), beside another worktree's single-lane\n" +
        "  `tools/test.py --suite light --stack tools`: total ×1.0036 and ×1.0072 (5,902 → 5,923 ms; 5,830 → 5,872 ms); median\n" +
        "  ratio over sources of 1 ms or more ×1.0019 and ×1.0016 (p25 0.965, p75 1.033 / 1.036). Null pair (11dffaca2 against\n" +
        "  itself): ×0.9908, median ×0.9945.\n" +
        "- `cost-keys.mjs`: one call reading one union-typed binding N times, linear in N on both: in place 10.5 / 10.5 ms at\n" +
        "  N = 160; by a constant key 11.9 / 9.5 ms; by a key under three wrappers 20.4 / 14.8 ms (candidate / 11dffaca2), about\n" +
        "  0.04 ms a read for typing each key layer once.\n" +
        "EOF\n" +
        "echo appended";

    // GS-03, Claude Bash: a process listing whose PowerShell -match text names dotnet test.
    public const string Gs03ProcessListing = "powershell -NoProfile -Command 'Get-Date -Format HH:mm:ss; $cut=(Get-Date).AddMinutes(-40); Get-CimInstance Win32_Process | Where-Object { $_.CreationDate -gt $cut -or ([string]$_.CommandLine) -match \"pytest|testhost|vitest|phasecount|ci_runner|dotnet test|playwright test\" } | Where-Object { $_.Name -notmatch \"conhost|git|bash|sh.exe\" } | ForEach-Object { $c=[string]$_.CommandLine; if($c.Length -gt 300){$c=$c.Substring(0,300)}; \"{0,-6} ppid={1,-6} {2,-14} start={3:HH:mm:ss} cpu={4,6:N0}s  {5}\" -f $_.ProcessId,$_.ParentProcessId,$_.Name,$_.CreationDate,(($_.KernelModeTime+$_.UserModeTime)/1e7),$c }'";

    // GS-03, Codex (PowerShell): an rg pattern whose alternation names cs2ts.
    public const string Gs03RgPattern = "git ls-files dydo/reference; Get-Content dydo/reference/agent-permissions.md | Select-Object -First 16; Get-Content dydo/reference/mutation-equivalence-registry.md | Select-Object -First 16; Get-Content dydo/reference/test-metadata.md | Select-Object -First 18; rg -n '(coverage-tools|test-metadata|mutation-testing|cs2ts|agent-permissions|tools\\.md)' --glob '!dydo/project/**' --glob '!archive/**' --glob '!architecture.checkpoints/**' --glob '!*.stackdump' --glob '!**/pnpm-lock.yaml' dydo/reference/_reference.md setup-skills.mjs tools skills .github dydo/guides | Select-Object -First 70; Get-Command dydo,python,node,gh | Select-Object Name,Source";

    // GS-03, Claude Bash: a double-quoted PowerShell process listing whose -match names dotnet test.
    public const string Gs03QuotedProcessListing = "powershell -NoProfile -Command \"Get-CimInstance Win32_Process | Where-Object { \\$_.CommandLine -match 'test\\.py|pytest|dotnet test|vitest' } | Select-Object ProcessId,CreationDate,CommandLine | Format-List\" 2>&1 | head -40";

    // GS-03, Claude PowerShell: a process listing whose -match names dotnet test.
    public const string Gs03PowerShellProcessListing = "[math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB,1); (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average; Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'run_tests.py|vitest|dotnet test|testhost' -and $_.Name -notmatch 'bash' } | ForEach-Object { $_.Name + ' ' + $_.CommandLine.Substring(0,[Math]::Min(150,$_.CommandLine.Length)) }";

    // GS-03, Codex (PowerShell): git grep patterns whose alternation names npm.
    public const string Gs03GitGrepPattern = "git grep -n -i -E 'pnpm.*control-lc.*(test|test:heavy)|control-lc.*pnpm.*test|control-lc.*test:heavy' 14d900b7d -- ':!archive/**' ':!dydo/project/evidence/**'; git grep -n -E 'pnpm (run )?test|npm (run )?test|test:heavy' 14d900b7d -- tools/control-lc; rg -n -A 9 '^def _current_use' tests/contract/auth/test_legacy_password_tokens.py; git show 39284b9ab:tools/contracts/author_password_blind_vectors.py | Select-String -Pattern '^def _is_retirement_identifier' -Context 0,18; git status --porcelain=v1";

    // GS-03, Claude Bash: a log grep whose alternation names dotnet test.
    public const string Gs03LogGrep = "for l in full-raw-1 full-adapter-1; do echo \"### $l\"; grep -c \"\" dydo/agents/workspace/lc-1267/hop2/runs/$l/meta.txt; grep -E \"^== |exit=\" dydo/agents/workspace/lc-1267/hop2/runs/$l/meta.txt; grep -iE \"pytest|test\\.py|run_tests|gap_check|dotnet test|vitest|stryker|testhost\" dydo/agents/workspace/lc-1267/hop2/runs/$l/meta.txt | cut -c1-230; done";

    // GS-04, Codex (PowerShell): Python test declarations whose bodies are pass, on python's stdin.
    public const string Gs04PreflightDeclarations =
        "@'\n" +
        "from pathlib import Path\n" +
        "p = Path('tools/ci_runner/tests/suite/test_agent_test_command.py')\n" +
        "p.write_text(p.read_text(encoding='utf-8') + '''\n" +
        "\n" +
        "# LC-1182 acceptance cases; bodies are filled with the implementation.\n" +
        "@QUALITY\n" +
        "def test_preflight_refuses_unreachable_docker_before_collection():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_refuses_missing_browser_executables_before_collection():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_checks_stack_harness_prerequisites_before_collection():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_refuses_temp_inside_any_checkout_or_node_modules():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_refuses_unwritable_temp_and_scratch():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_refuses_low_space_temp_and_scratch():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_checks_only_selected_routes_plus_ripple():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_preflight_passes_run_and_plan_without_repairing_the_host():\n" +
        "    pass\n" +
        "''', encoding='utf-8')\n" +
        "'@ | python -";

    // GS-04, Codex (PowerShell): pre-edit evidence and Python declarations whose bodies are pass, on python's stdin.
    public const string Gs04LightLanesDeclarations =
        "@'\n" +
        "from pathlib import Path\n" +
        "import json, subprocess, datetime\n" +
        "root=Path.cwd(); scratch=root/'dydo/agents/workspace/lc-1184'; scratch.mkdir(parents=True,exist_ok=True)\n" +
        "def git(*args): return subprocess.check_output(['git',*args],text=True).strip()\n" +
        "assert git('branch','--show-current')=='LC-1184-balanced-tools-light'\n" +
        "assert Path(git('rev-parse','--show-toplevel')).resolve()==root.resolve()\n" +
        "subprocess.run(['git','merge-base','--is-ancestor','027b634593ab28cd92d56ccee0fbf3546c0a5ecb','HEAD'],check=True)\n" +
        "assert not git('status','--porcelain')\n" +
        "owned=['tools/test.py','tools/ci_runner/light_lanes.py','tools/ci_runner/ripple.py','tools/ci_runner/pytest_adapter.py','tools/ci_runner/tests/suite/test_agent_test_command.py','tools/ci_runner/tests/suite/test_ci_ripple.py','tools/ci_runner/tests/suite/test_ci_pytest_adapter.py','tools/ci_runner/tests/suite_ts/test_vitest_adapter.py','tools/ci_runner/tests/suite_ts/fixtures/vitest.sample.config.mjs','tools/ci_runner/tests/suite_ts/fixtures/vitest.import-cause.config.mjs','tools/coverage/tests/fixtures/vitest_worker_crash/vitest.config.mjs','tools/coverage/tests/test_vitest_worker_crash.py','dydo/guides/testing-strategy.md']\n" +
        "(scratch/'pre-edit.json').write_text(json.dumps({'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'branch':git('branch','--show-current'),'root':str(root),'head':git('rev-parse','HEAD'),'base_ancestor':True,'clean':True,'owned_paths':owned,'merges':git('log','-2','--format=%H %P %s').splitlines()},indent=2))\n" +
        "p=root/owned[4]\n" +
        "with p.open('a',encoding='utf-8',newline='\\n') as f:\n" +
        " f.write('''\n" +
        "\n" +
        "# LC-1184 acceptance cases: authored before the implementation.\n" +
        "@QUALITY\n" +
        "def test_light_lanes_balance_fallback_weights_and_manifest_ties():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_unknown_global_and_transitive_resources_are_safe():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_overlap_and_join_in_manifest_collection_order():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_serial_retains_manifest_order_without_workers():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_default_is_restricted_and_usage_accepts_only_one_or_two():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_plan_is_read_only_and_records_phase_times():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_drain_started_routes_after_native_or_history_failure():\n" +
        "    pass\n" +
        "\n" +
        "\n" +
        "@QUALITY\n" +
        "def test_light_lanes_prepare_first_native_import_on_the_coordinator():\n" +
        "    pass\n" +
        "''')\n" +
        "print((scratch/'pre-edit.json').read_text())\n" +
        "'@ | python -";

    // GS-04, Claude Bash: a compound cleanup that greps the protocol for a quoted PASS verdict.
    public const string Gs04ReviewVerdictGrep = "rm -rf dydo/agents/workspace/LC-1195/review2; rmdir dydo/agents/workspace/LC-1195 2>/dev/null; git status --short; git rev-parse HEAD; sed -n '/^## Communication and evidence/,/^## [^C]/p' dydo/reference/linear-workspace-standard.md | grep -n -A12 -i \"PASS\\b\\|PASS —\\|PASS -\" | head -60";

    // GS-05, Claude Bash: a protected file and its Scaffold twin rewritten by a Python program on stdin.
    public const string Gs05ProtectedRewrite =
        "W=C:/Users/User/Desktop/Projects/DynaDocs/.claude/worktrees/dyd-373; cd $W; python - <<'EOF'\n" +
        "new=(\"the user owns their content. dydo's own commands still rewrite what they manage - `dydo index`\\n\"\n" +
        "\"overwrites `dydo/index.md` with a fixed navigation page, and `dydo init`, `dydo fix` and\\n\"\n" +
        "\"`dydo adopt` write `dydo.json` - so the tier stops hand edits, not dydo's tooling. Whitelist entries do not apply\\n\")\n" +
        "for p,mid in [('dydo/files-off-limits.md','`dydo fix`, `dydo template update` write'),('Scaffold/dydo/files-off-limits.md','`dydo fix` writes')]:\n" +
        "    raw=open(p,encoding='utf-8',newline='').read(); crlf='\\r\\n' in raw; s=raw.replace('\\r\\n','\\n')\n" +
        "    old=(\"the user owns their content. dydo's own commands still rewrite what they manage - `dydo index`\\n\"\n" +
        "         \"regenerates `dydo/index.md`, and \"+mid+\"\\n\"\n" +
        "         \"`dydo.json` - so the tier stops hand edits, not dydo's tooling. Whitelist entries do not apply\\n\")\n" +
        "    assert s.count(old)==1,p\n" +
        "    s=s.replace(old,new)\n" +
        "    if crlf: s=s.replace('\\n','\\r\\n')\n" +
        "    open(p,'w',encoding='utf-8',newline='').write(s)\n" +
        "EOF\n" +
        "diff <(tr -d '\\r' <dydo/files-off-limits.md) <(tr -d '\\r' < Scaffold/dydo/files-off-limits.md) && echo same";

    // GS-05, Claude Bash: the same paragraph rewrapped by a second Python program on stdin.
    public const string Gs05ProtectedRewrap =
        "W=C:/Users/User/Desktop/Projects/DynaDocs/.claude/worktrees/dyd-373; cd $W; python - <<'EOF'\n" +
        "old=(\"`dydo adopt` write `dydo.json` - so the tier stops hand edits, not dydo's tooling. Whitelist entries do not apply\\n\"\n" +
        "\"to this section.\\n\")\n" +
        "new=(\"`dydo adopt` write `dydo.json` - so the tier stops hand edits, not dydo's tooling. Whitelist\\n\"\n" +
        "\"entries do not apply to this section.\\n\")\n" +
        "for p in ['dydo/files-off-limits.md','Scaffold/dydo/files-off-limits.md']:\n" +
        "    raw=open(p,encoding='utf-8',newline='').read(); crlf='\\r\\n' in raw; s=raw.replace('\\r\\n','\\n')\n" +
        "    assert s.count(old)==1,p\n" +
        "    s=s.replace(old,new)\n" +
        "    if crlf: s=s.replace('\\n','\\r\\n')\n" +
        "    open(p,'w',encoding='utf-8',newline='').write(s)\n" +
        "EOF\n" +
        "sed -n 122,130p Scaffold/dydo/files-off-limits.md";
}
