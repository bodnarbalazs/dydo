@DYD-134
Feature: Delegators choose a model for each task
  Admirals and Issue Captains choose the smallest adequate model and supported effort at dispatch time.
  Generated roles preserve native capabilities without fixing a permanent role-to-model policy.

  Scenario Outline: Fresh initialization leaves every generated agent selectable at dispatch time
    Given an empty project directory
    When I initialize dydo with "<integration>"
    And I synchronize the native artifacts
    Then dydo.json has no "models" property
    And every enabled Claude agent contains exactly "model: inherit" and no "effort" frontmatter
    And every enabled Codex agent contains neither "model" nor "model_reasoning_effort"
    And every generated agent retains its name, description, skill loading, permissions, web policy, delegation setting, sandbox, and nesting shape
    And skill-only roles remain skills without generated agent definitions

    Examples:
      | integration |
      | all         |
      | claude      |
      | codex       |
      | none        |

  Scenario: Retire legacy model configuration on the first successful configuration write
    Given an initialized project whose dydo.json contains legacy "models.agents" and "models.tiers"
    And dydo.json contains skills, integrations, nudges, scan exclusions, framework hashes, and unrelated nested configuration
    When I synchronize the native artifacts successfully
    Then the legacy "models" property is absent from the saved dydo.json
    And every surviving configuration property retains its value and nesting
    And the legacy model values have no effect on either provider's generated agents

  Scenario: Preserve disabled-provider bytes until that provider is enabled
    Given both providers have previously generated agents and project-owned custom sibling files
    And exactly one provider is disabled in dydo.json
    When I synchronize the native artifacts
    Then the disabled provider's complete tree retains identical paths and bytes
    And the enabled provider's managed agents have the selectable model shape
    And every project-owned custom sibling retains identical paths and bytes
    When I enable the other provider and synchronize again
    Then that provider's managed agents have the selectable model shape
    And every project-owned custom sibling still retains identical paths and bytes

  Scenario: Apply dynamic selection to a newly discovered custom agent
    Given an initialized project with both providers selected
    And a valid enabled custom agent template with authored switchboard metadata, permissions, resources, and nesting
    When I synchronize the native artifacts
    Then its Claude agent contains exactly "model: inherit" and no "effort" frontmatter
    And its Codex agent contains neither "model" nor "model_reasoning_effort"
    And its authored switchboard metadata, skills, permissions, resources, and nesting retain their documented native effect
    And unrelated custom sources and generated siblings retain identical paths and bytes

  Scenario Outline: Initialization, update, and synchronization reach one fixed point
    Given a project containing legacy model configuration and previously pinned generated agents
    When I run the successful source-built operation sequence "<sequence>" twice
    Then the second sequence leaves dydo.json, local template sources, managed native artifacts, and custom siblings at identical paths and bytes
    And no generated concrete model, generated effort, "models.agents", or "models.tiers" returns

    Examples:
      | sequence                    |
      | init all --join; sync       |
      | template update; sync       |
      | sync                        |

  Scenario: Delegators choose capability per task without a permanent role table
    Given the canonical admiral and issue-captain skills are compiled to both providers
    When I read each delegation step
    Then it requires model choice from difficulty, uncertainty, consequence of error, required independence, context size, and likely retries
    And it requires an adequate supported model and effort where available, with escalation on evidence
    And it forbids weakening reviews or gates to reduce cost
    And it contains no permanent role-to-model or role-to-effort table

  Scenario: Claude agents preserve truthful dispatch precedence and limits
    Given a generated Claude named agent
    Then its agent file declares "model: inherit" and declares no effort
    And the documented precedence is per-invocation model, agent-file model, environment override, then parent model for the supported installed version
    And the documentation says organization policy may substitute unavailable models
    And it does not invent a per-Agent-call effort argument
    And it distinguishes requested model, configured model, effective model, requested session effort, and effective effort

  Scenario: Codex agents leave both dispatch controls to the caller
    Given a generated Codex named agent
    Then its agent file declares neither "model" nor "model_reasoning_effort"
    And the documented precedence is explicit spawn value, matching agents default, then parent value before the custom agent file for the supported installed version
    And the documentation requires selecting model and supported reasoning effort together for an explicit task choice
    And it distinguishes requested, configured, and effective values

  @native @paid
  Scenario Outline: A fresh native role catalog accepts two task-specific choices per supported host
    Given host "<host>" is installed at the recorded version and exposes the required native dispatch surface
    And a source-built dydo initializes and synchronizes a new isolated read-only canary project
    And the host starts a fresh parent invocation after synchronization so it loads that project's current named-role catalog
    When the parent invokes the generated "reviewer" once for each "<choice-a>" and "<choice-b>" with distinct nonces
    Then each trace contains a native named-role invocation, its task-specific requested values, its matching nonce, and successful completion
    And the canary project has no writes
    And requested, configured, and effective model and effort evidence are recorded in separate fields
    And child self-report is not accepted as identity evidence
    And any effective capability absent from trustworthy host telemetry is marked "unproved"
    And an unavailable or rejected choice fails the gate with the host diagnostic instead of being reinterpreted as proof

    Examples:
      | host   | choice-a                    | choice-b                   |
      | Codex  | gpt-5.6-luna / low          | gpt-5.6-sol / high         |
      | Claude | haiku / session effort low  | sonnet / session effort low|
