using UnityEngine;

namespace Fireworks
{
    // 수면 반사용 보조 카메라. 주 카메라를 수면 아래로 뒤집은 위치에서 RenderTexture에 그리고,
    // 그 텍스처를 전역 셰이더 속성 _FireworksReflectionTex로 넘긴다(Water 셰이더가 읽음).
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class PlanarReflection : MonoBehaviour
    {
        public Camera source;
        public float waterHeight;
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;

        private static readonly int ReflectionTexId = Shader.PropertyToID("_FireworksReflectionTex");
        private Camera cam;
        private RenderTexture texture;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
        }

        private void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (texture != null)
            {
                texture.Release();
                DestroyImmediate(texture);
                texture = null;
            }
        }

        private void LateUpdate()
        {
            if (source == null || cam == null) return;
            EnsureTexture();

            Vector3 position = source.transform.position;
            position.y = 2f * waterHeight - position.y;
            Vector3 forward = source.transform.forward;
            forward.y = -forward.y;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

            cam.fieldOfView = source.fieldOfView;
            cam.aspect = source.aspect;
            cam.nearClipPlane = source.nearClipPlane;
            cam.farClipPlane = source.farClipPlane;
        }

        private void EnsureTexture()
        {
            int width = Mathf.Max(256, Mathf.RoundToInt(source.pixelWidth * resolutionScale));
            int height = Mathf.Max(144, Mathf.RoundToInt(source.pixelHeight * resolutionScale));
            if (texture != null && texture.width == width && texture.height == height)
            {
                if (cam.targetTexture != texture) cam.targetTexture = texture;
                Shader.SetGlobalTexture(ReflectionTexId, texture);
                return;
            }
            if (texture != null)
            {
                cam.targetTexture = null;
                texture.Release();
                DestroyImmediate(texture);
            }
            texture = new RenderTexture(width, height, 16, RenderTextureFormat.DefaultHDR)
            {
                name = "FireworksReflection",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            cam.targetTexture = texture;
            Shader.SetGlobalTexture(ReflectionTexId, texture);
        }
    }
}
