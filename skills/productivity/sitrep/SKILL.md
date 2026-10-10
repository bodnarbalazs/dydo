---
name: sitrep
description: "Where are we? Tell me what happened, where it stands, and what needs me."
argument-hint: "Anything to focus on? (optional)"
disable-model-invocation: true
---

# Sitrep

The user is arriving cold. Assume he has a bunch of parallel threads going on and his attention is limited, therefore
he may have seen little or none of this session: not the files you read, the detours you took, or the reasons you turned. 
Give him a situation report he can act on without needing to look up anything.

## 1. Re-check the ground

Report the state as it is now, not as you remember it. Re-read whatever your work touched: `git status`
and `git log` on the branches you used, open PRs and their checks, Issues you moved, agents you
spawned and whether each is still running, returned or dead. Anything you could not re-check is
marked _from memory_ in the report.

Done when every thread the session opened has a current, checked state: finished, in progress,
parked, abandoned, or waiting on the user.

## 2. Write the report

Write for a reader who knows the project in general but none of this session. Name each Issue, branch,
file, tool and project term with one clause on what it is the first time it appears; take domain
terms from `dydo/glossary.md` and work-model terms from `dydo/reference/dydo-glossary.md`, both
read from the repository root. Prose follows `writing-for-humans` with ASD-STE100 English.

Five parts, in this order:
- **Bottom line.** Two sentences: what is going on, and whether anything is waiting on the user.
- **How we got here.** The chain from the original ask to now, each link with its _because_: "I
  started on <issue-X> (the export bug); fixing it needed the parser change from <issue-A>, so I switched
  to that; <issue-Y> is where I stopped." Detours and dead ends stay in the chain with what they taught.
- **Where things stand.** Each thread with its checked state and where it lives: branch, commit, PR,
  Issue, file path, scratch file. Say what is verified and how, and what is written but unproven.
- **What needs you.** Each ask as a decision the user can take from this report alone: the question,
  why it came up, what it blocks, the options with what each one leads to, and your recommendation.
  When nothing needs him, say so plainly. If there are PR-s to merge provide links.
- **What happens next.** What you do once he answers, and what stays waiting until he does.
  If the ticket is resolved and the user can close the thread without any further acts on his behalf say plainly that the "THE THREAD CAN BE CLOSED".

Done when someone who saw nothing of the session can answer, from the report alone: what was asked,
what got done, where it is, what is stuck on him and why, and what comes next.

## 3. Hand the turn back

The report ends the turn. Work resumes on the user's word.

When the user passed an argument, lead with the threads it names and keep the rest to their
bottom lines.
