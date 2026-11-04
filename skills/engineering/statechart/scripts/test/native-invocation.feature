Feature: Installed statechart invocation owns prerequisites and captured continuation
  Scenario: Ready invocation reuses verified compatible dependencies offline
    Given the installed capability has verified compatible dependencies
    When an agent invokes the original bounded model request
    Then the same request runs without installation or repeated consent

  Scenario: Missing prerequisites preserve the original request for normal host authorization
    Given required prerequisites are missing
    When an agent invokes the bounded model request
    Then the capability saves the original inputs and emits a locked continuation plan with exit 3
    And no installer or checker starts

  Scenario: Authorized resume installs once and continues the captured chart
    Given a saved request and the host authorizes its exact resume operation
    When the capability verifies and installs the missing locked prerequisites
    Then it checks the captured model and publishes its actual verdict and output

  Scenario: Denied or unavailable authorization preserves the pending request
    Given the capability has returned a saved continuation plan
    When the host refuses or cannot authorize the resume operation
    Then no provisioning or checking operation runs
    And the original request remains available for an authorized retry

  Scenario: Native or npm owner loss stops owned checking descendants
    Given checking descendants are gated and assigned to the registered native lifetime
    When the invocation owner disappears
    Then its owned checking descendants stop without touching foreign sentinels
    And uncertain cleanup retains scratch with an error

  Scenario: Installed payload or skill mismatch rejects before checking
    Given an installed payload or skill projection has changed identity
    When an agent invokes the capability
    Then it reports the mismatch before starting a checker
