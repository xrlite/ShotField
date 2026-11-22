using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// MR spike 调试可视化层（独立场景，不被 SC01_Main 引用）：
    /// 地面 1m 格线、原点 RGB 三轴、沙盘包围盒线框、平面缺失替身占位面、棋盘格顶面。
    /// 全部运行时程序化生成，不依赖外部资产；颜色/尺寸用于在透视背景上区分虚实物体。
    /// </summary>
    public sealed class MrSpikeDebugOverlay : MonoBehaviour
    {
        [Tooltip("微缩沙盘代理")]
        public Transform proxy;

        [Tooltip("平面缺失替身占位面")]
        public Transform placeholderPlane;

        private const float GridRange = 4f;
        private LineRenderer _boxWire;
        private readonly Vector3[] _boxCorners = new Vector3[8];

        private void Start()
        {
            BuildGrid();
            BuildAxes();
            BuildPlaceholder();
            ApplyCheckerTop();
            BuildBoxWire();
        }

        private void LateUpdate()
        {
            if (proxy != null && _boxWire != null) UpdateBoxWire();
        }

        private void BuildGrid()
        {
            var gray = new Color(0.55f, 0.55f, 0.55f, 0.9f);
            for (int i = -(int)GridRange; i <= (int)GridRange; i++)
            {
                var a = MakeLine("GridX_" + i, gray, 0.02f);
                a.SetPositions(new[] { new Vector3(-GridRange, 0, i), new Vector3(GridRange, 0, i) });
                var b = MakeLine("GridZ_" + i, gray, 0.02f);
                b.SetPositions(new[] { new Vector3(i, 0, -GridRange), new Vector3(i, 0, GridRange) });
            }
        }

        private void BuildAxes()
        {
            AxisLine(Vector3.right, Color.red, "AxisX");
            AxisLine(Vector3.up, Color.green, "AxisY");
            AxisLine(Vector3.forward, Color.blue, "AxisZ");
        }

        private void AxisLine(Vector3 dir, Color color, string name)
        {
            var line = MakeLine(name, color, 0.03f);
            line.SetPositions(new[] { Vector3.zero, dir });
        }

        private void BuildPlaceholder()
        {
            if (placeholderPlane == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "PlanePlaceholder";
                go.transform.SetParent(transform, false);
                placeholderPlane = go.transform;
            }
            var rend = placeholderPlane.GetComponent<MeshRenderer>();
            rend.sharedMaterial = MakeTransparentMaterial(new Color(0.3f, 0.6f, 1f, 0.22f));
            if (proxy != null)
            {
                placeholderPlane.position = proxy.position + Vector3.down * 0.035f;
                placeholderPlane.rotation = proxy.rotation;
                placeholderPlane.localScale = new Vector3(1.2f, 0.8f, 1f);
            }
            var label = new GameObject("PlaceholderLabel").AddComponent<TextMesh>();
            label.transform.SetParent(placeholderPlane, false);
            label.text = "平面缺失替身（非真实桌面）";
            label.characterSize = 0.05f;
            label.fontSize = 40;
            label.color = Color.cyan;
            label.transform.localPosition = new Vector3(0, 0.1f, 0);
        }

        private void ApplyCheckerTop()
        {
            if (proxy == null) return;
            var rend = proxy.GetComponent<MeshRenderer>();
            if (rend == null) return;
            var tex = new Texture2D(16, 16) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool light = ((x / 2) + (y / 2)) % 2 == 0;
                    tex.SetPixel(x, y, light ? Color.white : new Color(0.25f, 0.25f, 0.25f));
                }
            tex.Apply();
            rend.material.mainTexture = tex;
        }

        private void BuildBoxWire()
        {
            _boxWire = MakeLine("ProxyBoxWire", new Color(1f, 0.9f, 0.1f), 0.03f);
            _boxWire.loop = true;
        }

        private void UpdateBoxWire()
        {
            var s = proxy.lossyScale;
            var h = s * 0.5f;
            _boxCorners[0] = new Vector3(-h.x, -h.y, -h.z);
            _boxCorners[1] = new Vector3(h.x, -h.y, -h.z);
            _boxCorners[2] = new Vector3(h.x, -h.y, h.z);
            _boxCorners[3] = new Vector3(-h.x, -h.y, h.z);
            _boxCorners[4] = new Vector3(-h.x, h.y, -h.z);
            _boxCorners[5] = new Vector3(h.x, h.y, -h.z);
            _boxCorners[6] = new Vector3(h.x, h.y, h.z);
            _boxCorners[7] = new Vector3(-h.x, h.y, h.z);
            for (int i = 0; i < 8; i++)
                _boxCorners[i] = proxy.TransformPoint(_boxCorners[i]);
            // 上下两圈 + 四条竖边，用一条折线分段表达
            var pts = new[]
            {
                _boxCorners[0], _boxCorners[1], _boxCorners[2], _boxCorners[3], _boxCorners[0],
                _boxCorners[4], _boxCorners[5], _boxCorners[6], _boxCorners[7], _boxCorners[4],
                _boxCorners[0], _boxCorners[4], _boxCorners[1], _boxCorners[5],
                _boxCorners[2], _boxCorners[6], _boxCorners[3], _boxCorners[7]
            };
            _boxWire.positionCount = pts.Length;
            _boxWire.SetPositions(pts);
        }

        private LineRenderer MakeLine(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
            lr.material.color = color;
            lr.material.SetColor("_BaseColor", color);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 2;
            return lr;
        }

        private static Material MakeTransparentMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            mat.color = color;
            if (shader != null)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            return mat;
        }
    }
}
