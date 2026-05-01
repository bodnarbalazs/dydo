---
id: 122
area: backend
type: issue
severity: critical
status: resolved
found-by: inquisition
date: 2026-04-28
resolved-date: 2026-04-30
---

# Watchdog kills Linux terminal emulator process via argv leak

Resolved critical-severity bug — independent root cause of Linux agent deaths. Linux terminal emulators inherit the launcher's argv, so `FindProcessesByCommandLineLinux`'s substring match on `{agent} --inbox` returned the terminal-emulator PID alongside bash/claude. `ShellProcessNames` didn't list the emulators, so the watchdog killed the host terminal and took its bash + claude children with it. Fixed in commit `06512de` by whitelisting kill targets to `ClaudeProcessNames` so terminal emulators no longer match.

## Description

Independent root cause of agent deaths on Linux (e.g. C:/Users/User/Desktop/LC project).

**Mechanism.** Every Linux terminal in `Services/TerminalLauncher.cs:73-90` is launched as e.g. `gnome-terminal -- bash -c "...claude '{agent} --inbox'..."`. The terminal-emulator process inherits this argv. `ProcessUtils.FindProcessesByCommandLineLinux` (Services/ProcessUtils.CommandLine.cs:100-119) reads `/proc/{pid}/cmdline` and substring-matches `{agent} --inbox` — returning the terminal-emulator PID alongside (or instead of) the bash/claude PID. `WatchdogService.ShellProcessNames` (Services/WatchdogService.cs:9-12) only contains `{powershell, pwsh, bash, sh, cmd, zsh}` — the Linux terminal emulators (`gnome-terminal`, `konsole`, `xfce4-terminal`, `alacritty`, `kitty`, `wezterm`, `tilix`, `foot`, `xterm`) are not protected. Killing the terminal emulator takes down its bash child and claude grandchild — the entire tab vanishes mid-work.

This is independent of finding #1: even without the redispatch race, every legitimate auto-close kill on Linux destroys the host terminal. Combined with #1, the kill happens before the agent has even released.

**Suggested fix (preferred).** In `PollAndCleanup`, restrict kills to processes whose `ProcessName` is `claude` (Mac/Linux) or `node` (Windows), or whose parent chain runs through the matched bash. The 'kill the actual target' approach is durable.

**Suggested fix (one-line alternative).** Extend `ShellProcessNames` with the Linux terminal emulators above, plus `osascript` (Mac) and `wt` (Windows) for defense in depth.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #2).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed by 06512de (whitelist kill targets to ClaudeProcessNames; Linux terminal emulator no longer matches).