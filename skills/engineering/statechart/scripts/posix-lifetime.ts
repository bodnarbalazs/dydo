import fs from "node:fs";
import { Socket } from "node:net";
import path from "node:path";
import type { Duplex } from "node:stream";
import { worker } from "./lifetime.ts";

type Pending = {
  expected: string;
  terminal: boolean;
  resolve(): void;
  reject(error: Error): void;
};

export class PosixControl {
  readonly lost: Promise<never>;
  private failLost!: (error: Error) => void;
  private pending?: Pending;
  private error?: Error;
  private frame = "";
  private finished = false;
  private readonly channel: Duplex;

  constructor(channel: Duplex) {
    this.channel = channel;
    this.lost = new Promise<never>((_, reject) => { this.failLost = reject; });
    // Loss can arrive between requests, before the public flow starts awaiting it.
    void this.lost.catch(() => {});
    channel.on("data", (chunk: Buffer) => this.receive(chunk));
    channel.on("error", (error: Error) => this.fail(error));
    channel.on("end", () => this.ended());
    channel.on("close", () => this.ended());
  }

  async request(frame: string, expected: string, terminal = false): Promise<void> {
    if (this.error) throw this.error;
    if (this.finished || this.pending || frame.includes("\n") || frame.length > 16384)
      throw new Error("Invalid statechart POSIX control transition.");
    let timeout: ReturnType<typeof setTimeout> | undefined;
    try {
      await new Promise<void>((resolve, reject) => {
        this.pending = { expected, terminal, resolve, reject };
        timeout = setTimeout(() => this.fail(new Error("Statechart POSIX owner acknowledgment timed out.")), 30000);
        this.channel.write(`${frame}\n`, (error) => { if (error) this.fail(error); });
      });
    } finally { clearTimeout(timeout); }
  }

  close(): void { this.channel.destroy(); }

  private receive(chunk: Buffer): void {
    this.frame += chunk.toString("utf8");
    if (this.frame.length > 16384) {
      this.fail(new Error("Oversize statechart POSIX control frame."));
      return;
    }
    if (!this.frame.includes("\n")) return;
    const pending = this.pending;
    if (!pending || this.frame !== `${pending.expected}\n`) {
      this.fail(new Error("Statechart POSIX owner acknowledgment rejected."));
      return;
    }
    this.frame = "";
    this.pending = undefined;
    this.finished = pending.terminal;
    pending.resolve();
  }

  private ended(): void {
    if (!this.finished) this.fail(new Error("Independent statechart POSIX owner channel lost."));
  }

  private fail(error: Error): void {
    this.error ??= error;
    this.pending?.reject(this.error);
    this.pending = undefined;
    this.failLost(this.error);
  }
}

function plainAncestors(directory: string): void {
  for (let current = directory;; current = path.dirname(current)) {
    const info = fs.lstatSync(current, { throwIfNoEntry: false });
    if (info && (!info.isDirectory() || info.isSymbolicLink()))
      throw new Error(`Statechart output has a foreign or linked ancestor: ${current}`);
    if (path.dirname(current) === current) return;
  }
}

export function publishPosix(done: string, destination: string): void {
  if (!path.isAbsolute(done) || !path.isAbsolute(destination)) throw new Error("Statechart publication requires absolute paths.");
  plainAncestors(done);
  plainAncestors(destination);
  const target = fs.lstatSync(destination, { throwIfNoEntry: false });
  if (target && fs.readdirSync(destination).length !== 0)
    throw new Error("Statechart output became nonempty; foreign content retained.");
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  plainAncestors(path.dirname(destination));
  // POSIX rename atomically accepts only a missing or empty destination directory.
  fs.renameSync(done, destination);
}

type Configuration = {
  mode: string;
  stage: string;
  nativeExecutable: string;
  nativeArguments: string[];
  completedRequest: string | null;
  request: { Output: string; Caller: string };
};

function handoff(arguments_: string[]): { file: string; fd: number; nonce: string; group: number } {
  const [file, descriptor, nonce, groupId] = arguments_;
  const fd = Number(descriptor);
  const group = Number(groupId);
  if (arguments_.length !== 4 || !file || !path.isAbsolute(file) || !/^[a-f0-9]{32}$/.test(nonce ?? "") ||
      !Number.isSafeInteger(fd) || fd < 3 || !Number.isSafeInteger(group) || group <= 0)
    throw new Error("Invalid inherited statechart POSIX ownership handoff.");
  return { file, fd, nonce, group };
}

function result(stage: string, status: number): string | undefined {
  const done = path.join(stage, "done");
  const file = path.join(stage, "verdict.json");
  if (!fs.existsSync(file) || !fs.existsSync(path.join(done, "report.md"))) return;
  const verdict = JSON.parse(fs.readFileSync(file, "utf8")).verdict;
  if (verdict !== 0 && verdict !== 1) return;
  if (status !== verdict && !fs.existsSync(path.join(done, "render-error.json"))) return;
  return done;
}

async function check(input: Configuration, script: string, file: string, control: PosixControl, nonce: string, group: number): Promise<number> {
  let signalCode = 0;
  let stop!: (code: number) => void;
  const stopped = new Promise<number>((resolve) => { stop = resolve; });
  const interrupt = () => { signalCode ||= 130; stop(signalCode); };
  const terminate = () => { signalCode ||= 143; stop(signalCode); };
  process.on("SIGINT", interrupt);
  process.on("SIGTERM", terminate);
  try {
    const running = worker(script, file, { executable: input.nativeExecutable, arguments: input.nativeArguments, group,
      attach: (pid) => control.request(`attach:${nonce}:${pid}`, `attached:${nonce}:${pid}`) });
    void running.catch(() => {});
    const status = await Promise.race([running, stopped, control.lost]);
    await control.request(`drain:${nonce}`, `drained:${nonce}`);
    if (signalCode) return signalCode;
    const done = result(input.stage, status);
    if (!done) return 2;
    publishPosix(done, input.request.Output);
    if (input.completedRequest && (status === 0 || status === 1))
      await control.request(`result:${nonce}:${status}`, `result:${nonce}`);
    process.stdout.write(fs.readFileSync(path.join(input.request.Output, "report.md")));
    console.log(`\nWrote ${input.request.Output}`);
    return signalCode || status;
  } finally {
    process.off("SIGINT", interrupt);
    process.off("SIGTERM", terminate);
  }
}

export async function runPosix(script: string, arguments_: string[]): Promise<number> {
  const { file, fd, nonce, group } = handoff(arguments_);
  const control = new PosixControl(new Socket({ fd, readable: true, writable: true }));
  try {
    const input: Configuration = JSON.parse(fs.readFileSync(file, "utf8"));
    if (input.mode !== "check" || !path.isAbsolute(input.stage) || !path.isAbsolute(input.nativeExecutable) ||
        !Array.isArray(input.nativeArguments) || input.nativeArguments.some((value) => typeof value !== "string") ||
        !path.isAbsolute(input.request.Output) || process.cwd() !== input.request.Caller)
      throw new Error("Invalid statechart POSIX checking configuration.");
    const status = await check(input, script, file, control, nonce, group);
    await control.request(`complete:${nonce}`, `cleaned:${nonce}`, true);
    return status;
  } finally { control.close(); }
}
