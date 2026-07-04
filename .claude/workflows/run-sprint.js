export const meta = {
  name: 'run-sprint',
  description: 'Implement a sprint: for each disjoint slice, loop code-writer → reviewer until the review passes (escalating after 5 failed rounds or whenever a worker raises its hand), sequentially merge passed worktree slices back into the invoking branch, then run a sprint-auditor final review over the entire merged sprint diff.',
  whenToUse: 'Run a sprint of pre-sliced, disjoint implementation work with automated code→review loops, merge-back, and a final sprint audit (dydo 2.0 flagship). Pass args = array of slice briefs (strings) or {name, brief} objects.',
  phases: [
    { title: 'Slice' },
    { title: 'Implement & review' },
    { title: 'Merge' },
    { title: 'Audit' },
    { title: 'Report' },
  ],
}

// Grounded in the human's in-the-wild experience: up to 4 consecutive *natural*
// review fails observed (each surfacing a new real issue), so 5 gives headroom
// before escalating instead of burning tokens on a stuck slice.
const MAX_REVIEW_ROUNDS = 5

// Appended to every code-writer prompt. Declared here (not below the helpers) so it
// is initialized before the phase body runs — a `const` is in the temporal dead zone
// until its declaration executes, and the phase body calls codePrompt() before that.
const RAISE_HAND_NOTE =
  '\n\nIf the brief is ambiguous, contradicts the codebase, or you find yourself thrashing on the same root cause across rounds, set raiseHand=true with a reason instead of guessing — a human will step in.'

// Worker structured outputs. All carry a `raiseHand` circuit-breaker so any
// worker can proactively pull the human in mid-loop, independent of the cap.
// branch/worktreePath are required so the merge phase knows where each slice's
// work lives (the harness runs isolated slices in .claude/worktrees/wf_* worktrees
// on their own worktree-wf_* branches).
const CODE_RESULT = {
  type: 'object',
  required: ['summary', 'raiseHand', 'branch', 'worktreePath'],
  additionalProperties: false,
  properties: {
    summary: { type: 'string', description: 'What you implemented or changed this round, and the test/coverage outcome.' },
    raiseHand: { type: 'boolean', description: 'true if the spec is ambiguous, contradicts the codebase, or you are thrashing — human judgment needed.' },
    reason: { type: 'string', description: 'If raiseHand is true, why human eyes are needed.' },
    branch: { type: ['string', 'null'], description: 'The branch your work lives on — required, so state your mode explicitly. Isolated (dedicated worktree): your worktree branch from `git branch --show-current`. In-tree (main working tree): null (you were told not to create/reuse a branch).' },
    worktreePath: { type: 'string', description: 'The root of the working tree you edited: `git rev-parse --show-toplevel`.' },
  },
}

const REVIEW_RESULT = {
  type: 'object',
  required: ['pass', 'raiseHand'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true ONLY if the change is correct, tested, and standards-clean. PASS means perfect — no "pass with notes".' },
    findings: { type: 'string', description: 'Specific, actionable findings when not pass.' },
    raiseHand: { type: 'boolean', description: 'true if something needs human judgment rather than another code round.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

const MERGE_RESULT = {
  type: 'object',
  required: ['merged', 'conflicted', 'baseCommit', 'raiseHand'],
  additionalProperties: false,
  properties: {
    merged: {
      type: 'array',
      description: 'Every slice whose `git merge` completed WITHOUT abort, in merge order — each with the result of verifying the landing via `git merge-base --is-ancestor <slice-branch> HEAD`. Conflict-aborted slices go in `conflicted`, NOT here.',
      items: {
        type: 'object',
        required: ['name', 'verified'],
        additionalProperties: false,
        properties: {
          name: { type: 'string' },
          verified: { type: 'boolean', description: 'true ONLY if `git merge-base --is-ancestor <slice-branch> HEAD` exited 0 — the slice branch is provably an ancestor of the invoking branch HEAD. Report what the command actually returned; never assume.' },
        },
      },
    },
    conflicted: {
      type: 'array',
      description: 'Slices whose merge needed judgment and was aborted — left intact on their worktree branches.',
      items: {
        type: 'object',
        required: ['name', 'reason'],
        additionalProperties: false,
        properties: {
          name: { type: 'string' },
          reason: { type: 'string', description: 'What conflicted and why it was not trivially resolvable.' },
        },
      },
    },
    baseCommit: { type: 'string', description: 'SHA of the invoking branch HEAD before the first merge (`git rev-parse HEAD`) — the sprint audit diffs from here.' },
    raiseHand: { type: 'boolean', description: 'true if the merge as a whole needs human judgment.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

const AUDIT_RESULT = {
  type: 'object',
  required: ['pass', 'raiseHand'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true ONLY if the merged sprint holds together as one unit — correct, seam-clean, covered, standards-clean. PASS means perfect; findings mean fail.' },
    findings: { type: 'string', description: 'Specific findings with file:line locations when not pass — cross-slice seam issues, correctness bugs, coverage gaps, standards violations.' },
    raiseHand: { type: 'boolean', description: 'true if something needs human judgment beyond reporting findings.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

phase('Slice')
const slices = normalizeSlices(args)
// Worktree isolation exists to keep PARALLEL slices from clobbering each other's
// files and build outputs — so it only applies when there's more than one slice.
// A lone slice has nothing to isolate from, and the harness's worktree branches
// from a base commit that can predate in-progress branch work (it would hide the
// very code the slice builds on), so a single slice runs in the main working tree.
const ISOLATE = slices.length > 1
log(`Sprint: ${slices.length} slice(s) — ${slices.map(s => s.name).join(', ')}. ${ISOLATE ? 'Worktree-isolated (parallel).' : 'In-tree (single slice).'}`)

phase('Implement & review')
const results = (await parallel(slices.map(slice => () => runSlice(slice)))).filter(Boolean)

phase('Merge')
// Merge passed slices back into the invoking branch (Decision 026) — without this,
// passed work would be stranded in worktrees. The workflow script has no fs/git
// access, so ONE dedicated merge agent does every merge, strictly one slice at a
// time, never concurrent. Single-slice sprints run in-tree: nothing to merge back.
let baseCommit = null
const toMerge = results.filter(r => r.status === 'passed')
if (ISOLATE && toMerge.length > 0) {
  const merge = await agent(mergePrompt(toMerge), {
    agentType: 'code-writer',
    label: 'merge',
    phase: 'Merge',
    schema: MERGE_RESULT,
  })
  baseCommit = merge?.baseCommit ?? null
  for (const r of toMerge) {
    // Conflict first: an aborted merge was "attempted", so a schema-literal agent may
    // list it under merged with verified:false — but its specific reason is the truth.
    const conflict = merge?.conflicted?.find(c => c.name === r.name)
    if (conflict) {
      r.status = 'escalated'
      r.stage = 'merge'
      r.reason = conflict.reason
      continue
    }
    // merged:true is set ONLY from the agent's git merge-base --is-ancestor verification,
    // never from a bare claim. A claimed-but-unverified landing (or an omitted slice)
    // escalates with the slice's worktree branch/worktreePath preserved in the ledger.
    const claim = merge?.merged?.find(m => m.name === r.name)
    if (claim?.verified) {
      r.merged = true
      continue
    }
    r.status = 'escalated'
    r.stage = 'merge'
    r.reason = claim
      ? 'merge claimed but not verified'
      : (merge?.reason ?? 'merge agent did not account for this slice')
  }
  log(`Merge: ${toMerge.filter(r => r.merged).length}/${toMerge.length} passed slice(s) landed on the invoking branch.`)
} else {
  // In-tree work already sits on the invoking branch — nothing stranded. But a
  // compliant single-slice writer was told to commit nothing and return branch:null;
  // a non-null branch means it committed somewhere unexpected (e.g. a stale reused
  // worktree). Never assume that work is on the invoking branch or audit a diff that
  // isn't there — escalate with the branch named and its worktreePath in the ledger.
  for (const r of toMerge) {
    if (r.branch) {
      r.status = 'escalated'
      r.stage = 'merge'
      r.reason = `work landed on unexpected branch ${r.branch}`
      continue
    }
    r.merged = true
  }
}

phase('Audit')
// ONE final-review agent over the ENTIRE merged sprint as a unit (Decision 026):
// inquisitor-lensed, judge-strict, and natively unable to dispatch subagents (its
// agent definition omits the Agent tool). A failing audit does NOT loop back into
// code rounds (v1) — the findings surface to the orchestrator/human via the return.
let auditVerdict, auditFindings
if (results.some(r => r.merged)) {
  const audit = await agent(auditPrompt(baseCommit), {
    agentType: 'sprint-auditor',
    label: 'sprint-audit',
    phase: 'Audit',
    schema: AUDIT_RESULT,
  })
  auditVerdict = audit?.pass && !audit.raiseHand ? 'pass' : 'fail'
  // Keep BOTH findings and reason: an auditor that raises its hand AND returns
  // findings has said two distinct things — dropping either loses signal.
  auditFindings = audit
    ? [audit.findings, audit.raiseHand && audit.reason ? `Raised hand: ${audit.reason}` : audit.reason]
        .filter(Boolean).join('\n\n')
    : 'sprint-auditor did not return a result'
} else {
  auditVerdict = 'skipped'
  auditFindings = 'No slice work landed on the invoking branch — nothing to audit.'
}

phase('Report')
const passed = results.filter(r => r.status === 'passed')
const escalated = results.filter(r => r.status === 'escalated')
log(`Done: ${passed.length} passed & merged, ${escalated.length} escalated to the human. Audit: ${auditVerdict}.`)
return {
  passed: passed.map(r => r.name),
  escalated, // each: { name, stage, round, reason, findings?, summary?, branch?, worktreePath? }
  // Per-slice ledger: branch + merged/stranded status, so nothing is silently lost —
  // an escalated or merge-conflicted slice still lives intact on its worktree branch.
  slices: results.map(r => ({
    name: r.name,
    status: r.status,
    round: r.round,
    branch: r.branch ?? null,
    worktreePath: r.worktreePath ?? null,
    merged: r.merged === true,
  })),
  auditVerdict, // 'pass' | 'fail' | 'skipped' — a fail does not loop; route the findings yourself
  auditFindings,
}

// One slice: loop code → review until pass, the cap, or a raised hand.
async function runSlice(slice) {
  let feedback = null
  let work = {} // branch + worktree of the last code round — where the slice's files live
  for (let round = 1; round <= MAX_REVIEW_ROUNDS; round++) {
    const codeOpts = {
      agentType: 'code-writer',
      label: `code:${slice.name}#${round}`,
      phase: 'Implement & review',
      schema: CODE_RESULT,
    }
    if (ISOLATE) codeOpts.isolation = 'worktree'
    const code = await agent(codePrompt(slice, feedback, round), codeOpts)
    if (code?.branch) work = { branch: code.branch, worktreePath: code.worktreePath }
    if (!code || code.raiseHand)
      return escalate(slice, 'code-writer', round, code?.reason ?? 'code-writer did not return a result', { summary: code?.summary, ...work })

    const review = await agent(reviewPrompt(slice, code.summary, work), {
      agentType: 'reviewer',
      label: `review:${slice.name}#${round}`,
      phase: 'Implement & review',
      schema: REVIEW_RESULT,
    })
    if (!review || review.raiseHand)
      return escalate(slice, 'reviewer', round, review?.reason ?? 'reviewer did not return a result', { findings: review?.findings, ...work })
    if (review.pass)
      return { name: slice.name, status: 'passed', round, ...work }

    feedback = review.findings // the next code round fixes exactly these
  }
  return escalate(slice, 'review-cap', MAX_REVIEW_ROUNDS,
    `${MAX_REVIEW_ROUNDS} consecutive review rounds did not pass — human eyes needed.`, work)
}

function escalate(slice, stage, round, reason, extra = {}) {
  // Attention signal (Decision 030 §1): escalated slices are surfaced to the Tier-1 orchestrator in
  // this workflow's result. When the orchestrator stops to consult the human about them, dydo's
  // Stop-hook detection sets the orchestrator's OWN needs-human flag — the raise happens there, not
  // here. The workflow harness exposes no shell, so there is no in-script CLI call to make.
  return { name: slice.name, status: 'escalated', stage, round, reason, ...extra }
}

function normalizeSlices(a) {
  // Harness/permission-pipeline workaround (observed live 2026-07-03, runs
  // wf_6cd452d1-276 and wf_8eba6003-d9d): args passed as a real JSON array can
  // arrive stringified, silently collapsing a multi-slice sprint into ONE
  // in-tree slice. If the string parses as JSON, honor the parsed value.
  if (typeof a === 'string') {
    const t = a.trim()
    if (t.startsWith('[') || t.startsWith('{')) {
      try { a = JSON.parse(t) } catch { /* genuine prose brief — fall through */ }
    }
    if (typeof a === 'string') return [{ name: 'slice-1', brief: a }]
  }
  if (a && !Array.isArray(a) && typeof a === 'object') a = [a]
  if (!Array.isArray(a) || a.length === 0)
    throw new Error('run-sprint expects args = a non-empty array of slice briefs (strings) or {name, brief} objects.')
  return a.map((s, i) => typeof s === 'string'
    ? { name: `slice-${i + 1}`, brief: s }
    : { name: s.name ?? `slice-${i + 1}`, brief: s.brief })
}

function codePrompt(slice, feedback, round) {
  // The writer must know its mode: an isolated slice commits on its own worktree
  // branch (the merge phase lands it), while an in-tree slice must NOT branch or
  // commit — otherwise its work can silently land on an unexpected/stale branch.
  const modeNote = ISOLATE
    ? '\n\nYou are in a dedicated worktree the harness created for this slice. Commit your work on your worktree branch and return that branch (`git branch --show-current`) and its root (`git rev-parse --show-toplevel`) as branch + worktreePath — the sprint merge phase needs them to land your work.'
    : '\n\nYou work directly in the main working tree. Do NOT create or reuse any worktree or branch, and do NOT commit — leave your changes uncommitted for the in-tree audit. Return branch: null and worktreePath = `git rev-parse --show-toplevel`.'
  const base = `You are implementing sprint slice "${slice.name}".\n\nBrief:\n${slice.brief}\n\nImplement it fully, add or adjust tests, and run the test runner + coverage gate before finishing.${modeNote}`
  const roundNote = round === 1 ? '' :
    `\n\nThis is round ${round}. A prior review FAILED — address these findings specifically:\n${feedback}`
  return base + roundNote + RAISE_HAND_NOTE
}

function reviewPrompt(slice, codeSummary, work) {
  // Isolated slices live in a workflow worktree, not the main tree — without the
  // location the reviewer would diff the wrong tree and see none of the work.
  const whereNote = ISOLATE && work?.branch
    ? `\n\nThe work lives on branch \`${work.branch}\` in the worktree \`${work.worktreePath}\` — review it THERE (run tests and diffs against that working tree, not the main one).`
    : ''
  return `Review the implementation of sprint slice "${slice.name}".\n\nBrief:\n${slice.brief}\n\nThe code-writer reports:\n${codeSummary}${whereNote}\n\nReview strictly per your reviewer methodology — PASS only if correct, tested, and standards-clean (no "pass with notes"; PASS means perfect). Run the tests + coverage gate. Return a structured verdict; if not pass, make findings specific and actionable. If something needs human judgment rather than another code round, set raiseHand=true.`
}

function mergePrompt(toMerge) {
  const list = toMerge.map((r, i) => `${i + 1}. ${r.name} — branch \`${r.branch}\`, worktree \`${r.worktreePath}\``).join('\n')
  return `You are the sprint merge agent. ${toMerge.length} passed slice(s) live on workflow worktree branches; land them on the invoking branch SEQUENTIALLY — fully finish one slice before touching the next, never concurrently — in this exact order:\n\n${list}\n\nYou work in the MAIN working tree (confirm with \`git rev-parse --show-toplevel\`). Before the first merge, record \`git rev-parse HEAD\` and return it as baseCommit.\n\nPer slice, in order:\n1. If its worktree has uncommitted work, commit it on ITS branch: \`git -C <worktreePath> add -A && git -C <worktreePath> commit -m "<slice name>: sprint work"\`.\n2. Merge into the invoking branch: \`git merge --no-ff <branch> -m "merge sprint slice <slice name>"\`.\n3. Merge completed without abort → VERIFY the landing: run \`git merge-base --is-ancestor <branch> HEAD\` and record the slice under merged as {name, verified} where verified is true ONLY if that command exited 0. Report the actual result — never claim verified without running the check. Continue to the next slice.\n4. Conflict → you may resolve ONLY trivial conflicts: disjoint hunks, or pure whitespace/line-ending differences. Anything requiring judgment about intent: run \`git merge --abort\`, record the slice under conflicted (NOT merged) with a specific reason, and continue with the remaining slices. Never rebase, never force, never discard slice work — an aborted slice stays intact on its branch for the human.\n\nReturn merged ({name, verified} for every slice whose merge completed without abort, in merge order), conflicted ({name, reason} for every slice you aborted), and baseCommit.` + RAISE_HAND_NOTE
}

function auditPrompt(baseCommit) {
  const scope = baseCommit
    ? `the merged sprint diff on the invoking branch — \`git diff ${baseCommit}..HEAD\` — plus any uncommitted changes on top (\`git status\`, \`git diff HEAD\`)`
    : 'the sprint work in the current working tree: `git diff HEAD` plus untracked files (single-slice sprints run in-tree, uncommitted)'
  const stranded = results.filter(r => !r.merged).map(r => r.name)
  const strandedNote = stranded.length
    ? `\n\nNOTE: slice(s) ${stranded.join(', ')} did NOT land (escalated or merge-conflicted) — their absence is expected; do not report their missing work as a finding.`
    : ''
  const sliceBlock = slices.map(s => `### ${s.name}\n${s.brief}`).join('\n\n')
  return `Final sprint audit. Audit ${scope} as ONE unit, per your sprint-auditor methodology: hunt real cross-slice issues — correctness, the seams between slices, coverage gaps, standards — verify every finding against the actual code, run the tests + coverage gate, and return a strict verdict (pass means perfect; findings mean fail, with file:line specifics).${strandedNote}\n\nThe sprint's slice briefs:\n\n${sliceBlock}`
}
