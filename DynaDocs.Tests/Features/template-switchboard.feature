@DYD-111
Feature: Local skill templates compile through an enabled switchboard
  A project keeps an explicit local source copy of every shipped skill template and resource.
  Valid custom skills join the same bounded source layout, while extensions and Must-Reads remain
  the preferred ways to customize shipped methods and share project context.
  The switchboard selects skills and remembers only the output shape needed for exact cleanup.

  Scenario Outline: Initialize the local source and switchboard for every integration selection
    Given an empty project directory
    When I initialize dydo with "<integration>"
    Then the command succeeds
    And "dydo/_system/templates" contains every shipped skill and resource template exactly once
    And every shipped source copy has shipped provenance in "frameworkHashes"
    And "dydo.json" contains one ordinally sorted "skills" entry for every discovered skill
    And every new entry has "enabled" true, "origin" "shipped", its "emitAgent" and "codexInvocation" booleans, and its ordinally sorted unique resource slugs
    And "_system/templates/" is a required scan exclusion
    And no native skill or agent has been compiled yet
    When I synchronize the native artifacts
    Then only enabled skills are compiled for "<providers>"
    And each switch retains its enabled value and generated origin, output-shape, and resource provenance

    Examples:
      | integration | providers        |
      | none        | claude and codex |
      | claude      | claude           |
      | codex       | codex            |
      | all         | claude and codex |

  Scenario: Discover and compile a distinctly named custom skill in one synchronization
    Given an initialized project with both providers selected
    And "skill-release-notes.template.md" is a valid custom agent source
    And "release-notes-resource-style.template.md" is its referenced custom resource
    And "writing-for-humans" is explicitly disabled
    When I synchronize the native artifacts
    Then "release-notes" is added to "skills" with "enabled" true and generated origin, output-shape, and resource provenance
    And its resource inventory is exactly "style"
    And its body, frontmatter metadata, Must-Reads, resource, and agent shape are compiled consistently to both providers
    And "writing-for-humans" remains disabled and absent from both provider surfaces
    When I synchronize the native artifacts again
    Then configuration and native artifacts retain identical paths and bytes

  Scenario: Accept a minimal custom switch and fill generated provenance
    Given "skill-release-notes.template.md" is a valid custom skill source
    And its switch is exactly `{ "enabled": true }`
    When I synchronize the native artifacts
    Then the command succeeds
    And "enabled" remains true
    And "origin", "emitAgent", "codexInvocation", and "resources" are generated from the validated source
    And no permission or methodology metadata is copied into "dydo.json"

  Scenario: Update shipped source copies without merging local edits
    Given an initialized project with a hard edit in a shipped skill source and a shipped resource source
    And a project extension exists under "dydo/_system/template-additions"
    And a distinctly named valid custom skill and its Must-Read document exist
    And shipped and custom skills have explicit enabled and disabled choices
    When I update the framework templates from a newer dydo installation
    Then every current shipped skill and resource source exactly matches the running executable
    And their provenance hashes match LF-normalized shipped content
    And the hard edits are overwritten without a backup, merge, re-anchoring, or conflict file
    And the project extension, custom skill, custom resource, and Must-Read document retain their exact paths and bytes
    And every existing enabled or disabled choice retains its value
    And each newly shipped skill is added with enabled true
    And the command reports source replacements, discoveries, retirements, and metadata-only hash refreshes separately and truthfully

  Scenario: Repair a malformed hard-edited shipped source during update
    Given a recorded shipped source copy has malformed frontmatter or body
    And the running executable carries a valid replacement for the same shipped name
    And every custom source and collision input is valid
    When I update the framework templates
    Then preflight validates the effective catalog after the packaged replacement
    And the malformed shipped copy is replaced by the valid packaged copy
    And its switch provenance is reconciled from the replacement
    And the malformed pre-update shipped copy does not block its own repair

  Scenario: Upgrade a project that predates the local source layer
    Given an initialized project has no "dydo/_system/templates" directory and no "skills" switchboard
    And its existing documentation, integrations, model bindings, nudges, exclusions, and template additions are recorded
    When I update the framework templates
    Then the command succeeds
    And the shipped local source, shipped provenance hashes, scan exclusion, and enabled switchboard are scaffolded
    And the existing project configuration and project-owned files retain their values and bytes
    When I synchronize the native artifacts
    Then enabled skills compile from the local source rather than an embedded fallback

  Scenario: Preview an update without changing the project
    Given an initialized project has a hard-edited shipped source, a new valid custom skill, and stale provenance hashes
    When I preview the framework template update
    Then validation and collision preflight are the same as a real update
    And the preview reports every source, switchboard, provenance, retirement, and scan-exclusion change it would make
    And every project path and byte remains unchanged

  Scenario: Preserve an explicit disable while a custom variant survives updates
    Given "writing-for-humans" is explicitly disabled
    And "skill-writing-for-our-team.template.md" is a valid custom skill with enabled true
    When I update the framework templates and synchronize the native artifacts
    Then "writing-for-humans" remains disabled
    And "writing-for-our-team" remains custom and enabled
    And only "writing-for-our-team" is present on each selected provider surface

  Scenario Outline: Disable a skill and remove only its managed native output
    Given an enabled skill previously emitted <prior-shape> and two resources to both providers
    And its switch records "emitAgent" <emit-agent>, "codexInvocation" <codex-invocation>, and the two unique resource slugs
    And "<current-integration>" is now the only selected integration
    And unrelated files and directories exist beside and inside its provider directories
    When I set that skill's "enabled" switch to false and synchronize
    Then the command succeeds
    And that skill's fixed SKILL.md paths, recorded agent definitions, Codex invocation metadata, and recorded resource files are absent from Claude and Codex surfaces
    And every unrelated or custom sibling retains its exact path and bytes
    And only directories made empty by the managed removals are absent
    And the switch remains present with enabled false and its generated cleanup provenance

    Examples:
      | current-integration | prior-shape             | emit-agent | codex-invocation |
      | claude              | an agent definition     | true       | false            |
      | claude              | explicit skill metadata | false      | true             |
      | codex               | an agent definition     | true       | false            |
      | codex               | explicit skill metadata | false      | true             |

  Scenario: Remove a resource and agent shape from an enabled skill
    Given an enabled custom agent previously emitted resources "one" and "two" to both providers
    And its switch records "emitAgent" true, "codexInvocation" false, and resources "one" and "two"
    And its valid source now emits a skill only with automatic invocation and references only resource "two"
    And one provider is currently deselected
    When I synchronize the native artifacts
    Then its recorded Claude and Codex agent definitions and resource "one" are removed from both provider surfaces
    And its enabled skill and resource "two" are current on the selected provider
    And tracked output on the deselected provider is cleaned only where the source removed an artifact
    And unrelated sibling files retain their exact paths and bytes
    And its provenance records "emitAgent" false, "codexInvocation" false, and resource "two"

  Scenario: Remove Codex invocation metadata from an enabled skill-only template
    Given an enabled custom skill-only template previously used explicit invocation on both providers
    And its switch records "emitAgent" false and "codexInvocation" true
    And its valid source now uses automatic invocation
    And one provider is currently deselected
    When I synchronize the native artifacts
    Then its recorded Codex invocation metadata is removed from both provider surfaces
    And its fixed SKILL.md is current on the selected provider
    And unrelated sibling files retain their exact paths and bytes
    And its provenance records "emitAgent" false and "codexInvocation" false

  Scenario Outline: Remember a switch and output shape when its custom source is temporarily missing
    Given a custom skill with enabled <enabled> previously emitted <prior-shape> and resources to both providers
    And its switch records "emitAgent" <emit-agent>, "codexInvocation" <codex-invocation>, and unique resource slugs
    And its source and resource templates are absent
    When I synchronize the native artifacts
    Then its fixed SKILL.md paths and its recorded agent, invocation, and resource output are removed from both provider surfaces
    And its switch remains a custom tombstone with enabled <enabled> and its prior generated cleanup provenance
    And unrelated sibling files retain their exact paths and bytes
    And the command <result> because the requested source is <availability>
    When the same valid custom source returns and I synchronize again
    Then the remembered enabled value is retained
    And the skill is <emission> on the selected providers

    Examples:
      | enabled | prior-shape              | emit-agent | codex-invocation | result   | availability | emission     |
      | true    | agent definition         | true       | false            | fails    | unavailable  | compiled     |
      | false   | explicit skill metadata  | false      | true             | succeeds | disabled     | not compiled |

  Scenario: Intentionally delete a custom skill after cleaning its output
    Given a valid custom skill source and switch exist
    And the skill previously emitted managed output to both providers
    When I set its "enabled" switch to false and synchronize
    Then its recorded managed output is absent from both provider surfaces
    And its disabled switch and valid source remain restorable
    When I remove that custom source, its resource sources, and its switch entry
    And I synchronize the native artifacts
    Then the command succeeds
    And the custom switch is not recreated
    And no same-name native path is deleted during the terminal synchronization
    And unrelated files retain their exact paths and bytes

  Scenario Outline: Retire formerly shipped source and native output by persisted provenance
    Given a prior installation recorded a shipped <prior-shape>, its shipped resources, and its switch
    And its switch records "emitAgent" <emit-agent> and "codexInvocation" <codex-invocation>
    And the current executable no longer ships them
    And their local source and generated output contain hard edits
    And custom siblings exist beside them
    When I update the framework templates and synchronize the native artifacts
    Then the formerly shipped local sources and their provenance hashes are absent
    And their fixed SKILL.md paths and recorded agent, invocation, and resource output are absent from both provider surfaces
    And their switch remains a shipped tombstone with its explicit enabled value and prior cleanup provenance
    And custom siblings retain their exact paths and bytes
    And no unrecorded template or native path is removed

    Examples:
      | prior-shape             | emit-agent | codex-invocation |
      | agent definition        | true       | false            |
      | explicit skill metadata | false      | true             |

  Scenario Outline: Reject invalid source layouts before changing anything
    Given an initialized project contains "<defect>"
    And snapshots cover the intended post-operation template source, dydo.json, and both provider surfaces
    When I "<command>"
    Then the command fails with every invalid path and reason
    And no source, configuration, or native output path or byte changes

    Examples:
      | defect                                                                    | command                          |
      | a nested skill or resource template                                       | synchronize the native artifacts |
      | a name outside 1-64 lowercase kebab-case characters                       | synchronize the native artifacts |
      | a name containing the protected -resource- delimiter                      | synchronize the native artifacts |
      | a case-insensitive duplicate skill or resource name                       | synchronize the native artifacts |
      | an untracked custom source whose name is newly shipped or retired          | update the framework templates    |
      | a resource with no matching skill                                          | synchronize the native artifacts |
      | an extra resource attached to a shipped skill                              | synchronize the native artifacts |
      | missing or blank name, description, or body                                | synchronize the native artifacts |
      | a name that disagrees with its filename                                    | synchronize the native artifacts |
      | an unknown frontmatter key or a value outside its documented domain        | synchronize the native artifacts |
      | agent-only metadata on a skill-only template                               | synchronize the native artifacts |
      | explicit invocation on an agent template                                   | synchronize the native artifacts |
      | a resource link without its source or an unreferenced resource source      | synchronize the native artifacts |
      | a Must-Read that escapes the project or does not exist after includes      | synchronize the native artifacts |

  Scenario: Validate sources against the intended post-operation state
    Given an update or synchronization creates a referenced local source, resource, or Must-Read target in that same operation
    And the effective post-operation catalog is otherwise valid
    And stale native output disagrees with that catalog
    When preflight validates the operation
    Then targets created by that operation satisfy their references
    And stale native output has no bearing on catalog validity
    And cleanup and writes begin only after the complete intended catalog passes

  Scenario Outline: Reject a malformed switchboard before update or synchronization changes anything
    Given an initialized project contains "<defect>" in "dydo.json"
    And snapshots cover the template source, dydo.json, and both provider surfaces
    When I "<command>"
    Then the command fails and identifies the malformed switch entry
    And it does not replace a missing or invalid enabled value with true
    And no source, configuration, or native output path or byte changes

    Examples:
      | defect                                                                | command                          |
      | skills is not an object                                               | update the framework templates   |
      | an entry is not an object                                             | synchronize the native artifacts |
      | enabled is absent or is not a boolean                                 | synchronize the native artifacts |
      | a switch key is outside 1-64 lowercase kebab-case characters          | update the framework templates   |
      | a switch key contains the protected -resource- delimiter              | synchronize the native artifacts |
      | switch keys or source names collide by ordinal-ignore-case comparison | synchronize the native artifacts |
      | supplied origin is neither shipped nor custom                         | synchronize the native artifacts |
      | supplied emitAgent or codexInvocation is not a boolean                | synchronize the native artifacts |
      | supplied resources is not a unique lowercase-kebab-case string array  | synchronize the native artifacts |
      | an entry has an unknown property                                      | synchronize the native artifacts |
      | an entry has no source and no prior generated provenance              | synchronize the native artifacts |

  Scenario Outline: Check and validate reject a malformed switchboard
    Given "dydo.json" contains "<defect>"
    When I run `dotnet ./bin/Release/net10.0/dydo.dll <command>`
    Then the command exits nonzero
    And its diagnostic identifies "dydo.json", the switch entry, and the malformed field or key
    And malformed JSON is distinguished from a missing configuration file
    And documentation validation does not report success for the malformed configuration

    Examples:
      | defect                                                       | command  |
      | malformed JSON                                               | check    |
      | malformed JSON                                               | validate |
      | enabled is absent                                            | check    |
      | enabled is absent                                            | validate |
      | a switch key contains uppercase characters                   | check    |
      | two switch keys collide by ordinal-ignore-case comparison    | validate |
      | supplied generated output provenance has the wrong JSON type | check    |
      | supplied generated output provenance has the wrong JSON type | validate |

  Scenario Outline: Compile a valid agent with its declared delegation shape
    Given a valid local agent template declares read-only true, web true, delegates <delegates>, automatic invocation, and an argument hint
    And each resource link and Must-Read resolves inside the project after includes
    When I synchronize the native artifacts
    Then its name, description, agent shape, read-only policy, web policy, automatic invocation, argument hint, Must-Reads, and resources have their documented native effect
    And Claude Agent tool is <claude-agent-tool>
    And Codex V1 agents are <codex-agents> and max depth three is <max-depth>
    And the compiler emits no unsupported permission or dependency claim

    Examples:
      | delegates | claude-agent-tool | codex-agents | max-depth |
      | true      | present           | enabled      | present   |
      | false     | absent            | disabled     | absent    |

  Scenario: Compile a valid skill-only template with explicit invocation
    Given a valid local skill-only template declares explicit invocation and an argument hint
    And it declares no agent-only read-only, delegates, or web field
    And each resource link and Must-Read resolves inside the project after includes
    When I synchronize the native artifacts
    Then its name, description, skill shape, explicit invocation, argument hint, Must-Reads, and resources have their documented native effect
    And no Claude or Codex agent definition is emitted
    And Codex invocation metadata is emitted at the fixed managed path
    And the compiler emits no unsupported permission or dependency claim

  Scenario: Preserve the beta hash observation as field-level migration regression evidence
    Given historical beta.1 evidence at base "a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f" observed dydo.json SHA-256 change from "D43EA96236F78662F90E22F0F79C4B54346EE1C5F6392834E75DEA03119F53FE" to "9F5ECD3F2DB8BF23211D49DA7ADC2756349E8A97EF67821AB1950DD5016ACAA2"
    And the six framework documents already equal their LF-normalized current content
    And only those six stored document provenance fields are stale
    When the source-built command updates and migrates the framework templates
    Then the command reports zero document content updates and six metadata-only document hash refreshes
    And each of the six replacement fields equals the LF-normalized on-disk document content hash
    And the new local template sources, switchboard, source hashes, and scan exclusion are also materialized
    And no post-migration whole-file dydo.json hash is inferred from the historical beta hashes

  Scenario: Reach a post-migration fixed point over the complete managed manifest
    Given one source-built template update and synchronization have completed
    And a sorted relative-path and SHA-256 manifest covers dydo.json, every local skill and resource source, and all Claude and Codex managed outputs
    When I run the same source-built template update and synchronization again
    Then the second command pair succeeds without a content, metadata, discovery, retirement, or cleanup change
    And the post-migration manifest retains identical relative paths and bytes
    And the working-tree diff is unchanged by the second command pair
