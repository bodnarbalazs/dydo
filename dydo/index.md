---
area: general
type: hub
---
# DynaDocs - AI Agent Entry Point  
  
Documentation-driven context and agent orchestration for AI coding assistants.  
  
You need an assigned identity to work here.  
  
---  
  
## Your Identity  
  
Check your prompt for an agent name (e.g., `Adele`, `Brian`).  
  
**Found one?** → Open `agents/<your-name>/workflow.md`  
  
**None?** → Run `dydo agent claim auto`, then open `agents/<assigned-name>/workflow.md`  
  
Your prompt may include a --flag. Your workflow file explains everything.  
  
---  
  
## Disoriented?  
  
Lost context? Run `dydo whoami`. It shows your identity, role, and task.  
  
If claimed: check your workspace for notes, return to your mode file.  
If not claimed: follow the flow above.  
  
## Warning  
  
Guard audits all actions. Violations are logged and traceable.  
Agents that bypass guardrails — even to complete their task faster — will be immediately terminated and their work  
discarded.  
  
If a guardrail blocks your work, first re-read the relevant docs and usage — most issues stem from not using the  
system correctly.  
If you're still blocked after that, report the issue to the user. Never work around it.  
  
The system is built to enable many agents to work in harmony toward a common goal.  
Adherence to the rules and the workflow is essential for it to work well.  
Don't be that one agent who ruins the party for everyone.