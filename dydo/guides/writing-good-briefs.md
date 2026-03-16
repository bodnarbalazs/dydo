---
area: guides
type: guide
---

# Writing Good Briefs

How to write dispatch briefs that give agents enough context to work independently.

---

## What makes a good brief

A good brief is:

- **Self-contained** — The receiving agent can work without asking follow-up questions
- **Actionable** — Clear about what needs to be done, not just what happened
- **Scoped** — Focused on one task, not a laundry list

The agent reading your brief starts fresh. It has no memory of your conversation, your reasoning, or the files you looked at. Everything it needs must be in the brief.

---

## Brief anatomy

A well-structured brief has four parts:

### 1. Context — What happened

One or two sentences of background. Why is this work needed?

### 2. Task — What needs doing

Specific, concrete actions. Not "fix the auth" but "add input validation to the login endpoint in `Services/AuthService.cs`."

### 3. File references — Where to look

List the files the agent should read. Be specific — agents waste time searching when you could just tell them.

### 4. Success criteria — How to know it's done

What does "done" look like? Tests pass? A specific behavior works? A document has certain sections?

---

## Using --brief vs --brief-file

**Inline briefs** (`--brief`) work for short, focused tasks:

```bash
dydo dispatch --wait --auto-close --role reviewer --task auth-login \
  --brief "Review the OAuth implementation in Services/AuthService.cs. Check token expiry handling and error responses."
```

**File-based briefs** (`--brief-file`) are better for complex tasks with multiple steps, file lists, or detailed context:

```bash
dydo dispatch --no-wait --role code-writer --task data-pipeline \
  --brief-file ./dydo/agents/Adele/workspace/pipeline-brief.md
```

Write the brief file to your agent workspace before dispatching.

---

## Common mistakes

### Too vague

```
--brief "Fix the bug"
```

The agent doesn't know which bug, where it is, or what "fixed" looks like. Be specific.

### Too much context

```
--brief "So we were discussing this earlier and I think the problem might be related to
the refactor we did last week where we changed the auth flow and also there was that
issue with the database connection pooling..."
```

Briefs aren't conversation dumps. Strip it down to what the agent needs to act.

### Missing file references

```
--brief "Add validation to the form handler"
```

Which form handler? In which file? The agent will spend time searching when you could have just said `Controllers/FormController.cs`.

### Missing success criteria

```
--brief "Improve the error handling"
```

How much? Which errors? What does "improved" mean? Include concrete checks: "All database calls should catch `DbException` and return a 503."

---

## Examples by role

### Dispatching to code-writer

```bash
dydo dispatch --no-wait --auto-close --role code-writer --task rate-limiting \
  --brief "Implement rate limiting on the /api/upload endpoint.

## Source files
- Services/UploadService.cs — current upload logic
- Models/RateLimitConfig.cs — configuration model (already exists)

## Requirements
- 10 requests per minute per user
- Return 429 with Retry-After header when exceeded
- Add unit tests in DynaDocs.Tests/UploadServiceTests.cs

## Success criteria
- All existing tests pass
- New rate limit tests pass
- Manual test: 11th request within 1 minute returns 429"
```

### Dispatching to reviewer

```bash
dydo dispatch --wait --auto-close --role reviewer --task rate-limiting \
  --brief "Review the rate limiting implementation.

## Files changed
- Services/UploadService.cs — added rate limit check
- Models/RateLimitConfig.cs — added default values
- DynaDocs.Tests/UploadServiceTests.cs — new tests

## What to check
- Thread safety of the rate counter
- Edge cases around the time window boundary
- Test coverage completeness"
```

### Dispatching to test-writer

```bash
dydo dispatch --no-wait --auto-close --role test-writer --task auth-login \
  --brief "Write integration tests for the login flow.

## Source files
- Services/AuthService.cs — the login method
- Models/LoginRequest.cs — request model

## Test scenarios needed
- Valid credentials → returns token
- Invalid password → returns 401
- Locked account → returns 403
- Missing fields → returns 400
- SQL injection in username → handled safely"
```

### Dispatching to docs-writer

```bash
dydo dispatch --no-wait --auto-close --role docs-writer --task api-docs \
  --brief "Document the new rate limiting behavior.

## Source material
- Services/UploadService.cs — implementation
- Models/RateLimitConfig.cs — configuration options

## Docs to update
- dydo/reference/api-endpoints.md — add rate limit section
- dydo/understand/architecture.md — mention rate limiting in the upload flow"
```

---

## Related

- [Dispatch and Messaging](../understand/dispatch-and-messaging.md) — How dispatch works
- [Multi-Agent Workflows](../understand/multi-agent-workflows.md) — Coordinating agents
- [CLI Commands Reference](../reference/dydo-commands.md) — dydo dispatch options
