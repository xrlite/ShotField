using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 道具分类。对标小云雀资源库的道具子类。
    /// 只登记工程里**真实存在**的资产，空分类如实留空，不塞占位条目。
    /// </summary>
    public enum PropCategory
    {
        Actor = 0,          // 人体素模
        Furniture = 1,      // 家具
        DailyProp = 2,      // 日用品
        Environment = 3,    // 环境装饰（植物/灯/窗）
        Architecture = 4,   // 建筑构件（门/台阶/障碍）—— 当前无资产
        Vehicle = 5,        // 载具 —— 当前无资产
        Other = 6           // 其他
    }

    /// <summary>
    /// 一条道具登记。prefabPath 为空且 isProcedural=true 表示"待程序化生成"，不是资产。
    /// </summary>
    [Serializable]
    public sealed class PropEntry
    {
        public string id;
        public string displayName;
        public PropCategory category;
        public SemanticMarker.Kind semanticKind = SemanticMarker.Kind.Prop;
        public string prefabPath;
        public bool isProcedural;
        public Vector3 defaultScale = Vector3.one;
        public string note;
    }

    /// <summary>
    /// 道具库数据层。纯数据，不做加载、不碰场景、不读磁盘。
    /// 路径可加载性由 Editor 侧 PropLibraryVerifier 用 AssetDatabase 校验。
    /// </summary>
    public static class PropLibrary
    {
        private static readonly List<PropEntry> entries = Build();

        public static IReadOnlyList<PropEntry> All { get { return entries; } }
        public static int Count { get { return entries.Count; } }

        public static PropEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == id) return entries[i];
            }
            return null;
        }

        /// <summary>某个分类下的全部条目；空分类返回空列表，不返回 null。</summary>
        public static List<PropEntry> ByCategory(PropCategory category)
        {
            var list = new List<PropEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category == category) list.Add(entries[i]);
            }
            return list;
        }

        /// <summary>有资产的分类数（用于验收断言，避免为凑数造假条目）。</summary>
        public static int NonEmptyCategoryCount()
        {
            int n = 0;
            for (int c = 0; c <= (int)PropCategory.Other; c++)
            {
                if (ByCategory((PropCategory)c).Count > 0) n++;
            }
            return n;
        }

        public static int CountWithPrefab()
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.IsNullOrEmpty(entries[i].prefabPath)) n++;
            }
            return n;
        }

        private static List<PropEntry> Build()
        {
            var list = new List<PropEntry>();

            Add(list, "actorA", "主角 A（人形素模）", PropCategory.Actor,
                SemanticMarker.Kind.ActorPrimary,
                "Assets/Prefabs/SC01/ActorA_Character.prefab",
                "Biped_Humanoid.fbx 来源待确权（裁定项 12）");

            // M1 六步链第 2 步要"从资源列表选一个人物"。原先库里只有 actorA 一条，
            // 用户点开面板只能看到同一个人。以下六色是 scenario-01 的正式阵容，
            // 直接登记进库，面板才选得出来。
            Add(list, "actorRed", "角色 红（Protagonist）", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorA_Red.prefab", "六色阵容 · #FF2D2D");
            Add(list, "actorBlue", "角色 蓝（Antagonist）", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorB_Blue.prefab", "六色阵容 · #0055FF");
            Add(list, "actorYellow", "角色 黄", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorC_Yellow.prefab", "六色阵容");
            Add(list, "actorGreen", "角色 绿", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorD_Green.prefab", "六色阵容");
            Add(list, "actorViolet", "角色 紫", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorE_Violet.prefab", "六色阵容");
            Add(list, "actorMagenta", "角色 品红", PropCategory.Actor,
                SemanticMarker.Kind.ActorSecondary,
                "Assets/Prefabs/SC01/ActorF_Magenta.prefab", "六色阵容");

            Add(list, "sofa", "沙发", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Prefabs/SC01/Sofa_Prop.prefab", "");
            Add(list, "chair", "单椅", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Prefabs/SC01/Chair_Prop.prefab", "");
            Add(list, "coffeeTable", "茶几", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Prefabs/SC01/Desk_Prop.prefab", "");
            Add(list, "shoeCabinet", "玄关鞋柜", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Prefabs/SC01/Cabinet_Prop.prefab", "");
            Add(list, "armChairRaw", "单椅（原始导入）", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Models/Furniture/modern_arm_chair_01/Imported/modern_arm_chair_01.prefab", "");
            Add(list, "coffeeTableRaw", "茶几（原始导入）", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Models/Furniture/modern_coffee_table_01/Imported/modern_coffee_table_01.prefab", "");
            Add(list, "woodenCabinetRaw", "木柜（原始导入）", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Models/Furniture/modern_wooden_cabinet/Imported/modern_wooden_cabinet.prefab", "");
            Add(list, "sofaRaw", "沙发（原始导入）", PropCategory.Furniture,
                SemanticMarker.Kind.Prop,
                "Assets/Models/Furniture/Sofa_01/Imported/Sofa_01.prefab", "");

            Add(list, "envelope", "信封（关键道具）", PropCategory.DailyProp,
                SemanticMarker.Kind.KeyProp,
                "Assets/Prefabs/SC01/Envelope_Prop.prefab",
                "关键面朝向约束依赖此项，见制作包 §2");

            Add(list, "plant", "盆栽", PropCategory.Environment,
                SemanticMarker.Kind.Environment,
                "Assets/Prefabs/SC01/Plant_Prop.prefab", "");
            Add(list, "pottedPlantRaw", "盆栽（原始导入）", PropCategory.Environment,
                SemanticMarker.Kind.Environment,
                "Assets/Models/Furniture/potted_plant_02/Imported/potted_plant_02.prefab", "");

            return list;
        }

        private static void Add(List<PropEntry> list, string id, string displayName,
            PropCategory category, SemanticMarker.Kind kind, string prefabPath, string note)
        {
            var e = new PropEntry();
            e.id = id;
            e.displayName = displayName;
            e.category = category;
            e.semanticKind = kind;
            e.prefabPath = prefabPath;
            e.isProcedural = string.IsNullOrEmpty(prefabPath);
            e.note = note;
            list.Add(e);
        }
    }
}
