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

  Scenario: A framework update removes only the retired model block
    Given a project predates the local source layer and records legacy model bindings
    And its integrations, skills, nudges, scan exclusions, framework hashes, template additions, documentation, custom sources, and project-owned files are snapshotted
    When I update the framework templates successfully
    Then the saved dydo.json contains no "models" property
    And its integrations, explicit skill choices, nudges, scan exclusions, unrelated framework hashes, and template additions retain their values and nesting
    And its documentation, custom sources, and project-owned files retain identical paths and bytes
    And only the expected shipped sources, shipped hashes, scan exclusion, and generated switch entries are scaffolded

  Scenario Outline: A failed existing-config rewrite preserves its input before final commit
    Given an existing dydo.json contains legacy models and byte-distinct configuration sentinels
    And "<operation>" has an in-memory configuration change ready to save
    And its managed file work, validation, warnings, and reporting produce "<late outcome>"
    When I run the existing-config operation
    Then the command returns a nonzero exit code
    And the original dydo.json retains identical bytes
    And the legacy "models" property remains in those original bytes
    And no configuration save or temporary sibling creation was attempted

    Examples:
      | operation              | late outcome                        |
      | sync                   | an injected post-work IOException   |
      | template update        | an update warning                    |
      | template update        | an injected post-work IOException   |
      | fix                    | a validation error                   |
      | fix                    | an injected post-work IOException   |
      | init codex --join      | an injected post-work IOException   |

  Scenario Outline: A failed final atomic configuration commit preserves the original file
    Given an existing dydo.json contains legacy models and byte-distinct configuration sentinels
    And configuration saving is injected to fail "<failure>"
    When an existing-config command completes all other work and reaches its final configuration commit
    Then the command fails with the injected save diagnostic
    And the original dydo.json retains identical bytes
    And no temporary sibling created by the failed save remains

    Examples:
      | failure                                                        |
      | after a strict prefix is written to the temporary sibling      |
      | during replacement after the flushed temporary file is closed |

  Scenario: A temporary-name collision cannot overwrite either file
    Given an existing dydo.json and a same-directory temporary-name candidate contain different sentinel bytes
    And configuration saving is injected to choose that existing sibling
    When a successful preflight reaches the configuration save
    Then the command fails before writing or replacing either file
    And dydo.json and the pre-existing sibling retain identical bytes
    And cleanup does not delete the sibling it did not create

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
