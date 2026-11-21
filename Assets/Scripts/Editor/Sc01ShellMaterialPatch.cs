#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B2 材质：把 41 处"URP 包默认白 Lit"换成项目自己的 MAT_STU_* 影棚材质。
    /// 这是"白模感"的首因 —— 房间内壳从来没被赋过材质。
    /// </summary>
    internal static class Sc01ShellMaterialPatch
    {
        private static readonly string[] WallFaces =
        {
            "RightWall", "EntryWallLeft", "EntryWallRight", "EntryLintel",
            "BackWindowLeft", "BackWindowRight", "BackWindowHeader", "BackWindowSill"
        };

        private static readonly string[] WoodParts = { "RightBaseboard", "BackBaseboard", "Door" };

        private static readonly string[] WindowFrames =
        {
            "WindowFrameTop", "WindowFrameBottom", "WindowFrameFront", "WindowFrameBack"
        };

        private static readonly string[] EdgeParts =
        {
            "SETW_OpenCapTop", "SETW_OpenPostFront", "SETW_OpenPostBack"
        };

        private static readonly string[] FixtureParts =
        {
            "Housing", "Door_Top", "Door_Bottom", "Door_Left", "Door_Right"
        };

        [MenuItem("ShotField/SC01/Patch B2 清白模材质")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("ShellMat");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("SHELLMAT");
        }

        internal static bool Apply()
        {
            var wall = Sc01PatchCore.LoadMat("MAT_STU_Wall_v05");
            var floor = Sc01PatchCore.LoadMat("MAT_STU_Floor_v05");
            var wood = Sc01PatchCore.LoadMat("MAT_STU_Wood_URP");
            var frame = Sc01PatchCore.LoadMat("MAT_STU_Frame_v05");

            foreach (var name in WallFaces)
                Sc01PatchCore.PaintNamed(name, wall, "B2 墙面 " + name);

            Sc01PatchCore.PaintNamed("Floor", floor, "B2 地面");
            foreach (var name in WoodParts)
                Sc01PatchCore.PaintNamed(name, wood, "B2 木件 " + name);
            foreach (var name in WindowFrames)
                Sc01PatchCore.PaintNamed(name, frame, "B2 窗框 " + name);
            foreach (var name in EdgeParts)
                Sc01PatchCore.PaintNamed(name, frame, "B2 开口收边 " + name);

            PaintFixtures(frame);
            Sc01PatchCore.PaintNamed("SC01_FreeCameraHandle", frame, "B2 自由机位手柄");
            return Sc01PatchCore.AllPassed;
        }

        private static void PaintFixtures(Material frame)
        {
            for (var slot = 1; slot <= 3; slot++)
            {
                var root = Sc01PatchCore.FindDeep("StudioFixture_Slot" + slot);
                if (root == null)
                {
                    Sc01PatchCore.Check(false, "B2 灯夹具 Slot" + slot, "缺失");
                    continue;
                }
                foreach (var part in FixtureParts)
                {
                    var target = Sc01PatchCore.FindUnder(root, part);
                    Sc01PatchCore.Paint(target, frame, "B2 灯夹具 Slot" + slot + "/" + part);
                }
            }
        }
    }
}
#endif
