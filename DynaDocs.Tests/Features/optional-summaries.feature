@DYD-98
Feature: Optional document summaries
  A document may begin with useful content after its title.
  Checking and fixing documents must not demand an opening summary.

  Background:
    Given a documentation tree whose frontmatter, navigation and links are valid

  Scenario: Begin with a section instead of an opening paragraph
    Given a document with an H1 title followed directly by an H2 section
    When I check the documentation
    Then the document has no validation errors or warnings

  Scenario: Begin with a list instead of an opening paragraph
    Given a document with an H1 title followed directly by a list
    When I check the documentation
    Then the document has no validation errors or warnings

  Scenario: A title needs no following paragraph
    Given a document containing valid frontmatter and an H1 title only
    When I check the documentation
    Then the document has no validation errors or warnings

  Scenario: Optional opening prose is not a summary requirement
    Given a document with an H1 title followed by "(One-line summary)"
    When I check the documentation
    Then the document has no validation errors or warnings

  Scenario: A missing title remains an error
    Given a document with valid frontmatter and an H2 section but no H1 title
    When I check the documentation
    Then checking fails with "Missing title (# heading)" for that document

  Scenario: A broken link remains an error without an opening summary
    Given a document with an H1 title followed directly by an H2 section
    And that section links to "./missing.md" which does not exist
    When I check the documentation
    Then checking fails with "Broken link: ./missing.md" for that document

  Scenario: Fix does not request or insert a summary
    Given a document with an H1 title followed directly by an H2 section
    When I fix the documentation
    Then no manual repair asks for a summary paragraph
    And that document's bytes are unchanged

  Scenario: Fix preserves optional opening prose
    Given a document with an H1 title followed by "A useful navigation description."
    When I fix the documentation
    Then that document's bytes are unchanged

  Scenario: Fix still requests missing frontmatter
    Given a document with an H1 title and no frontmatter or opening summary
    When I fix the documentation
    Then the manual repairs include "Add frontmatter" for that document
    And no manual repair asks for a summary paragraph
