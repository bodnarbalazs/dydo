
# Dydo

## What it is
Dydo is my opinionated workflow to build software projects with agents.
It has:
- Skills
	- Telling agents how to get work done, what roles they should play and how they should interact with the other agents
- A CLI tool which
	- Generates skills/agents from templates to Codex and Claude Code
	- Has a nudge system which uses Regex patterns in a guard hook to deny/warn on agent actions with a helpful messages like
		- Don't hand-edit migrations, use "X" instead.
		- Don't invoke "this command" like this use it "like this"
	- Provides other handy commands like
		- Proxies a python swiss-army knife test runner script
		- Checks for broken links in the docs
- Documentation structure
	- Diátaxis [source] inspired structure
		- Guides
		- Reference
		- Understand
		- Project
		- Glossary
	- Auto generated placeholder files like
		- About
		- Architecture
		- Coding Standards

## How to use it
Don't. Seriously. Build your own system.
Because it should reflect:
- How you work
- What you work on
- With what stack
- What "good" (code) looks like to you

So I suggest you cherry pick ideas, fork the project, point your agent at it, steal what you need.
You can use it as is (as I use it), but it will reflect MY values and not necessarily yours.
It is built to be customizable, but only to a degree. If you still want to give it a try as is point your agent to [onboarding file] and follow their instructions.
## Rant (feel free to skip)
I started building DyDo, back then DynaDocs, "Dynamic documentation", because Claude didn't work like I wanted it to. Every time I had to reexplain it things about the project, what it's job would be, what it shouldn't mess up (and it did every new session if I didn't tell it) and the Claude.md just wasn't enough. So I "invented" an onboarding system where the Claude.md would tell them that they were invoked with either a name like "Adele" or they should "claim" their own identity then follow the further instructions inside their folder which doubled as their workspace as well. So "Adele" arrived at "Project/dydo/agents/Adele/index.md" and was presented with a choice on what role they would play. There were co-thinker, planner, code-writer, test-writer, docs-writer, reviewer, later orchestrator, inquisitor. And then they would be presented with their job-description, tools, and access. Because co-thinker's couldn't write code outside of their workspace, code-writers couldn't write tests, test-writers couldn't have access to the rest of the code (yes, this was stupid in hindsight).
The guard enforced all of this. If they didn't claim their identity they couldn't even read files not needed for that. If they didn't read the files which were the "must reads" for their role they couldn't read or write, they couldn't get their task done otherwise. I didn't want to waste the context and attention on coding-standards of an agent who is brainstorming with me and someone who's job is to write the damn code shouldn't be bored with what the docs structure looks like. Progressive disclosure reinvented from first principles.
Then I felt like a flight-traffic controller juggling many agents so I made orchestrator agents. Mind you this was way before sub-agents were a thing. It was glorious. These agents were able to use "dydo dispatch" to open a new terminal and spin up another top-level agent in it, which would onboard, look at it's inbox and start to work. 4-5 terminal tabs in each terminal window. Then I got tired of closing them when they were done so I added that as well. And it worked all nice until the terminal app crashed and I had to clean up stale identities and try to resume sessions.
Then in 2.0 I learned the lesson that I shouldn't build stuff which Openai and Anthropic would build anyways - and build it better, because this is not my main product, it's their's. The goal is that dydo should be my workflow and customization on top of their harnesses. So I embraced skills (and I no longer enforce that they actually read them). In 2.0 I had a notion sync and view cooked up so it would provide a better interface to interact with the project management records. I didn't like it. So in 3.0 I made the explicit decision that this should support my work and I won't think about "backwards compatibility" or generally anything which I would do if this was a product meant to be used by others. I built the entire new system around Matt Pocock's wayfinder skill and the divide and conquer idea within it. I had adapted a bunch of other skills from others like: [insert list here] and harmonized them with each other. I also given up on hand rolling the PM layer and adopted Linear as the platform to use and removed all the notion sync stuff.

## Overview
So how does dydo work in practice?

I break the work with a software product into many Linear projects. Each project is a map, where the issues are the nodes, they are marked with statuses and labels. Each project is driven by an Admiral, who's job is to chart the map, manage the PM surface coordinate the Issue Captain's who oversee the completion of the Issues with their crew of (specifiers, implementers, hardeners and reviewers).

Each issue is either AFK or HITL
- AFK is completed without me if everything goes according to plan
- HITL happens with my direct involvement and guidance

These are the types of issues:
- Feature
	- Some new feature is being built or improved upon
- Bug
	- A bug gets reported, then it gets reproduced and fixed
- Enablement
	- Some human-involved setup which other work depends on (creating an api key)
- Inquisition
	- An audit where sub agents not only look at the code like reviewers, but also try to come up with ways which it could break and they test these hypotheses out to verify them
- Grilling
	- An agent asks me questions about my intent and vision, I understand and weigh the choices and out of these session come Decision Records, plans and further issues which can be navigated
- Merge
	- Each project is a feature branch, each issue where code gets written is a sub branch and each lane or sub issue within that issue also get's it's child branch so agents never collide. When these are merged back to their parent, it's a merge issue.
- Prototype
	- An interactive session where throwaway code gets created mostly for UI decisions, the winner code may become preserved as a starting guide for the actual implementation
- Question
	- When some decision is needed which is not answerable from any previous artifact (implementation plan, DR, docs, etc.) work stops until it's resolved so things don't go in incorrect ways
- Research
	- When some question needs to be answered there is a research work where an agent sends out scouts then verifies the evidence and presents the findings
- Walkthrough
	- Happens each time the feature is merged mack to master (or when requested), this is the final, hands-on review where things get spot checked by me and I get in touched with the codebase so I understand what's going on and if I don't like something I'll send it back to be fixed

Here are some nice charts about the workflow:

[insert charts here] (not all)

here's the further workflow documented: [isnert control flow]
and here's the Linear setup dydo and the agents expect: [linear spec]