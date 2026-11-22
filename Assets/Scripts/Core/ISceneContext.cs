using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// Read-only abstraction of a single entity present in the virtual production scene.
    /// Exposes spatial location, semantic archetype, and passport metadata.
    /// </summary>
    public interface ISceneObject
    {
        /// <summary>Unique stable identifier for the scene entity.</summary>
        string ObjectId { get; }

        /// <summary>Human-readable display name or asset label.</summary>
        string DisplayName { get; }

        /// <summary>Semantic archetype classification (ActorPrimary, KeyProp, etc.).</summary>
        string Archetype { get; }

        /// <summary>Asset catalog category (Actor, Furniture, DailyProp, Environment).</summary>
        string Category { get; }

        /// <summary>World-space position in the studio coordinate system.</summary>
        Vector3Dto Position { get; }

        /// <summary>World-space orientation quaternion.</summary>
        QuaternionDto Rotation { get; }

        /// <summary>Axis-aligned bounding box enclosing the object mesh/geometry.</summary>
        BoundsDto Bounds { get; }

        /// <summary>Whether the object is active and visible in the scene hierarchy.</summary>
        bool IsActive { get; }

        /// <summary>Summary of production passport metadata for this entity.</summary>
        PassportSummaryDto PassportSummary { get; }
    }

    /// <summary>
    /// Read-only snapshot interface representing the full virtual production stage context.
    /// Used as upstream context for AI analysis and downstream validation gates.
    /// </summary>
    public interface ISceneContext
    {
        /// <summary>Scene or stage identifier (e.g., SC01_Main).</summary>
        string SceneId { get; }

        /// <summary>UTC timestamp when this context snapshot was captured.</summary>
        long TimestampUtc { get; }

        /// <summary>Walkable NavMesh boundaries and obstacle bounding boxes.</summary>
        NavMeshBoundsDto WalkableBounds { get; }

        /// <summary>Total number of tracked scene entities in the context.</summary>
        int ObjectCount { get; }

        /// <summary>Count of active actor entities currently in the stage.</summary>
        int ActiveActorCount { get; }

        /// <summary>Count of active prop entities currently in the stage.</summary>
        int ActivePropCount { get; }

        /// <summary>Complete collection of tracked scene entities.</summary>
        IReadOnlyList<ISceneObject> Objects { get; }

        /// <summary>
        /// Retrieves a scene entity by its unique identifier.
        /// Returns null if not found.
        /// </summary>
        ISceneObject GetObjectById(string objectId);

        /// <summary>
        /// Attempts to locate a scene entity by its unique identifier.
        /// </summary>
        bool TryGetObject(string objectId, out ISceneObject obj);

        /// <summary>
        /// Retrieves all scene entities matching the specified archetype.
        /// </summary>
        IReadOnlyList<ISceneObject> GetObjectsByArchetype(string archetype);

        /// <summary>
        /// Retrieves all scene entities matching the specified category.
        /// </summary>
        IReadOnlyList<ISceneObject> GetObjectsByCategory(string category);

        /// <summary>
        /// Checks whether a world-space position falls within the walkable NavMesh polygon.
        /// </summary>
        bool IsPositionWalkable(Vector3Dto position);

        /// <summary>
        /// Finds the nearest active scene object within a given search radius.
        /// </summary>
        ISceneObject FindNearestEntity(Vector3Dto position, float maxRadius);

        /// <summary>
        /// Exports this context into a serializable SceneContextDto for network or AI transport.
        /// </summary>
        SceneContextDto CreateContextSnapshot();
    }
}
