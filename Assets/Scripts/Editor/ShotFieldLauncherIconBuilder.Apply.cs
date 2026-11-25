using UnityEditor;
using UnityEngine;

public static partial class ShotFieldLauncherIconBuilder
{
    // Unity 6000.6 已移除 Assets/Plugins/Android/res，图标只能走 PlayerSettings。
    // 新旧两套入口并存且部分被标成 error 级 obsolete，这里用反射按签名挨个试，避免编译期锁死。
    static void ApplyPlayerSettingsIcon(Color32[] px)
    {
        const string AssetPath = "Assets/Art/LauncherIcon/icon_composite_1024.png";
        AssetDatabase.Refresh();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (tex == null)
        {
            Debug.LogError("[IconBuilder] 找不到合成图资产，图标未设置: " + AssetPath);
            return;
        }

        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
        var arr = new Texture2D[] { tex };

        foreach (var m in typeof(PlayerSettings).GetMethods(flags))
        {
            if (m.Name != "SetIcons") continue;
            var ps = m.GetParameters();
            if (ps.Length != 3) continue;
            try
            {
                object target = GetStaticMember(ps[0].ParameterType, "Android");
                object kind = System.Enum.Parse(ps[1].ParameterType, "Application");
                m.Invoke(null, new object[] { target, kind, arr });
                AssetDatabase.SaveAssets();
                Debug.Log("[IconBuilder] SetIcons(3 参) 写入成功，目标=" + ps[0].ParameterType.Name);
                return;
            }
            catch (System.Exception e)
            {
                Debug.Log("[IconBuilder] SetIcons/3 失败: " + e.GetBaseException().Message);
            }
        }

        foreach (var m in typeof(PlayerSettings).GetMethods(flags))
        {
            if (m.Name != "SetIconsForTargetGroup") continue;
            var ps = m.GetParameters();
            if (ps.Length != 2) continue;
            try
            {
                m.Invoke(null, new object[] { BuildTargetGroup.Android, arr });
                AssetDatabase.SaveAssets();
                Debug.Log("[IconBuilder] SetIconsForTargetGroup(2 参) 写入成功");
                return;
            }
            catch (System.Exception e)
            {
                Debug.Log("[IconBuilder] SetIconsForTargetGroup/2 失败: " + e.GetBaseException().Message);
            }
        }
        Debug.LogWarning("[IconBuilder] PlayerSettings 图标入口全部失败，图标会回落默认。");
    }

    static object GetStaticMember(System.Type t, string name)
    {
        var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (f != null) return f.GetValue(null);
        var pr = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (pr != null) return pr.GetValue(null);
        var fm = t.GetMethod("FromTargetGroup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (fm != null) return fm.Invoke(null, new object[] { BuildTargetGroup.Android });
        throw new System.Exception("无法构造 " + t.Name + "." + name);
    }

    static void ValidateBackground(Color32[] bg)
    {
        int c = Canvas / 2, holes = 0;
        float r = Canvas / 2f;
        for (int y = 0; y < Canvas; y++)
        {
            for (int x = 0; x < Canvas; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                if (dx * dx + dy * dy <= r * r * 0.98f && bg[y * Canvas + x].a < 255) holes++;
            }
        }
        Debug.Log("[IconBuilder] 底板层 内切圆内非不透明像素=" + holes);
        if (holes > 0) Debug.LogWarning("[IconBuilder] 底板层内切圆内有透明，审核不通过。");
    }

    static void ValidateForeground(Color32[] fg2, Color32[] fg3)
    {
        int outside = CountOutside(fg2) + CountOutside(fg3);
        Debug.Log("[IconBuilder] 前景层 内切圆外像素=" + outside);
        if (outside > 0) Debug.LogWarning("[IconBuilder] 前景层超出内切圆，注视时会被裁切。");
    }

    static int CountOutside(Color32[] px)
    {
        if (px == null) return 0;
        int c = Canvas / 2, n = 0;
        float r = Canvas / 2f;
        for (int y = 0; y < Canvas; y++)
        {
            for (int x = 0; x < Canvas; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                if (dx * dx + dy * dy > r * r && px[y * Canvas + x].a > 0) n++;
            }
        }
        return n;
    }
}
