namespace ShotField.Core
{
    /// <summary>
    /// Supported whitelist commands that AI or remote collaborators can issue.
    /// Strict whitelist prevents arbitrary scene mutation.
    /// </summary>
    public enum SceneCommandType
    {
        /// <summary>Spawn an asset into the stage (validated against PropLibrary).</summary>
        Spawn = 0,

        /// <summary>Move an actor or prop to a target position (validated against NavMesh).</summary>
        Move = 1,

        /// <summary>Assign a color scheme to an actor or prop (validated against PrevisPalette).</summary>
        AssignColor = 2,

        /// <summary>Add or update a director constraint (Axis, Framing, Visibility).</summary>
        SetConstraint = 3,

        /// <summary>Propose a camera composition or shot setup.</summary>
        ProposeShot = 4
    }

    /// <summary>
    /// Enumeration of the four verification gates required for AI commands.
    /// </summary>
    public enum CommandValidationGate
    {
        /// <summary>Gate 1: JSON schema and parameter structure verification.</summary>
        Gate1_Schema = 1,

        /// <summary>Gate 2: Spatial boundary and entity existence verification.</summary>
        Gate2_Boundary = 2,

        /// <summary>Gate 3: Ghost preview generation and director approval.</summary>
        Gate3_Preview = 3,

        /// <summary>Gate 4: Post-execution readback and passport reconciliation.</summary>
        Gate4_ExecutionReadback = 4
    }

    /// <summary>
    /// Base interface for any executable scene command.
    /// </summary>
    public interface ISceneCommand
    {
        /// <summary>Unique tracking identifier for this command instance.</summary>
        string CommandId { get; }

        /// <summary>Type of command from the approved whitelist.</summary>
        SceneCommandType Type { get; }

        /// <summary>Target entity identifier affected by this command.</summary>
        string TargetId { get; }

        /// <summary>Creation timestamp in UTC milliseconds.</summary>
        long TimestampUtc { get; }

        /// <summary>Serialized JSON payload carrying command-specific parameters.</summary>
        string PayloadJson { get; }
    }

    /// <summary>
    /// Contract for the 4-gate validation pipeline.
    /// Commands must pass all applicable gates prior to permanent scene mutation.
    /// </summary>
    public interface ICommandValidator
    {
        /// <summary>
        /// Validates Gate 1: Syntax, non-empty fields, and valid enum values.
        /// </summary>
        CommandValidationResultDto ValidateSchema(ISceneCommand command);

        /// <summary>
        /// Validates Gate 2: Target entity existence, NavMesh bounds, and obstacle collisions.
        /// </summary>
        CommandValidationResultDto ValidateBoundary(ISceneCommand command, ISceneContext context);

        /// <summary>
        /// Validates Gate 3: Visual ghost preview feasibility and user confirmation state.
        /// </summary>
        CommandValidationResultDto ValidatePreview(ISceneCommand command, ISceneContext context);

        /// <summary>
        /// Validates Gate 4: Re-reads actual scene state after execution to ensure passport parity.
        /// </summary>
        CommandValidationResultDto ValidateExecutionReadback(
            ISceneCommand command,
            ISceneContext preContext,
            ISceneContext postContext);
    }

    /// <summary>
    /// Contract for consumers that apply validated commands to the virtual studio scene.
    /// </summary>
    public interface ICommandConsumer
    {
        /// <summary>
        /// Executes a validated command against the live scene.
        /// </summary>
        CommandExecutionResultDto ExecuteCommand(ISceneCommand command, ISceneContext context);

        /// <summary>
        /// Determines whether this consumer can handle the specified command type.
        /// </summary>
        bool CanConsume(SceneCommandType commandType);
    }
}
