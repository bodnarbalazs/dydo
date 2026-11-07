import { Buffer } from "node:buffer";
import { type ChildProcess, execFile, type ExecFileException, spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { inspect } from "node:util";
import { boundRun, chartRun, configText } from "./backends/cfg.ts";
import { plainText } from "./backends/plain-text.ts";
import { readConfig } from "./frontends/config.ts";
import { parseDot } from "./frontends/dot.ts";
import { readCounterexample, readMarks } from "./frontends/marks.ts";
import { readMessages } from "./frontends/messages.ts";
import { initialOnlyProperties, readChecks, readSany, readSpec } from "./frontends/spec.ts";
import { readTrace } from "./frontends/trace.ts";
import { type Break, readVerdict } from "./frontends/verdict.ts";
import {
  type Bug,
  CHART_STATES,
  type CheckResult,
  type ConfigSection,
  hasSection,
  type Lasso,
  LEVEL,
  type Mark,
  must,
  type Outcome,
  type Output,
  PART,
  type Part,
  type Property,
  RERUN_STATES,
  type Rule,
  ruleKey,
  type Stop,
  stateRules,
  type Trace,
  type Verdict,
} from "./ir.ts";
import { addPast, deadEnds, finder, lasso, mark } from "./passes/annotate.ts";
import { pathGraph } from "./passes/paths.ts";
import { checkerJar, pinnedTlc, prepareChecker, tlcInvocation } from "./checker.ts";
export { pinnedTlc, tlcInvocation } from "./checker.ts";

export function* fileLines(file: string) {
  const fd = fs.openSync(file, "r");
  try {
    let buf = Buffer.allocUnsafe(PART);
    let kept = 0;
    for (;;) {
      if (kept === buf.length) buf = Buffer.concat([buf], buf.length * 2);
      const n = fs.readSync(fd, buf, kept, buf.length - kept, null);
      if (!n) break;
      const end = kept + n;
      const cut = buf.lastIndexOf(10, end - 1);
      if (cut === -1) {
        kept = end;
        continue;
      }
      yield* buf.toString("utf8", 0, cut).split("\n");
      kept = buf.copy(buf, 0, cut + 1, end);
    }
    yield buf.toString("utf8", 0, kept);
  } finally {
    fs.closeSync(fd);
  }
}

const TLC_TOOL =
  "http:tla2tools[url=https://github.com/tlaplus/tlaplus/releases/download/v1.7.4/tla2tools.jar,checksum=sha256:936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88]@1.7.4";
const JAVA_TOOL = "java@temurin-25";
export const PINNED_FLAGS = ["-workers", "1", "-fp", "0", "-lncheck", "final"];

type ChildResult = {
  status: number | null;
  stdout: string;
  stderr: string;
  error: ExecFileException | undefined;
};

const childStatus = (error: ExecFileException | null) => {
  if (!error) return 0;
  return typeof error.code === "number" ? error.code : null;
};

const runChild = (file: string, args: string[], options: { cwd: string; env: NodeJS.ProcessEnv }) =>
  new Promise<ChildResult>((resolve) => {
    execFile(
      file,
      args,
      { encoding: "utf8", maxBuffer: 1 << 30, ...options, windowsHide: true },
      (error, stdout, stderr) =>
        resolve({
          status: childStatus(error),
          stdout,
          stderr,
          error: typeof error?.code === "string" ? error : undefined,
        }),
    );
  });

export const killGroup = (pid: number) =>
  new Promise((resolve) =>
    spawn("/bin/sh", ["-c", 'kill -s KILL -- "-$1"', "sh", String(pid)], { stdio: "ignore" }).on(
      "exit",
      resolve,
    ),
  );

export const messageOf = (error: unknown) =>
  typeof error === "object" &&
  error !== null &&
  "message" in error &&
  typeof error.message === "string"
    ? error.message
    : inspect(error);

const joined = (files: string[]): Output => {
  const fds: number[] = [];
  const sizes: number[] = [];
  for (const f of files) {
    const fd = fs.openSync(f, "r");
    fds.push(fd);
    sizes.push(fs.fstatSync(fd).size);
  }
  return {
    *[Symbol.iterator]() {
      for (let f = 0; f < fds.length; f++)
        for (let pos = 0; pos < sizes[f];) {
          const buf = Buffer.allocUnsafe(Math.min(PART, sizes[f] - pos));
          const n = fs.readSync(fds[f], buf, 0, buf.length, pos);
          if (!n) break;
          pos += n;
          yield buf.subarray(0, n);
        }
    },
  };
};

const utf8 = new TextDecoder("utf-8", { ignoreBOM: true });

function* outputLines(output: Output) {
  let rest: Uint8Array = new Uint8Array(0);
  for (const part of output) {
    const buf = rest.length ? Buffer.concat([rest, part]) : part;
    const cut = buf.lastIndexOf(10);
    if (cut === -1) {
      rest = buf;
      continue;
    }
    yield* utf8.decode(buf.subarray(0, cut)).split("\n");
    rest = buf.subarray(cut + 1);
  }
  yield utf8.decode(rest);
}

const messagesOf = (output: Output): Iterable<Part> => ({
  [Symbol.iterator]: () => readMessages(outputLines(output)),
});

type FileResult = {
  status: number | null;
  stdout: string;
  output: Output;
  error: Error | undefined;
};

const runToFiles = (
  file: string,
  args: string[],
  { out, ...options }: { out: string; cwd: string; env: NodeJS.ProcessEnv },
) =>
  new Promise<FileResult>((resolve) => {
    const files = [`${out}.stdout`, `${out}.stderr`];
    const fds: number[] = [];
    for (const f of files) fds.push(fs.openSync(f, "w"));
    let child: ChildProcess;
    try {
      child = spawn(file, args, { ...options, stdio: ["ignore", ...fds], windowsHide: true });
    } finally {
      for (const fd of fds) fs.closeSync(fd);
    }
    let error: Error | undefined;
    const settle = (code: number | null) =>
      resolve({
        status: error ? null : code,
        stdout: files[0],
        output: joined(files),
        error,
      });
    child.on("error", (e) => {
      error = e;
      if (child.pid === undefined) settle(null);
    });
    child.on("close", (code) => {
      if (child.pid !== undefined) settle(code);
    });
  });

async function findTlc(cwd: string) {
  const { TLA2TOOLS, JAVA } = process.env;
  const java = JAVA && /[/\\]/.test(JAVA) ? path.resolve(JAVA) : JAVA;
  if (TLA2TOOLS) return { jar: pinnedTlc(path.resolve(TLA2TOOLS)), java: java || "java" };
  const mise = (...args: string[]) =>
    runChild("mise", args, { cwd, env: { ...process.env, MISE_CEILING_PATHS: cwd } });
  const where = () =>
    Promise.all([mise("where", TLC_TOOL), mise("which", "java", "--tool", JAVA_TOOL)]);
  let found = await where();
  if (found.some((r) => r.status !== 0)) {
    const r = await mise("install", JAVA_TOOL, TLC_TOOL);
    if (r.status !== 0)
      throw new Error(
        `Cannot find TLC: mise install failed: ${r.error?.message ?? r.stderr.trim()}\nOr set TLA2TOOLS to the path of tla2tools.jar, and JAVA to a java binary (Java 11 or later).`,
      );
    found = await where();
  }
  return {
    jar: pinnedTlc(path.join(found[0].stdout.trim(), "tla2tools.jar")),
    java: java || found[1].stdout.trim(),
  };
}

const exitClass = (status: number | null): 0 | 1 | 2 => {
  if (status === 0) return 0;
  return status === 11 || status === 12 || status === 13 ? 1 : 2;
};

const tail = (r: FileResult) => {
  if (r.error) return r.error.message;
  const kept: string[] = [];
  let size = 0;
  for (const chunk of plainText(messagesOf(r.output))) {
    kept.push(chunk);
    size += chunk.length;
    while (size - kept[0].length >= 1 << 28) size -= kept.shift()!.length;
  }
  return kept.join("").trim().split("\n").slice(-30).join("\n");
};

const stopOf = (r: FileResult): Stop => {
  const errors: Part[] = [];
  for (const m of r.error ? [] : messagesOf(r.output))
    if (
      typeof m !== "string" &&
      (m.level === LEVEL.ERROR ||
        m.level === LEVEL.TLCBUG ||
        (errors.length && m.level === LEVEL.STATE))
    )
      errors.push(m);
  if (!errors.length) return { status: r.status, message: tail(r) };
  const lines = [...plainText(errors)].join("").trim().split("\n");
  if (lines.length > 40) lines.splice(40, Infinity, `... ${lines.length - 40} more lines`);
  return { status: r.status, message: lines.join("\n") };
};

const names = (rules: Rule[], kind: Rule["kind"]) => {
  const found: string[] = [];
  for (const rule of rules) if (rule.kind === kind) found.push(rule.name);
  return found;
};

const brokenRule = (rules: Rule[], broke: Break) => {
  if (broke.kind === "temporal") return undefined;
  for (const rule of rules)
    if (rule.kind === broke.kind && (broke.kind === "deadlock" || rule.name === broke.name))
      return rule;
  if (broke.kind === "invariant")
    for (const rule of rules) if (rule.kind === "property" && rule.name === broke.name) return rule;
  return undefined;
};

function* only(marks: Iterable<Mark>, rules: Set<string>) {
  for (const { values, broken } of marks) {
    const kept: string[] = [];
    for (const name of broken) if (rules.has(name)) kept.push(name);
    if (kept.length) yield { values, broken: kept };
  }
}

type Judging = {
  verdicts: Map<string, Verdict>;
  states: { distinct: number; initial: number; complete: boolean; cut: boolean };
  specFile: string;
};

const decide = (judging: Judging, rule: Rule, outcome: Outcome, r?: FileResult) =>
  judging.verdicts.set(ruleKey(rule), { rule, outcome, messages: r && messagesOf(r.output) });

function splitProperties(judging: Judging, group: Rule[], r: FileResult, broke: Break) {
  if (broke.kind === "temporal") {
    judging.states.complete = true;
    for (const rule of group) if (rule.kind !== "property") decide(judging, rule, "holds");
  }
  const pending: Rule[][] = [];
  const rest: Rule[] = [];
  for (const rule of group)
    if (rule.kind === "property") pending.push([rule]);
    else if (!judging.verdicts.has(ruleKey(rule))) rest.push(rule);
  if (pending.length !== 1) return [rest, ...pending];
  decide(judging, pending[0][0], "broken", r);
  return undefined;
}

function settle(
  judging: Judging,
  group: Rule[],
  r: FileResult,
  { broke, distinct }: ReturnType<typeof readVerdict>,
  bounded: boolean,
): Rule[][] | undefined {
  const rule = broke && brokenRule(group, broke);
  if (rule) decide(judging, rule, "broken", r);
  else if (bounded && distinct >= RERUN_STATES && (r.status === 0 || broke?.kind === "temporal")) {
    judging.states.cut = true;
    for (const each of group) decide(judging, each, "not checked");
  } else if (r.status === 0) {
    judging.states.complete = true;
    for (const each of group) decide(judging, each, "holds");
  } else if (broke && group.some((each) => each.kind === "property"))
    return splitProperties(judging, group, r, broke);
  else
    throw new Error(
      `TLC exited ${r.status} on ${judging.specFile}, but its output names no rule that broke:\n${tail(r)}`,
    );
  return undefined;
}

async function judge(
  judging: Judging,
  all: Rule[],
  run: (name: string, group: Rule[], bounded: boolean) => Promise<FileResult>,
) {
  const groups = [all];
  let first: FileResult | undefined;
  let stop: Stop | undefined;
  for (let k = 1; groups.length;) {
    const group = groups[0].filter((rule) => !judging.verdicts.has(ruleKey(rule)));
    if (first && !group.length) {
      groups.shift();
      continue;
    }
    const bounded = first !== undefined && !judging.states.complete;
    const r = await run(`verdict-${k++}`, group, bounded);
    first ??= r;
    const found = readVerdict(messagesOf(r.output));
    judging.states.distinct = Math.max(judging.states.distinct, found.distinct);
    judging.states.initial = Math.max(judging.states.initial, found.initial);
    if (exitClass(r.status) === 2) {
      stop = stopOf(r);
      break;
    }
    const split = settle(judging, group, r, found, bounded);
    if (split) groups.splice(0, 1, ...split);
  }
  for (const rule of all) if (!judging.verdicts.has(ruleKey(rule))) decide(judging, rule, "not checked");
  return { first: first!, stop, states: judging.states };
}

export async function check(specFile: string, configFile: string): Promise<CheckResult> {
  const cfg = readConfig({ file: configFile, text: fs.readFileSync(configFile, "utf8") });
  const checks = readChecks({ configFile, config: cfg });
  const folder = path.dirname(path.resolve(specFile));
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "statechart-"));
  try {
    const tlc = await findTlc(dir);
    const checker = prepareChecker(tlc.jar, dir);
    tlcInvocation(checker);
    const config = (name: string, sections: ConfigSection[], extra: string[]) => {
      fs.writeFileSync(path.join(dir, `${name}.cfg`), configText(sections, extra));
      return [`-metadir`, path.join(dir, `meta-${name}`), "-config", path.join(dir, `${name}.cfg`)];
    };
    const runJava = (name: string, main: string, args: string[], jvm: string[] = []) => {
      const tmp = path.join(dir, `tmp-${name}`);
      fs.mkdirSync(tmp);
      return runToFiles(
        tlc.java,
        [
          `-Djava.io.tmpdir=${tmp}`,
          `-DTLA-Library=${folder}`,
          ...jvm,
          ...(main === "dydo.statechart.LoopbackTlc" ? tlcInvocation(checker) : ["-cp", checkerJar(checker), main]),
          ...args,
        ],
        {
          cwd: dir,
          out: path.join(dir, name),
          env: process.platform === "darwin" ? process.env : { ...process.env, LC_ALL: "C.UTF-8" },
        },
      );
    };
    const xml = await runJava(
      "sany",
      "tla2sany.xml.XMLExporter",
      ["-o", "-I", folder, path.resolve(specFile)],
      ["-XX:TieredStopAtLevel=1"],
    );
    if (xml.status !== 0)
      throw new Error(`SANY could not read ${specFile} (exit ${xml.status}):\n${tail(xml)}`);
    const sany = readSany(fs.readFileSync(xml.stdout, "utf8"));
    const module = path.basename(specFile, ".tla");
    const spec = { ...readSpec({ module, sany, checks }), folder };
    const rules = stateRules(spec);
    const all = [...rules];
    for (const name of spec.properties) all.push({ kind: "property", name });
    const judging: Judging = {
      verdicts: new Map<string, Verdict>(),
      states: { distinct: 0, initial: 0, complete: false, cut: false },
      specFile,
    };
    const verdicts = judging.verdicts;
    const runVerdict = (name: string, group: Rule[], bounded: boolean) => {
      const extra: string[] = [];
      const invariants = names(group, "invariant");
      const properties = names(group, "property");
      if (invariants.length) extra.push(`INVARIANT ${invariants.join(" ")}`);
      if (properties.length) extra.push(`PROPERTY ${properties.join(" ")}`);
      if (bounded) extra.push("CONSTRAINT ChartBound");
      return runJava(name, "dydo.statechart.LoopbackTlc", [
        "-tool",
        ...PINNED_FLAGS,
        ...(names(group, "deadlock").length ? [] : ["-deadlock"]),
        ...config(name, spec.written, extra),
        bounded ? "ChartBound" : spec.module,
      ]);
    };
    fs.writeFileSync(path.join(dir, "ChartBound.tla"), boundRun(spec.module));
    const judged = await judge(judging, all, runVerdict);
    const exit = exitClass(judged.first.status);
    const failed = (r: FileResult) =>
      new Error(`TLC could not check ${specFile} (exit ${r.status}):\n${stopOf(r).message}`);
    const initialOnly = initialOnlyProperties(sany, spec);
    const propertiesWith = (bugOf: (trace: Trace) => Lasso | undefined) => {
      const properties: Property[] = [];
      for (const rule of all) {
        if (rule.kind !== "property") continue;
        const v = must(verdicts, ruleKey(rule));
        properties.push({
          ...v,
          bug: v.outcome === "broken" && v.messages ? bugOf(readTrace(v.messages)) : undefined,
          initialOnly: initialOnly.has(rule.name),
        });
      }
      return properties;
    };
    if (exit === 2) throw failed(judged.first);
    const checked: Verdict[] = [];
    for (const rule of rules) checked.push(must(verdicts, ruleKey(rule)));
    const common = {
      spec,
      exit,
      stop: judged.stop,
      states: judged.states,
      messages: messagesOf(judged.first.output),
      rules: checked,
    };
    const chart = async () => {
      const tag = `chart-${randomBytes(16).toString("hex")}`;
      fs.writeFileSync(path.join(dir, "ChartRun.tla"), chartRun(spec, tag));
      const drawn = await runJava("ChartRun", "dydo.statechart.LoopbackTlc", [
        "-tool",
        ...PINNED_FLAGS,
        "-deadlock",
        ...config("ChartRun", spec.written, ["INVARIANT ChartMark", "CONSTRAINT ChartBudget"]),
        "-dump",
        "dot,actionlabels",
        path.join(dir, "graph"),
        "ChartRun",
      ]);
      const file = path.join(dir, "graph.dot");
      if (!fs.existsSync(file)) throw failed(drawn);
      if (readVerdict(messagesOf(drawn.output)).distinct > CHART_STATES) return undefined;
      const graph = parseDot(fileLines(file));
      const find = finder(graph, spec.variables);
      const drawing = drawn.status === 0 ? undefined : { status: drawn.status };
      const broken = new Set<string>();
      for (const { rule, outcome } of verdicts.values())
        if (rule.kind === "invariant" && outcome === "broken") broken.add(rule.name);
      const marks = only(readMarks(readMessages(fileLines(drawn.stdout)), tag), broken);
      const past = mark(graph, marks, spec.variables, rules, find);
      const excludes = hasSection(spec, "ACTION_CONSTRAINT");
      const deadEndsDrawn = !drawing && !hasSection(spec, "CONSTRAINT", "ACTION_CONSTRAINT");
      for (const rule of rules) {
        const { outcome, messages } = must(verdicts, ruleKey(rule));
        if (rule.kind === "deadlock" && outcome === "broken" && deadEndsDrawn) deadEnds(graph, rule);
        if (outcome !== "broken" || !messages) continue;
        const counterexample = readCounterexample(messages);
        if (!drawing || lasso(graph, { seq: counterexample, loop: undefined }, spec.variables, find))
          addPast(graph, counterexample, spec.variables, rule, past, excludes, find);
      }
      const properties = propertiesWith((trace) => lasso(graph, trace, spec.variables, find));
      return { graph, paths: undefined, properties, drawing };
    };
    const bugPaths = () => {
      const drawn = pathGraph(spec.variables);
      const paths: Bug[] = [];
      for (const rule of rules) {
        const { outcome, messages } = must(verdicts, ruleKey(rule));
        if (outcome === "broken" && messages) paths.push(drawn.bug(rule, readCounterexample(messages)));
      }
      return { graph: drawn.graph, paths, properties: propertiesWith(drawn.lasso), drawing: undefined };
    };
    const charted = judged.states.distinct <= CHART_STATES ? await chart() : undefined;
    return { ...common, ...(charted ?? bugPaths()) };
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}
