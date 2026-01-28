# DynaDocs (dydo) - Complete Specification

This document contains everything needed to build the DynaDocs tool from scratch.

## Background & Context

### The Project
This is a full-stack content creation platform (LC) with:
- **Backend**: C# .NET 10, ASP.NET Core, Clean Architecture
- **Frontend**: React 19 + TypeScript
- **Microservices**: Python FastAPI
- **Docs location**: `docs/LC/`

### The Problem We're Solving

The project uses a dynamic documentation system where AI agents (Claude Code) read relevant docs based on their current task instead of loading everything. This requires:

1. **Predictable structure** - AI needs to know where things are
2. **Relative links** - AI can directly resolve paths without searching
3. **Consistent naming** - No spaces or weird casing that breaks path resolution
4. **Standard format** - Frontmatter and summaries help AI understand docs without reading everything
5. **Hierarchical navigation** - Start broad, drill down to specifics
6. **Graph connectivity** - Related docs link to each other bidirectionally

**The specific pain points:**
- Obsidian (the editor used for docs) converts relative links to `[[wikilinks]]` when files are moved
- Spaces in filenames cause path parsing issues
- No enforced structure means inconsistent docs
- No way to validate the docs are AI-traversable

### Existing Similar Tool
The project has `cs2ts` - a tool that generates TypeScript types from C# models. DynaDocs follows a similar pattern as an internal tooling project.

---

## Tool Specification

### Name
- **Full name**: DynaDocs
- **CLI command**: `dydo`
- **Meaning**: Dynamic Documentation

### Commands

```bash
dydo check              # Validate all docs, report violations
dydo check <path>       # Check specific file or directory
dydo fix                # Auto-fix issues that can be fixed automatically
dydo fix <path>         # Fix specific file or directory
dydo index              # Regenerate Index.md from doc structure (hubs only)
dydo graph <file>       # Show graph connections for a file
dydo graph <file> --incoming        # Show docs that link TO this file (backlinks)
dydo graph <file> --degree <n>      # Show docs within n link-hops (default: 1)
```

### Exit Codes
- `0` - Success, no issues
- `1` - Validation errors found
- `2` - Tool error (bad arguments, IO error, etc.)

---

## Documentation Philosophy

### Hierarchical Navigation (Top-Down)

The docs form a tree where you start broad and drill down:

```
Index.md (entry point)
    │
    ├── understand/_index.md (hub)
    │       ├── platform.md (detail)
    │       └── content/_index.md (sub-hub)
    │               ├── teases.md (detail)
    │               └── assets.md (detail)
    │
    ├── guides/_index.md (hub)
    │       └── backend/_index.md (sub-hub)
    │               └── database.md (detail)
    │
    └── project/_index.md (hub)
            └── decisions/_index.md (sub-hub)
                    └── 001-clean-architecture.md (detail)
```

**Key principle:** Index.md only links to top-level hubs. Hubs link to their children. This keeps each level manageable.

### Graph Connectivity (Lateral)

In addition to the tree, docs link laterally to related content:

```
teases.md ◄──────────► glossary.md#tease
    │
    └──────────────────► 001-tease-structure-decision.md
                                    │
database.md ◄──────────► ef-migration-pitfall.md
```

**Key principle:** If doc A mentions concept B, link to B. If decision X affects feature Y, both should link to each other.

### Hub Files (`_index.md`)

Every folder that contains multiple docs should have an `_index.md` that:
1. Provides a brief overview of what's in this folder
2. Lists and briefly describes each child doc
3. Helps AI decide which child to read

```markdown
---
area: general
type: hub
---

# Backend Guides

Guides for working with the C# backend, organized by subsystem.

## Contents

- [API Patterns](./api-patterns.md) - Minimal API conventions, endpoint structure
- [Database](./database.md) - EF Core, migrations, query patterns
- [Background Jobs](./background-jobs.md) - TickerQ, MassTransit consumers
```

### Clustering Rule

**When a folder exceeds ~7-10 items, create subfolders with their own `_index.md`.**

Before:
```
understand/
├── _index.md
├── platform.md
├── teases.md
├── choice-actions.md
├── assets.md
├── tokens.md
├── subscriptions.md
├── refunds.md
├── users.md
└── creators.md       # 10 items - getting unwieldy
```

After:
```
understand/
├── _index.md
├── platform.md
├── architecture.md
├── content/
│   ├── _index.md
│   ├── teases.md
│   ├── choice-actions.md
│   └── assets.md
├── commerce/
│   ├── _index.md
│   ├── tokens.md
│   ├── subscriptions.md
│   └── refunds.md
└── people/
    ├── _index.md
    ├── citizens.md
    └── creators.md
```

---

## Folder Structure

### Target Structure

```
docs/LC/
├── index.md                        # AI entry point - links to top-level hubs ONLY
├── glossary.md                     # Term definitions with #anchors
│
├── understand/                     # What things ARE (domain knowledge)
│   ├── _index.md                   # Hub: "Core concepts of the platform"
│   ├── platform.md                 # What is this, who is it for
│   ├── architecture.md             # System design overview
│   │
│   ├── content/                    # Cluster: content concepts
│   │   ├── _index.md
│   │   ├── teases.md
│   │   ├── choice-actions.md
│   │   └── assets.md
│   │
│   ├── commerce/                   # Cluster: monetization concepts
│   │   ├── _index.md
│   │   ├── tokens.md
│   │   ├── subscriptions.md
│   │   └── refunds.md
│   │
│   └── people/                     # Cluster: user concepts
│       ├── _index.md
│       ├── citizens.md
│       └── creators.md
│
├── guides/                         # How to DO things (task-oriented)
│   ├── _index.md                   # Hub: "Which guide for which task"
│   ├── backend/
│   │   ├── _index.md
│   │   ├── api-patterns.md
│   │   ├── database.md
│   │   └── background-jobs.md
│   ├── frontend/
│   │   ├── _index.md
│   │   ├── components.md
│   │   ├── state.md
│   │   └── styling.md
│   └── microservices/
│       ├── _index.md
│       └── asset-processing.md
│
├── reference/                      # Lookup (specs, configs)
│   ├── _index.md
│   ├── api-endpoints.md
│   ├── config.md
│   └── tools/
│       ├── _index.md
│       ├── cs2ts.md
│       └── dynadocs.md
│
└── project/                        # Meta: how we work
    ├── _index.md
    ├── docs-system.md              # How the docs work (this system!)
    ├── decisions/                  # ADRs (numbered)
    │   ├── _index.md               # List of all decisions
    │   ├── 001-clean-architecture.md
    │   └── 002-react-compiler.md
    ├── pitfalls/                   # Known gotchas (named by problem)
    │   ├── _index.md
    │   └── ef-migration-conflicts.md
    └── changelog/                  # Session notes (dated)
        ├── _index.md
        └── 2025/                   # Year folder
            └── 2025-01-15/         # Date folder (YYYY-MM-DD)
                └── react-compiler.md
```

### Folder Purposes

| Folder | Question it answers | Content type |
|--------|---------------------|--------------|
| `understand/` | "What IS this?" | Domain concepts, business logic, architecture |
| `guides/` | "How do I DO this?" | Step-by-step task instructions |
| `reference/` | "What are the specs?" | API docs, config options, tool docs |
| `project/` | "Why/how do we work?" | Decisions, pitfalls, changelog, docs-about-docs |

---

## Rules to Implement

### Rule 1: Naming Convention (kebab-case)

**What**: All file and folder names must be kebab-case (lowercase, hyphens for spaces).

**Valid examples**:
```
coding-standards.md
tease-player-architecture.md
cdn-api-usage.md
_index.md
understand/
commerce/
```

**Invalid examples**:
```
Coding Standards.md      # Spaces and capitals
CodingStandards.md       # PascalCase
coding_standards.md      # snake_case (kebab preferred)
CODING-STANDARDS.md      # UPPERCASE
```

**Exceptions**:
- `CLAUDE.md` - All caps is the convention for these meta files
- `.gitkeep` - Standard convention

**Auto-fix**: Rename files to kebab-case, update all references.

### Rule 2: Relative Links Only

**What**: All internal links must be relative paths in standard markdown format.

**Valid**:
```markdown
[Coding Standards](./guidelines/coding-standards.md)
[CDN API](../backend/cdn-api-usage.md)
[Overview](./overview.md)
[Tease definition](../glossary.md#tease)
```

**Invalid**:
```markdown
[[coding-standards]]                    # Wikilink format
[[guidelines/coding-standards]]         # Wikilink with path
[Coding Standards](coding-standards)    # Missing .md extension
[Coding Standards](/absolute/path.md)   # Absolute path
```

**Auto-fix**:
- Convert `[[filename]]` to `[filename](./resolved/path/to/filename.md)`
- Requires scanning all docs to find the actual file location
- If ambiguous (multiple files with same name), report error instead of guessing

### Rule 3: Frontmatter Required

**What**: Every doc must have YAML frontmatter with required fields.

**Required format**:
```markdown
---
area: frontend | backend | microservices | platform | general
type: hub | concept | guide | reference | decision | pitfall | changelog
---

# Title

Brief summary paragraph here (1-3 sentences).

---

[Rest of content]
```

**Fields**:
- `area` (required): One of `frontend`, `backend`, `microservices`, `platform`, `general`
- `type` (required): One of `hub`, `concept`, `guide`, `reference`, `decision`, `pitfall`, `changelog`
- `status` (required for decisions): One of `proposed`, `accepted`, `deprecated`, `superseded`
- `date` (required for decisions and changelog): `YYYY-MM-DD` format

**Auto-fix**: Cannot auto-fix (requires human judgment), but can add template frontmatter for human to fill in.

### Rule 4: Summary Required

**What**: First paragraph after the title must be a 1-3 sentence summary.

**Valid**:
```markdown
---
area: backend
type: guide
---

# CDN API Usage

This guide covers how to upload and manage files using the Cloudflare R2 CDN integration. Read this when implementing file upload features.

---

## Setup
...
```

**Invalid**:
```markdown
---
area: backend
type: guide
---

# CDN API Usage

## Setup           <-- Jumps straight to heading, no summary
...
```

**Auto-fix**: Cannot auto-fix, report as warning.

### Rule 5: No Broken Links

**What**: All internal links must point to files that exist. Anchor links (e.g., `glossary.md#tease`) must point to existing anchors.

**Check**: Resolve each relative link from the source file's location and verify the target exists.

**Auto-fix**: Cannot auto-fix, report as error.

### Rule 6: Hub Files Required

**What**: Every folder containing docs must have an `_index.md` file.

**Purpose**: Ensures every folder has a navigable entry point.

**Auto-fix**: Can create skeleton `_index.md` with folder name as title.

### Rule 7: No Orphan Docs

**What**: Every doc (except `index.md`) should be reachable from `index.md` through some link path.

**Purpose**: Ensures no docs get "lost" and forgotten.

**Auto-fix**: Cannot auto-fix, report as warning.

---

## Document Type Definitions

| Type | Purpose | Location | Naming |
|------|---------|----------|--------|
| `hub` | Entry point for a folder, lists children | `_index.md` in any folder | Always `_index.md` |
| `concept` | Explains what something IS | `understand/` | Named by concept |
| `guide` | How to accomplish a task | `guides/` | Named by task |
| `reference` | Specs, APIs, configs | `reference/` | Named by subject |
| `decision` | ADR - why we decided something | `project/decisions/` | `NNN-topic.md` |
| `pitfall` | Common mistake to avoid | `project/pitfalls/` | Named by problem |
| `changelog` | Session notes, what changed | `project/changelog/{YYYY}/{YYYY-MM-DD}/` | `topic.md` |

---

## Linking Conventions

### Inline Links (First Mention)

Link the first meaningful occurrence of a term in a document:

```markdown
The [Tease](../glossary.md#tease) editor allows creators to add
[ChoiceActions](./choice-actions.md) which can have premium options
that cost [tokens](../commerce/tokens.md).
```

Don't link every occurrence - that's noisy. Just the first mention.

### Related Section

At the bottom of docs, add a "Related" section for things that are connected but weren't naturally mentioned in the body:

```markdown
## Related

- [Token Economy](../commerce/tokens.md) - Full monetization details
- [ADR-006: Refund Window](../../project/decisions/006-refund-window.md) - Why 6 minutes
```

### Bidirectional Linking for Decisions/Pitfalls

When a decision or pitfall affects other docs, link both directions:

**In the concept doc:**
```markdown
# understand/commerce/refunds.md

Users can refund token purchases within 6 minutes.
See [ADR-006](../../project/decisions/006-refund-window.md) for why this duration.
```

**In the decision doc:**
```markdown
# project/decisions/006-refund-window.md

## Affects

- [Refunds](../../understand/commerce/refunds.md)
- [Token Economy](../../understand/commerce/tokens.md)
```

### Glossary Links

The glossary uses anchors for each term. Link to specific terms:

```markdown
[Tease](./glossary.md#tease)
[Citizen](./glossary.md#citizen)
```

---

## Initial setup

The `dydo init` command generates the high level folder structure which should be used along with 

## Index.md Generation

The `dydo index` command generates `docs/LC/index.md` with links to **top-level hubs only**, not every file.

**Generated format:**
```markdown
# LC Documentation Index

This is the entry point for AI agents and humans exploring the LC documentation.

## How to Navigate

1. Start with [Platform Overview](./understand/platform.md) if you're new
2. Browse by purpose below
3. Use the [Glossary](./glossary.md) for term definitions

## Documentation Sections

### Understand - What Things Are
[Understanding the Platform](./understand/_index.md) - Core concepts, domain knowledge, architecture

### Guides - How to Do Things
[Development Guides](./guides/_index.md) - Task-oriented guides for backend, frontend, microservices

### Reference - Specs and Lookups
[Reference Documentation](./reference/_index.md) - API specs, configuration, tool documentation

### Project - How We Work
[Project Meta](./project/_index.md) - Decisions, pitfalls, changelog, docs system
```

**Key principle:** Index.md is shallow. It links to hubs. Hubs link to details.

---

## Project Structure (Tool)

```
tools/
└── DynaDocs/
    ├── DynaDocs.csproj
    ├── Program.cs              # Entry point, CLI parsing
    │
    ├── Commands/
    │   ├── CheckCommand.cs     # dydo check
    │   ├── FixCommand.cs       # dydo fix
    │   ├── IndexCommand.cs     # dydo index
    │   ├── GraphCommand.cs     # dydo graph
    │   ├── AgentCommand.cs     # dydo agent (claim/release/status/list/role)
    │   ├── DispatchCommand.cs  # dydo dispatch
    │   ├── InboxCommand.cs     # dydo inbox (list/show/clear)
    │   ├── GuardCommand.cs     # dydo guard (hook enforcement)
    │   ├── ReviewCommand.cs    # dydo review complete
    │   ├── TaskCommand.cs      # dydo task (create/ready-for-review/approve/reject)
    │   └── CleanCommand.cs     # dydo clean
    │
    ├── Rules/
    │   ├── IRule.cs            # Interface for rules
    │   ├── NamingRule.cs       # Kebab-case validation
    │   ├── RelativeLinksRule.cs # Link format validation
    │   ├── FrontmatterRule.cs  # Frontmatter validation
    │   ├── SummaryRule.cs      # Summary paragraph check
    │   ├── BrokenLinksRule.cs  # Link target exists check
    │   ├── HubFilesRule.cs     # _index.md exists in folders
    │   └── OrphanDocsRule.cs   # All docs reachable from index
    │
    ├── Models/
    │   ├── DocFile.cs          # Represents a parsed doc
    │   ├── Frontmatter.cs      # Parsed frontmatter
    │   ├── Violation.cs        # A rule violation
    │   ├── LinkInfo.cs         # Parsed link information
    │   ├── AgentState.cs       # Agent status, role, task, PID
    │   ├── AgentSession.cs     # Session info (PIDs, timestamps)
    │   ├── InboxItem.cs        # Dispatch message in inbox
    │   └── TaskFile.cs         # Task metadata and status
    │
    ├── Services/
    │   ├── DocScanner.cs       # Finds and parses all docs
    │   ├── LinkResolver.cs     # Resolves relative paths
    │   ├── MarkdownParser.cs   # Parses markdown, extracts links/frontmatter
    │   ├── AnchorExtractor.cs  # Extracts #anchors from markdown headings
    │   ├── IndexGenerator.cs   # Generates Index.md
    │   ├── DocGraph.cs         # Graph operations (incoming links, degree traversal)
    │   ├── AgentRegistry.cs    # Agent states, claim/release, PID tracking
    │   ├── WorkspaceManager.cs # Agent workspace operations
    │   └── ProcessUtils.cs     # PID walking, process tree utilities
    │
    └── Utils/
        └── PathUtils.cs        # Path normalization, kebab-case conversion
```

### Dependencies (NuGet)

```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="Markdig" Version="0.34.0" />
<PackageReference Include="YamlDotNet" Version="15.1.0" />
```

- **System.CommandLine**: CLI parsing (Microsoft's official library)
- **Markdig**: Markdown parsing (extract links, structure)
- **YamlDotNet**: YAML frontmatter parsing

---

## Implementation Notes

### Markdown Link Regex

To find markdown links:
```csharp
// Standard markdown links: [text](path)
var markdownLinkRegex = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

// Wikilinks: [[path]] or [[path|display]]
var wikilinkRegex = new Regex(@"\[\[([^\]|]+)(?:\|[^\]]+)?\]\]", RegexOptions.Compiled);
```

### Frontmatter Parsing

Frontmatter is YAML between `---` markers at the start of file:
```csharp
if (content.StartsWith("---"))
{
    var endIndex = content.IndexOf("---", 3);
    var yaml = content.Substring(3, endIndex - 3);
    var frontmatter = yamlDeserializer.Deserialize<Frontmatter>(yaml);
}
```

### Anchor Extraction

Extract anchors from markdown headings for link validation:
```csharp
// Headings become anchors: "## My Heading" -> #my-heading
var headingRegex = new Regex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline);
foreach (Match match in headingRegex.Matches(content))
{
    var anchor = match.Groups[1].Value
        .ToLowerInvariant()
        .Replace(" ", "-")
        .Replace(Regex.Match(input, @"[^\w\-]"), "");
    anchors.Add(anchor);
}
```

### Path Resolution

When resolving `[text](./relative/path.md)` from file at `docs/LC/backend/guide.md`:
```csharp
var sourceDir = Path.GetDirectoryName(sourceFile);  // docs/LC/backend
var resolved = Path.GetFullPath(Path.Combine(sourceDir, relativePath));
// Normalize to forward slashes for consistency
resolved = resolved.Replace('\\', '/');
```

### Graph Building and Traversal

Build the graph from parsed docs, then traverse for queries:
```csharp
public class DocGraph
{
    // Built during doc scanning
    private Dictionary<string, List<string>> _outgoing = new();  // doc -> docs it links to
    private Dictionary<string, List<string>> _incoming = new();  // doc -> docs that link to it

    public void AddLink(string from, string to)
    {
        if (!_outgoing.ContainsKey(from)) _outgoing[from] = new();
        _outgoing[from].Add(to);

        if (!_incoming.ContainsKey(to)) _incoming[to] = new();
        _incoming[to].Add(from);
    }

    // Get docs that link TO this file (backlinks)
    public List<string> GetIncoming(string doc) => _incoming.GetValueOrDefault(doc, new());

    // BFS traversal for degree-based expansion
    public List<(string Doc, int Degree)> GetWithinDegree(string startDoc, int maxDegree)
    {
        var result = new List<(string, int)>();
        var visited = new HashSet<string> { startDoc };
        var queue = new Queue<(string Doc, int Degree)>();

        queue.Enqueue((startDoc, 0));

        while (queue.Count > 0)
        {
            var (doc, degree) = queue.Dequeue();
            if (degree > 0) result.Add((doc, degree));
            if (degree >= maxDegree) continue;

            foreach (var linked in _outgoing.GetValueOrDefault(doc, new()))
            {
                if (visited.Add(linked))
                    queue.Enqueue((linked, degree + 1));
            }
        }
        return result;
    }
}
```

### Kebab-Case Conversion

```csharp
public static string ToKebabCase(string input)
{
    // "Coding Standards.md" -> "coding-standards.md"
    // "CodingStandards.md" -> "coding-standards.md"

    // Handle spaces
    var result = input.Replace(' ', '-');

    // Handle PascalCase - insert hyphen before capitals
    result = Regex.Replace(result, "([a-z])([A-Z])", "$1-$2");

    // Lowercase everything
    result = result.ToLowerInvariant();

    // Collapse multiple hyphens
    result = Regex.Replace(result, "-+", "-");

    return result;
}
```

---

## Example Output

### `dydo check` output

```
Checking docs/LC/...

ERRORS:
  docs/LC/backend/CDN_API_USAGE.md
    - Filename should be kebab-case: cdn-api-usage.md
    - Line 15: Wikilink found: [[redis-task-queue-architecture]]
    - Missing frontmatter

  docs/LC/frontend/TeasePlayer docs.md
    - Filename contains spaces: tease-player-docs.md
    - Missing frontmatter
    - Missing summary paragraph

  docs/LC/guides/backend/
    - Missing _index.md hub file

WARNINGS:
  docs/LC/understand/architecture.md
    - No 'last_updated' in frontmatter (optional)

  docs/LC/microservices/QUICK_START.md
    - Filename should be kebab-case: quick-start.md

  docs/LC/reference/old-api-notes.md
    - Orphan doc: not reachable from index.md

Found 8 errors, 3 warnings in 16 files.
```

### `dydo fix` output

```
Fixing docs/LC/...

FIXED:
  ✓ Renamed CDN_API_USAGE.md -> cdn-api-usage.md
  ✓ Renamed TeasePlayer docs.md -> tease-player-docs.md
  ✓ Renamed QUICK_START.md -> quick-start.md
  ✓ Updated 3 links in index.md
  ✓ Converted 5 wikilinks to relative paths
  ✓ Created docs/LC/guides/backend/_index.md (skeleton)

NEEDS MANUAL FIX:
  ✗ docs/LC/backend/cdn-api-usage.md - Add frontmatter
  ✗ docs/LC/frontend/tease-player-docs.md - Add frontmatter, summary
  ✗ docs/LC/reference/old-api-notes.md - Link from somewhere or delete

Fixed 6 issues automatically. 3 issues require manual attention.
```

### `dydo index` output

```
Generating index.md...

Scanned 4 top-level hubs:
  - understand/_index.md (12 docs)
  - guides/_index.md (8 docs)
  - reference/_index.md (5 docs)
  - project/_index.md (7 docs)

Generated docs/LC/index.md
```

### `dydo graph` output

```
$ dydo graph tokens.md --incoming

Incoming links to tokens.md (4 docs link here):
  understand/commerce/subscriptions.md:23
  understand/commerce/refunds.md:15
  guides/backend/payment-processing.md:42
  glossary.md:156

$ dydo graph tokens.md --degree 2

tokens.md
├── [degree 1] subscriptions.md
│   ├── [degree 2] subscription-tiers.md
│   └── [degree 2] billing-cycles.md
├── [degree 1] refunds.md
│   └── [degree 2] customer-support.md
├── [degree 1] glossary.md
└── [degree 1] _index.md (commerce)

Found 8 docs within 2 hops of tokens.md

$ dydo graph tokens.md --incoming --degree 2

Incoming (4 docs):
  subscriptions.md:23
  refunds.md:15
  payment-processing.md:42
  glossary.md:156

Outgoing within 2 hops (8 docs):
  [degree 1] subscriptions.md, refunds.md, glossary.md, _index.md
  [degree 2] subscription-tiers.md, billing-cycles.md, customer-support.md, pricing.md
```

---

## Integration with CLAUDE.md

The `CLAUDE.md` file at project root says:

```markdown
**Before starting any non-trivial work, read [docs/LC/index.md](./docs/LC/index.md).**
```

This is the entry point. The index.md links to hubs, hubs link to details.

---

## Agent Workflow System

A multi-agent orchestration system that provides persistent state, role-based permissions, and cross-agent coordination.

### Agent Pool

26 predefined agents (A-Z), each with their own workspace:

| Agent | Letter | Agent | Letter |
|-------|--------|-------|--------|
| Adele | A | Noah | N |
| Brian | B | Olivia | O |
| Charlie | C | Paul | P |
| Dexter | D | Quinn | Q |
| Emma | E | Rose | R |
| Frank | F | Sam | S |
| Grace | G | Tara | T |
| Henry | H | Uma | U |
| Iris | I | Victor | V |
| Jack | J | Wendy | W |
| Kate | K | Xavier | X |
| Leo | L | Yara | Y |
| Mia | M | Zack | Z |

### Workspace Structure

```
.workspace/
├── agent-states.md          # Central registry of all agents
├── Adele/
│   ├── workflow.md          # Agent-specific workflow instructions
│   ├── state.md             # Current role, permissions, progress
│   ├── .session             # Session info (PID, timestamps)
│   └── inbox/               # Messages from other agents
├── Brian/
│   └── ...
├── Charlie/
│   └── ...
└── ... (all 26 agents)
```

### Agent Identification via PID

Each terminal has a unique process ID. When an agent claims, we register the terminal's PID.

**Process tree:**
```
Terminal (PowerShell) ─── PID: 1000
    └── Claude Code ───── PID: 1001
          └── Hook (dydo) ─ PID: 1002
```

**Claim flow:**
1. User starts: `claude --feature C`
2. Claude reads index.md, learns: "C means I'm Charlie"
3. Claude runs: `dydo agent claim Charlie`
4. dydo walks process tree, records terminal PID
5. Writes `.workspace/Charlie/.session`:
   ```json
   {
     "agent": "Charlie",
     "terminal_pid": 1000,
     "claude_pid": 1001,
     "claimed": "2025-01-28T10:30:00Z"
   }
   ```
6. Updates `agent-states.md`

**Guard flow (hook calls):**
1. Hook fires: `dydo guard --action edit --path src/Auth.cs`
2. dydo gets parent PID (Claude Code process)
3. Scans `.session` files for matching PID
4. Finds agent, checks permissions from `state.md`
5. Returns allow/deny

### Agent Roles

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `docs/**`, `project/**` |
| `reviewer` | (nothing) | (everything) |
| `docs-writer` | `docs/**` | `src/**`, `tests/**`, `project/**` |
| `interviewer` | `.workspace/{self}/**` | Everything else |
| `planner` | `.workspace/{self}/**`, `project/tasks/**` | `src/**`, `docs/**` |

### Agent States Registry (agent-states.md)

```markdown
---
last-updated: 2025-01-28T10:45:00Z
---

# Agent States

| Agent | Status | Role | Task | Since |
|-------|--------|------|------|-------|
| Adele | working | code-writer | jwt-auth | 10:30 |
| Brian | free | - | - | - |
| Charlie | working | reviewer | jwt-auth-review | 10:45 |
| Dexter | free | - | - | - |
| ...

## Pending Inbox

| Agent | Items | Oldest |
|-------|-------|--------|
| Emma | 2 | 09:15 |
```

### Workflow Modes

Triggered by flags passed to Claude:

| Flag | Workflow | Steps |
|------|----------|-------|
| `--feature X` | Full | Interview → Plan → Implement → Review → Docs |
| `--task X` | Standard | Plan → Implement → Review |
| `--quick X` | Light | Just implement |
| `--inbox X` | Process inbox | Handle pending dispatches |
| `--review X` | Review mode | Code review only |

The letter X determines the agent (A=Adele, B=Brian, etc.).

### Task Lifecycle

```
pending → active → review-pending → human-reviewed → closed
                        ↓
                   review-failed → active
```

**Task file when ready for review:**
```markdown
---
status: review-pending
review-summary: |
  Implemented JWT authentication with refresh token rotation.
  - JwtService handles token generation/validation
  - 23 tests added, all passing
files-changed:
  - src/Auth/JwtService.cs (new)
  - src/Auth/AuthMiddleware.cs (new)
  - tests/Auth/JwtServiceTests.cs (new)
---
```

### Cross-Agent Dispatch

Agent A needs review. Agent A runs:

```bash
dydo dispatch \
  --role reviewer \
  --task jwt-auth \
  --brief "Review JWT implementation" \
  --files "src/Auth/**" \
  --context-file ".workspace/Adele/review-context.md"
```

**dydo dispatch:**
1. Finds first free agent alphabetically (Charlie)
2. Updates `agent-states.md`: Charlie → working
3. Writes to `.workspace/Charlie/inbox/`:
   ```markdown
   ---
   from: Adele
   role: reviewer
   task: jwt-auth
   received: 2025-01-28T10:45:00Z
   ---

   # Review Request

   ## Brief
   Review JWT implementation for security and correctness.

   ## Files to Review
   - src/Auth/JwtService.cs
   - src/Auth/AuthMiddleware.cs
   ```
4. Launches new terminal: `start powershell -Command "claude --inbox C"`
5. Returns: "Dispatched to Charlie"

**Review completion:**
```bash
# Pass
dydo review complete jwt-auth --status pass --notes "LGTM"

# Fail (same agent can fix - context continuity)
dydo review complete jwt-auth --status fail --notes "Security issue on line 42"
```

### Agent Commands

```bash
# Lifecycle
dydo agent claim <letter>            # Claim agent for this terminal
dydo agent release                   # Release current agent
dydo agent status [letter]           # Show agent status
dydo agent list                      # List all agents
dydo agent list --free               # List free agents
dydo agent role <role> [--task X]    # Set role and permissions

# Dispatch
dydo dispatch --role <role> --task <name> --brief "..." [--files "..."]

# Inbox
dydo inbox list                      # Agents with pending items
dydo inbox show                      # Show current agent's inbox
dydo inbox clear                     # Clear processed items

# Guard (called by hooks)
dydo guard --action <tool> --path <file>

# Review
dydo review complete <task> --status pass|fail [--notes "..."]

# Task lifecycle
dydo task ready-for-review <name> --summary "..."
dydo task approve <name>             # Human only
dydo task reject <name> --notes "..."  # Human only
dydo tasks --needs-review            # List tasks awaiting human review

# Cleanup
dydo clean <letter>                  # Clean agent workspace
dydo clean --all                     # Clean all (denied if any working)
dydo clean --task <name>             # Clean workspaces for task
```

### Hook Configuration

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard --action edit --path \"$FILE_PATH\""
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "dydo workflow check --on-stop"
          }
        ]
      }
    ]
  }
}
```

### Example: Full Feature Workflow

```
Human: claude --feature A "Implement JWT authentication"

Terminal A (Adele):
  1. Reads index.md → learns workflow
  2. dydo agent claim Adele
  3. dydo agent role interviewer
  4. Interviews human, clarifies requirements
  5. Writes .workspace/Adele/brief.md
  6. dydo agent role planner
  7. Creates plan from brief
  8. dydo task create jwt-auth
  9. dydo agent role code-writer
  10. Implements feature
  11. Updates task progress
  12. dydo task ready-for-review jwt-auth --summary "..."
  13. dydo dispatch --role reviewer --task jwt-auth --brief "..."
      → Launches Terminal B (Brian)

Terminal B (Brian):
  1. claude --inbox B
  2. dydo agent claim Brian
  3. Reads inbox, sees review request
  4. Reviews code (read-only)
  5. dydo review complete jwt-auth --status pass
  6. Creates changelog entry
  7. dydo dispatch --role docs-writer --task jwt-auth-docs --brief "..."
      → Launches Terminal C (Charlie)
  8. dydo agent release

Terminal C (Charlie):
  1. claude --inbox C
  2. dydo agent claim Charlie
  3. Reads inbox, sees docs request
  4. Writes documentation
  5. dydo task complete jwt-auth-docs
  6. dydo agent release

Human reviews, approves via: dydo task approve jwt-auth
Feature complete. Changelog linked. Docs written.
```

---

## Future Enhancements (Not for v1)

1. **Watch mode**: `dydo watch` - Continuously validate on file changes
2. **Pre-commit hook**: Validate docs before commit
3. **Graph visualization**: Generate a visual map of doc connections (Mermaid/DOT export)
4. **Staleness detection**: Warn if docs haven't been updated in N months
5. **Coverage report**: What areas have good docs vs sparse docs
6. **Auto-clustering**: Suggest when folders should be split based on item count

---

## Summary

Build a C# console tool called DynaDocs (`dydo`) that:

1. **Validates** docs against naming, linking, structure, and organization rules
2. **Auto-fixes** what it can (renames, link conversion, skeleton hubs)
3. **Generates** index.md from doc structure (hubs only, not all files)
4. **Queries** the doc graph (backlinks, degree traversal) for context gathering
5. **Orchestrates** multi-agent workflows with role-based permissions and PID tracking

The goal is to keep the `docs/LC/` directory in a state that's easily traversable by both humans (in Obsidian) and AI agents (Claude Code), enabling dynamic context loading based on current task.

### Key Principles

1. **Hierarchical navigation**: Index → Hubs → Details (top-down)
2. **Graph connectivity**: Related docs link to each other (lateral)
3. **Clustering**: Subfolders with `_index.md` when folders get large (7-10+ items)
4. **Bidirectional linking**: Decisions and pitfalls link to and from affected docs
5. **Relative paths**: No wikilinks, always `[text](./path.md)` format
6. **Kebab-case**: All filenames lowercase with hyphens
