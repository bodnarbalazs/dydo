# Engineering

12 skills for the craft: diagnosing, shaping and designing code.

- **codebase-design** (model-invoked): Shared vocabulary for designing deep modules. Use when the user wants to design or improve a module's interface, find deepening opportunities, decide where a seam goes, make code more testable or AI-navigable, or when another skill needs the deep-module vocabulary.
- **create-formal-verification-skill** (user-invoked): Generate a project-local skill that formally verifies the code where a bug costs the most and tests reach the least, in any language, with whichever verification tool fits.
- **create-verification-skill** (user-invoked): Generate a project-local verification skill that drives the app the way a user does, through a deterministic control CLI and a feature map.
- **diagnosing-bugs** (model-invoked): Diagnosis loop for hard bugs and performance regressions. Use when the user says "diagnose"/"debug this", or a ticket reports something broken/throwing/failing/slow.
- **domain-modeling** (model-invoked): Build and sharpen a project's domain model. Use when discussing codebase terminology, writing or editing the glossary, or recording or editing a Decision Record.
- **improve-codebase-architecture** (user-invoked): Scan a codebase for deepening opportunities, present them as a visual HTML report, then grill through whichever one you pick.
- **maintain-formal-verification-skill** (model-invoked): Bring a project's formal verification back in line with the code after the code changes. Use when merges, refactors or edits touched verified code without rechecking the model, harness or properties.
- **maintain-verification-skill** (model-invoked): What counts as proof, and keeping a project's verification skill and feature map true to the running app. Use when work must be proved, a drive contradicts the feature map, a touched area has no map file, or a landing wants the whole map audited.
- **pr** (model-invoked): Use when writing a PR body.
- **prototype** (model-invoked): Build a throwaway prototype to answer a design question. Use when the user wants to sanity-check whether a state model or logic feels right, or explore what a UI should look like.
- **verify-dydo** (model-invoked): Verify DynaDocs CLI and guard journeys in owned disposable projects, with retained evidence. Use for runtime proof, release inquisition or a mapped regression.
- **wizard** (model-invoked): User-only setup, credentials or cutover steps need a repeatable interactive bash procedure; the Enablement captain uses this method.
