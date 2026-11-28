#if UNITY_EDITOR
using System;
using System.IO;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    /// <summary>Batchmode entry: build the art hall, save scene/prefab, render three evidence views.</summary>
    internal static class HallArtCapture
    {
        struct Pose { public string Name; public Vector3 Pos; public Vector3 Target; public float Fov; }

        public static void Run()
        {
            int failures = 0;
            try
            {
                HallArtBuilder.Build(true, true);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.227f, 0.204f, 0.173f) * 1.25f;
                var probeGo = new GameObject("HallReflectionProbe");
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probeGo.transform.position = new Vector3(0f, 1.5f, 0f);
                probe.size = new Vector3(20f, 8f, 20f);
                probe.resolution = 16;
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.10f, 0.088f, 0.072f);
                probe.RenderProbe();
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = probe.texture;
                // A1 关雾：这是最后一个会把雾重新打开的代码点。雾在暖调礼堂里只负责洗白暗部，
                // 与本轮美术方向相反，故恒关闭。下面 4 行 fog 参数保留（fog=false 时不生效）。
                RenderSettings.fog = false;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.10f, 0.088f, 0.072f);
                RenderSettings.fogStartDistance = 7f;
                RenderSettings.fogEndDistance = 16f;

                var holder = new GameObject("HallArtEvidenceCamera") { hideFlags = HideFlags.HideAndDontSave };
                var cam = holder.AddComponent<Camera>();
                cam.enabled = false;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
                cam.allowHDR = true; cam.allowMSAA = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.055f, 0.047f, 0.039f);
                EnablePost(holder);

                var poses = new[]
                {
                    new Pose { Name = "主视角", Pos = new Vector3(0f, 1.55f, 4.6f), Target = new Vector3(0f, 1.15f, -0.6f), Fov = 55f },
                    new Pose { Name = "创作台特写", Pos = new Vector3(2.3f, 1.45f, 2.5f), Target = new Vector3(0f, 0.95f, 0.7f), Fov = 46f },
                    new Pose { Name = "拱门对称", Pos = new Vector3(0f, 1.5f, 2.2f), Target = new Vector3(0f, 1.7f, -1.6f), Fov = 58f }
                };
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../05_验证记录/2026-11"));
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                foreach (var pose in poses)
                {
                    cam.fieldOfView = pose.Fov; cam.aspect = 16f / 9f;
                    cam.transform.SetPositionAndRotation(pose.Pos, Quaternion.LookRotation((pose.Target - pose.Pos).normalized, Vector3.up));
                    byte[] bytes = FrameExportUtility.RenderCameraToPng(cam, 1920, 1080, new Color(0.055f, 0.047f, 0.039f));
                    string path = Path.Combine(folder, stamp + "_美术厅_" + pose.Name + ".png");
                    File.WriteAllBytes(path, bytes);
                    Debug.Log("[HallArtCapture] " + pose.Name + " bytes=" + bytes.Length + " -> " + path);
                }
                UnityEngine.Object.DestroyImmediate(holder);
            }
            catch (Exception error)
            {
                failures++;
                Debug.LogException(error);
            }
            Debug.Log("[HallArtCapture] RESULT=" + (failures == 0 ? "PASS 3/3" : "FAIL"));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void EnablePost(GameObject holder)
        {
            var camDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (camDataType != null)
            {
                var data = new SerializedObject(holder.AddComponent(camDataType));
                var post = data.FindProperty("m_RenderPostProcessing");
                if (post != null) { post.boolValue = true; data.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            var profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");
            if (volumeType == null || profileType == null) { Debug.LogWarning("[HallArtCapture] SRP core volume types missing"); return; }
            var volume = holder.AddComponent(volumeType);
            SetMember(volume, "isGlobal", true);
            var profile = ScriptableObject.CreateInstance(profileType);
            SetMember(volume, "sharedProfile", profile);
            AddOverride(profile, "UnityEngine.Rendering.Universal.Tonemapping, Unity.RenderPipelines.Universal.Runtime",
                new[] { "mode.overrideState", "mode.value" }, new[] { "1", "2" });
            AddOverride(profile, "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime",
                new[] { "threshold.overrideState", "threshold.value", "intensity.overrideState", "intensity.value" },
                new[] { "1", "1.1", "1", "0.18" });
            AddOverride(profile, "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime",
                new[] { "intensity.overrideState", "intensity.value" }, new[] { "1", "0.22" });
        }

        static void SetMember(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name);
            if (p != null) { p.SetValue(target, value); return; }
            var f = target.GetType().GetField(name);
            if (f != null) f.SetValue(target, value);
        }

        static void AddOverride(object profile, string typeName, string[] props, string[] values)
        {
            var type = Type.GetType(typeName);
            if (type == null) { Debug.LogWarning("[HallArtCapture] post type missing: " + typeName); return; }
            var component = ScriptableObject.CreateInstance(type);
            var so = new SerializedObject(component);
            for (int i = 0; i < props.Length; i++)
            {
                var p = so.FindProperty(props[i]);
                if (p == null) continue;
                if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = values[i] == "1";
                else if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = int.Parse(values[i]);
                else p.floatValue = float.Parse(values[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var member = profile.GetType().GetProperty("components");
            var list = (member != null ? member.GetValue(profile) : profile.GetType().GetField("components").GetValue(profile)) as System.Collections.IList;
            list.Add(component);
        }
    }
}
#endif
