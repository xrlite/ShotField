#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B5 虚拟演员：两个 Performer 用的是 Starter Assets 的 Armature.fbx，
    /// 但该 fbx 导入时设了 materialImportMode=0（不导入材质），场景里也没有材质覆写，
    /// 于是回落 URP 默认白 Lit —— 这就是画面里的"灰泥解剖人"。
    /// 这里直接给实例的 renderer 赋 sharedMaterials：资产零改动，回滚只需删覆写。
    /// 绝不改 fbx 的 meta —— 改成 1 会强制重导，有把 Humanoid Avatar 推回 Generic 的风险。
    /// </summary>
    internal static class Sc01ActorPatch
    {
        private static readonly string[] ActorNames = { "SC01_Performer_A", "SC01_Performer_B" };

        private static readonly Vector3 ActorAPos = new Vector3(-0.60f, 0f, 2.35f);
        private static readonly Vector3 ActorALook = new Vector3(-0.35f, 0f, 0.65f);
        private static readonly Vector3 ActorBPos = new Vector3(1.18f, 0f, 3.05f);
        private const float ActorBYaw = 198f;

        [MenuItem("ShotField/SC01/Patch B5 演员材质")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("Actor");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("ACTOR");
        }

        internal static bool Apply()
        {
            var body = Sc01PatchCore.LoadArmatureMat("M_Armature_Body");
            var arms = Sc01PatchCore.LoadArmatureMat("M_Armature_Arms");
            var legs = Sc01PatchCore.LoadArmatureMat("M_Armature_Legs");

            foreach (var name in ActorNames)
            {
                var root = Sc01PatchCore.FindDeep(name);
                if (root == null) { Sc01PatchCore.Check(false, "B5 演员 " + name, "缺失"); continue; }
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Sc01PatchCore.Check(renderers.Length > 0, "B5 演员 " + name + " 有蒙皮渲染器",
                    "数量=" + renderers.Length);
                var painted = 0;
                foreach (var renderer in renderers)
                {
                    if (Paint(renderer, body, arms, legs, name)) painted++;
                }
                Sc01PatchCore.Check(painted == renderers.Length && painted > 0,
                    "B5 演员 " + name + " 材质已挂上", painted + "/" + renderers.Length);
            }

            Stage(ActorNames[0], ActorAPos, ActorALook, null, 0f);
            Stage(ActorNames[1], ActorBPos, Vector3.zero, null, ActorBYaw);
            return Sc01PatchCore.AllPassed;
        }

        private static bool Paint(SkinnedMeshRenderer renderer, Material body, Material arms,
            Material legs, string actor)
        {
            if (renderer == null) return false;
            var mesh = renderer.sharedMesh;
            var meshName = mesh == null ? "(null)" : mesh.name;
            var slots = Mathf.Max(1, mesh == null ? 1 : mesh.subMeshCount);
            var materials = new Material[slots];
            for (var i = 0; i < slots; i++) materials[i] = Pick(meshName, i, slots, body, arms, legs);
            renderer.sharedMaterials = materials;
            Debug.Log("[SC01-PATCH] " + actor + "/" + renderer.name + " mesh=" + meshName +
                      " sub=" + slots + " -> " + materials[0].name);
            return renderer.sharedMaterials[0] == materials[0] && renderer.sharedMaterials[0] != null;
        }

        private static Material Pick(string meshName, int slot, int slots, Material body,
            Material arms, Material legs)
        {
            var key = meshName.Replace("Armature", string.Empty, StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();
            if (key.Contains("leg") || key.Contains("foot")) return legs;
            if (key.Contains("arm") || key.Contains("hand")) return arms;
            if (key.Contains("body") || key.Contains("torso") || key.Contains("head")) return body;
            if (slots >= 3) return slot == 0 ? body : slot == 1 ? arms : legs;
            return body;
        }

        private static void Stage(string name, Vector3 position, Vector3 lookAt, Quaternion? rotation,
            float yaw)
        {
            var root = Sc01PatchCore.FindDeep(name);
            if (root == null) { Sc01PatchCore.Check(false, "B5 站位 " + name, "缺失"); return; }

            var y = root.position.y;
            root.position = new Vector3(position.x, y, position.z);
            if (lookAt != Vector3.zero)
            {
                var dir = lookAt - new Vector3(position.x, lookAt.y, position.z);
                root.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            else if (rotation.HasValue) root.rotation = rotation.Value;
            else root.rotation = Quaternion.Euler(0f, yaw, 0f);

            var planar = new Vector2(root.position.x - position.x, root.position.z - position.z);
            Sc01PatchCore.Check(planar.magnitude < 0.01f, "B5 站位 " + name,
                "pos=" + root.position + " yaw=" + root.eulerAngles.y.ToString("F1"));
        }
    }
}
#endif
