using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// Contract describing an expected command outcome within a test scenario case package.
    /// Supports asserting intentional validation failures (e.g., out-of-bounds, non-existent entity).
    /// </summary>
    public interface IExpectedCommand
    {
        /// <summary>Expected command tracking identifier.</summary>
        string ExpectedCommandId { get; }

        /// <summary>Expected command whitelist type.</summary>
        SceneCommandType ExpectedType { get; }

        /// <summary>Target entity identifier affected by the command.</summary>
        string ExpectedTargetId { get; }

        /// <summary>True if the command should pass all 4 gates; false if intentionally rejected.</summary>
        bool ExpectValid { get; }

        /// <summary>If ExpectValid is false, the expected gate step that rejects the command (1-4).</summary>
        int ExpectedFailedGate { get; }

        /// <summary>Expected error code or rejection reason token.</summary>
        string ExpectedErrorCode { get; }

        /// <summary>Human-readable explanation of why this command succeeds or is rejected.</summary>
        string Description { get; }
    }

    /// <summary>
    /// Complete test scenario specification bundling script prompt, mock LLM response,
    /// and expected command verification assertions.
    /// </summary>
    public interface ICasePackage
    {
        /// <summary>Unique case package identifier.</summary>
        string PackageId { get; }

        /// <summary>Scenario title or intent.</summary>
        string Title { get; }

        /// <summary>Script text or user prompt passed to the parser.</summary>
        string ScenarioPrompt { get; }

        /// <summary>Deterministic mock LLM response JSON payload.</summary>
        string MockResponseJson { get; }

        /// <summary>Initial scene context snapshot required before running the case.</summary>
        SceneContextDto InitialContext { get; }

        /// <summary>List of expected commands and their validation assertions.</summary>
        IReadOnlyList<IExpectedCommand> ExpectedCommands { get; }

        /// <summary>Total count of expected commands in this test scenario.</summary>
        int ExpectedCommandCount { get; }

        /// <summary>Count of commands explicitly expected to fail one of the 4 gates.</summary>
        int ExpectedRejectionCount { get; }

        /// <summary>Retrieves an expected command assertion by its command identifier.</summary>
        IExpectedCommand GetExpectedCommand(string commandId);

        /// <summary>Exports this case package to a serializable DTO.</summary>
        CasePackageDto ExportPackageDto();
    }

    /// <summary>
    /// Test harness runner that executes a case package against a scene context,
    /// feeds proposals through the 4 gates, and produces a conformance report.
    /// </summary>
    public interface ICasePackageRunner
    {
        /// <summary>
        /// Executes the given case package against the provided scene context.
        /// Produces counters for Proposals (N), Rejected (M), Executed (K), and Readback Parity (K).
        /// </summary>
        CasePackageReportDto RunPackage(ICasePackage package, ISceneContext initialContext);

        /// <summary>
        /// Evaluates whether the generated execution report matches all expected assertions.
        /// </summary>
        bool ValidateCaseConformance(CasePackageReportDto report, ICasePackage package);

        /// <summary>
        /// Evaluates gate-by-gate rejection accuracy against the expected rejection criteria.
        /// </summary>
        bool VerifyRejectionAccuracy(CasePackageReportDto report, ICasePackage package);

        /// <summary>Resets the internal test harness state and clear execution logs.</summary>
        void ResetHarness();
    }

    /// <summary>
    /// Registry contract for managing and loading case packages across the test harness.
    /// </summary>
    public interface ICasePackageRegistry
    {
        /// <summary>Total number of registered test cases.</summary>
        int PackageCount { get; }

        /// <summary>Retrieves a registered package by its package identifier.</summary>
        ICasePackage GetPackageById(string packageId);

        /// <summary>Enumerates all registered case packages.</summary>
        IReadOnlyList<ICasePackage> GetAllPackages();

        /// <summary>Registers a case package for automated execution.</summary>
        void RegisterPackage(ICasePackage package);
    }
}
