#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01ArtSceneBuilder
    {
        internal static readonly Vector3 TablePosition = new Vector3(0f, 0.4f, 1.2f);
        internal static readonly Vector3 EnvelopePosition = new Vector3(0f, 0.81f, 1.2f);
        internal static readonly Vector3 ActorPosition = new Vector3(0.8f, 0.86f, 1.25f);

        internal static void BuildProps(Transform room)
        {
            Place(room, "Cabinet_Prop", "ShoeCabinet", new Vector3(0.2f, 0.45f, 0.5f), Quaternion.identity);
            Place(room, "Sofa_Prop", "Sofa", new Vector3(0f, 0.425f, 3.2f), Quaternion.identity);
            Place(room, "Desk_Prop", "CoffeeTable", TablePosition, Quaternion.identity);
            Place(room, "Chair_Prop", "Chair", new Vector3(0.8f, 0.45f, 1.25f), Quaternion.identity);
            Place(room, "Plant_Prop", "Plant", new Vector3(-2.2f, 0.6f, 3.2f), Quaternion.identity);
            Place(room, "ActorA_Character", "ActorA", ActorPosition, Quaternion.identity);
            Place(room, "Envelope_Prop", "Envelope", EnvelopePosition, Quaternion.Euler(-90f, 0f, 0f));
        }

        private static GameObject Place(Transform parent, string assetName, string sceneName,
            Vector3 position, Quaternion rotation)
        {
            string path = $"Assets/Prefabs/SC01/{assetName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("SC01 art prefab missing: " + path);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (!instance) throw new InvalidOperationException("SC01 prefab instantiation failed: " + path);
            instance.name = sceneName;
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
            return instance;
        }
    }
}
#endif
