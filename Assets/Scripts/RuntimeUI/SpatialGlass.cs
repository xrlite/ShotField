using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>代码构建 UI 的统一暖白玻璃入口；圆角、边框、高光由 shader 一次完成。</summary>
    public static class SpatialGlass
    {
        private const string ResourcePath = "UI/SpatialGlass";
        private static Material _source;

        public static Material Apply(Image image, Color color, float radius = 32f)
        {
            if (image == null) return null;
            if (_source == null) _source = Resources.Load<Material>(ResourcePath);
            if (_source == null)
            {
                Debug.LogWarning("[SpatialGlass] 缺少 Resources/UI/SpatialGlass.mat，回退 Image 原色");
                image.color = color;
                return null;
            }

            var material = new Material(_source);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_CornerRadius", radius);
            material.SetVector("_RectSize", new Vector4(image.rectTransform.rect.width,
                image.rectTransform.rect.height, 0f, 0f));
            image.color = Color.white;
            image.material = material;
            var binder = image.GetComponent<SpatialGlassBinder>();
            if (binder == null) binder = image.gameObject.AddComponent<SpatialGlassBinder>();
            binder.Bind(image, material);
            return material;
        }
    }

    /// <summary>RectTransform 尺寸变化时同步 shader 像素尺寸，保证任意比例圆角不拉伸。</summary>
    public sealed class SpatialGlassBinder : MonoBehaviour
    {
        private Image _image;
        private Material _material;
        private Vector2 _lastSize;

        public void Bind(Image image, Material material)
        {
            _image = image;
            _material = material;
            _lastSize = image.rectTransform.rect.size;
            ApplySize();
        }

        private void LateUpdate()
        {
            if (_image == null || _material == null) return;
            var size = _image.rectTransform.rect.size;
            if ((size - _lastSize).sqrMagnitude < 0.01f) return;
            _lastSize = size;
            ApplySize();
        }

        private void ApplySize()
        {
            _material.SetVector("_RectSize", new Vector4(_lastSize.x, _lastSize.y, 0f, 0f));
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
