using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 3D 空间影视灯具实体与发光视锥线框（对齐 TLOU 照片模式三维白模）：
    /// 包含灯体本体、四叶遮光板 (Barn Doors) 与随角度/范围动态张合的发光投射线框。
    /// 支持空间交互抓取与焦点追踪。
    /// </summary>
    public class SpatialStudioLightFixture : MonoBehaviour
    {
        public Transform barnDoorTop;
        public Transform barnDoorBottom;
        public Transform barnDoorLeft;
        public Transform barnDoorRight;
        public LineRenderer frustumRenderer;
        public Transform housing;

        public float currentAngle = 40f;
        public float currentRange = 5f;

        private static Material _lineMat;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (housing == null)
            {
                var h = transform.Find("Housing");
                housing = h != null ? h : CreateHousing();
            }

            if (barnDoorTop == null || barnDoorBottom == null || barnDoorLeft == null || barnDoorRight == null)
            {
                CreateBarnDoors();
            }

            if (frustumRenderer == null)
            {
                frustumRenderer = GetComponent<LineRenderer>();
                if (frustumRenderer == null) frustumRenderer = gameObject.AddComponent<LineRenderer>();
                SetupLineRenderer(frustumRenderer);
            }
        }

        private Transform CreateHousing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Housing";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.18f, 0.18f, 0.22f);
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;
            return go.transform;
        }

        private void CreateBarnDoors()
        {
            barnDoorTop = MakeDoor("Door_Top", new Vector3(0f, 0.11f, 0.13f), new Vector3(-35f, 0f, 0f), new Vector3(0.20f, 0.01f, 0.10f));
            barnDoorBottom = MakeDoor("Door_Bottom", new Vector3(0f, -0.11f, 0.13f), new Vector3(35f, 0f, 0f), new Vector3(0.20f, 0.01f, 0.10f));
            barnDoorLeft = MakeDoor("Door_Left", new Vector3(-0.11f, 0f, 0.13f), new Vector3(0f, 35f, 0f), new Vector3(0.01f, 0.20f, 0.10f));
            barnDoorRight = MakeDoor("Door_Right", new Vector3(0.11f, 0f, 0.13f), new Vector3(0f, -35f, 0f), new Vector3(0.01f, 0.20f, 0.10f));
        }

        private Transform MakeDoor(string doorName, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            var existing = transform.Find(doorName);
            if (existing != null) return existing;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = doorName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go.transform;
        }

        private static void SetupLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = false;
            lr.startWidth = 0.008f;
            lr.endWidth = 0.012f;
            lr.positionCount = 11;
            if (_lineMat == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _lineMat = new Material(shader ?? Shader.Find("Hidden/Internal-Colored"));
            }
            lr.material = _lineMat;
            lr.startColor = new Color(0.9f, 0.95f, 1f, 0.85f);
            lr.endColor = new Color(0.9f, 0.95f, 1f, 0.5f);
        }

        public void UpdateFixture(float spotAngle, float range, Color lightColor, Transform focusTarget)
        {
            EnsureComponents();
            currentAngle = spotAngle;
            currentRange = Mathf.Max(0.5f, range);

            if (focusTarget != null)
            {
                transform.LookAt(focusTarget.position);
            }

            float halfRad = spotAngle * 0.5f * Mathf.Deg2Rad;
            float spread = Mathf.Tan(halfRad) * currentRange;

            Vector3 o = Vector3.forward * 0.11f; // 挡板前端原点
            Vector3 tl = new Vector3(-spread, spread, currentRange);
            Vector3 tr = new Vector3(spread, spread, currentRange);
            Vector3 br = new Vector3(spread, -spread, currentRange);
            Vector3 bl = new Vector3(-spread, -spread, currentRange);

            // 11 点单笔绘制：原点->TL->TR->BR->BL->TL->原点->TR->BR->原点->BL
            Vector3[] points = new Vector3[]
            {
                o, tl, tr, br, bl, tl, o, tr, br, o, bl
            };

            frustumRenderer.positionCount = points.Length;
            frustumRenderer.SetPositions(points);

            Color emissiveColor = Color.Lerp(Color.white, lightColor, 0.6f);
            emissiveColor.a = 0.8f;
            frustumRenderer.startColor = emissiveColor;
            frissiveEndColor(emissiveColor);
        }

        private void frissiveEndColor(Color c)
        {
            c.a = 0.45f;
            frustumRenderer.endColor = c;
        }

        public static SpatialStudioLightFixture AttachToLight(Light targetLight, string fixtureName = "StudioFixture")
        {
            if (targetLight == null) return null;
            var host = targetLight.transform.Find(fixtureName);
            GameObject go = host != null ? host.gameObject : new GameObject(fixtureName);
            go.transform.SetParent(targetLight.transform, false);
            var fixture = go.GetComponent<SpatialStudioLightFixture>() ?? go.AddComponent<SpatialStudioLightFixture>();
            fixture.EnsureComponents();
            return fixture;
        }
    }
}
