@DYD-111
Feature: Local skill templates compile through an enabled switchboard
  A project keeps an explicit local source copy of every shipped skill template and resource.
  Valid custom skills join the same bounded source layout, while extensions and Must-Reads remain
  the preferred ways to customize shipped methods and share project context.
  The switchboard selects skills without becoming a second catalog of their methodology or permissions.

  Scenario Outline: Initialize the local source and switchboard for every integration selection
    Given an empty project directory
    When I initialize dydo with "<integration>"
    Then the command succeeds
    And "dydo/_system/templates" contains every shipped skill and resource template exactly once
    And every shipped source copy has shipped provenance in "frameworkHashes"
    And "dydo.json" contains one ordinally sorted "skills" entry for every discovered skill
    And every new entry has "enabled" true, "origin" "shipped", and its ordinally sorted resource slugs
    And "_system/templates/" is a required scan exclusion
    And no native skill or agent has been compiled yet
    When I synchronize the native artifacts
    Then only enabled skills are compiled for "<providers>"
    And each switch retains its enabled value, origin, and resource inventory

    Examples:
      | integration | providers        |
      | none        | claude and codex |
      | claude      | claude           |
      | codex       | codex            |
      | all         | claude and codex |

  Scenario: Discover and compile a distinctly named custom skill in one synchronization
    Given an initialized project with both providers selected
    And "skill-release-notes.template.md" is a valid custom skill source
    And "release-notes-resource-style.template.md" is its referenced custom resource
    And "writing-for-humans" is explicitly disabled
    When I synchronize the native artifacts
    Then "release-notes" is added to "skills" with "enabled" true and "origin" "custom"
    And its resource inventory is exactly "style"
    And its body, frontmatter metadata, Must-Reads, resource, invocation policy, and agent shape are compiled consistently to both providers
    And "writing-for-humans" remains disabled and absent from both provider surfaces
    When I synchronize the native artifacts again
    Then configuration and native artifacts retain identical paths and bytes

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
    Given an enabled skill previously emitted an agent, a skill, invocation metadata, and two resources to both providers
    And "<current-integration>" is now the only selected integration
    And unrelated files and directories exist beside and inside its provider directories
    When I set that skill's "enabled" switch to false and synchronize
    Then the command succeeds
    And that skill's exact managed agent, SKILL.md, invocation metadata, and recorded resource files are absent from Claude and Codex surfaces
    And every unrelated or custom sibling retains its exact path and bytes
    And only directories made empty by the managed removals are absent
    And the switch remains present with enabled false

    Examples:
      | current-integration |
      | claude              |
      | codex               |

  Scenario: Remove a resource or agent shape from an enabled skill
    Given an enabled custom skill previously emitted an agent and resources "one" and "two" to both providers
    And its valid source now emits a skill only and references only resource "two"
    And one provider is currently deselected
    When I synchronize the native artifacts
    Then its managed agent definitions and resource "one" are removed from both provider surfaces
    And its enabled skill and resource "two" are current on the selected provider
    And tracked output on the deselected provider is cleaned only where the source removed an artifact
    And unrelated sibling files retain their exact paths and bytes
    And its resource inventory becomes exactly "two"

  Scenario Outline: Remember a switch when its custom source is temporarily missing
    Given a custom skill with enabled <enabled> previously emitted to both providers
    And its source and resource templates are absent
    When I synchronize the native artifacts
    Then its exact managed output is removed from both provider surfaces
    And its switch remains a custom tombstone with enabled <enabled> and its prior resource inventory
    And unrelated sibling files retain their exact paths and bytes
    And the command <result> because the requested source is <availability>
    When the same valid custom source returns and I synchronize again
    Then the remembered enabled value is retained
    And the skill is <emission> on the selected providers

    Examples:
      | enabled | result    | availability | emission     |
      | true    | fails     | unavailable  | compiled     |
      | false   | succeeds  | disabled     | not compiled |

  Scenario: Retire formerly shipped source and native output by persisted provenance
    Given a prior installation recorded a shipped skill, its shipped resources, and its switch
    And the current executable no longer ships them
    And their local source and generated output contain hard edits
    And custom siblings exist beside them
    When I update the framework templates and synchronize the native artifacts
    Then the formerly shipped local sources and their provenance hashes are absent
    And their exact managed output is absent from both provider surfaces
    And their switch remains a shipped tombstone with its explicit enabled value
    And custom siblings retain their exact paths and bytes
    And no unrecorded template or native path is removed

  Scenario Outline: Reject invalid source layouts before changing anything
    Given an initialized project contains "<defect>"
    And snapshots cover the template source, dydo.json, and both provider surfaces
    When I "<command>"
    Then the command fails with every invalid path and reason
    And no source, configuration, or native output path or byte changes

    Examples:
      | defect                                                                    | command                                |
      | a nested skill or resource template                                       | synchronize the native artifacts       |
      | a name outside 1-64 lowercase kebab-case characters                       | synchronize the native artifacts       |
      | a case-insensitive duplicate skill or resource name                       | synchronize the native artifacts       |
      | an untracked custom source whose name is newly shipped or retired          | update the framework templates          |
      | a resource with no matching skill                                          | synchronize the native artifacts       |
      | an extra resource attached to a shipped skill                              | synchronize the native artifacts       |
      | missing or blank name, description, or body                                | synchronize the native artifacts       |
      | a name that disagrees with its filename                                    | synchronize the native artifacts       |
      | an unknown frontmatter key or a value outside its documented domain        | synchronize the native artifacts       |
      | agent-only metadata on a skill-only template                               | synchronize the native artifacts       |
      | explicit invocation on an agent template                                   | synchronize the native artifacts       |
      | a resource link without its source or an unreferenced resource source      | synchronize the native artifacts       |
      | a Must-Read that escapes the project or does not exist after includes      | synchronize the native artifacts       |

  Scenario Outline: Reject a malformed switchboard before changing anything
    Given an initialized project contains "<defect>" in "dydo.json"
    And snapshots cover the template source, dydo.json, and both provider surfaces
    When I "<command>"
    Then the command fails and identifies the malformed switch entry
    And it does not replace a missing or invalid enabled value with true
    And no source, configuration, or native output path or byte changes

    Examples:
      | defect                                                      | command                          |
      | skills is not an object                                     | update the framework templates   |
      | an entry is not an object                                   | synchronize the native artifacts |
      | enabled is absent or is not a boolean                       | synchronize the native artifacts |
      | origin is neither shipped nor custom                        | synchronize the native artifacts |
      | resources is not a unique lowercase-kebab-case string array | synchronize the native artifacts |
      | an entry has an unknown property                             | synchronize the native artifacts |
      | an entry has no source and no prior provenance               | synchronize the native artifacts |

  Scenario: Compile reference and delegation declarations without silent weakening
    Given a valid local skill template uses every supported frontmatter field
    And each resource link and Must-Read resolves inside the project after includes
    When I synchronize the native artifacts
    Then name, description, emit, read-only, delegates, web, invocation, and argument-hint have their documented native effect
    And a delegating agent receives Claude's Agent tool and Codex V1 agents enabled with max depth three
    And a non-delegating agent omits Claude's Agent tool and emits Codex V1 agents disabled without max depth
    And each Must-Read appears in the agent context and each resource exists beside both compiled skills
    And the compiler emits no unsupported permission or dependency claim

  Scenario: Refresh stale framework hashes honestly and reach a fixed point
    Given the clean base is "a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f"
    And the six framework documents already equal their LF-normalized current content
    And only their stored provenance hashes are stale
    When I update the framework templates
    Then the command reports zero content updates and six metadata-only hash refreshes
    And "dydo.json" SHA-256 changes from "D43EA96236F78662F90E22F0F79C4B54346EE1C5F6392834E75DEA03119F53FE" to "9F5ECD3F2DB8BF23211D49DA7ADC2756349E8A97EF67821AB1950DD5016ACAA2"
    And each replacement hash equals the LF-normalized on-disk document content
    And current documentation and generated content retain their exact paths and bytes
    When I synchronize the native artifacts
    Then synchronization does not mutate "dydo.json"
    When I update the framework templates and synchronize the native artifacts again
    Then all 134 observed configuration and generated files retain identical paths and bytes
