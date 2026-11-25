using System.Collections.Generic;
using UnityEngine;

namespace Fireworks
{
    // 카메라 키 사이를 곡 시각에 따라 부드럽게 지나간다(Catmull-Rom). 약한 흔들림을 더한다.
    public class CameraDirector : MonoBehaviour
    {
        public Camera cam;
        public float swayAmount = 0.5f;
        public float swaySpeed = 0.06f;

        public void Evaluate(IReadOnlyList<CameraKey> keys, float time)
        {
            if (cam == null || keys == null || keys.Count == 0) return;
            int i = 0;
            while (i < keys.Count - 2 && time >= keys[i + 1].time) i++;
            var k1 = keys[i];
            var k2 = keys[Mathf.Min(i + 1, keys.Count - 1)];
            var k0 = keys[Mathf.Max(i - 1, 0)];
            var k3 = keys[Mathf.Min(i + 2, keys.Count - 1)];
            float u = k2.time > k1.time ? Mathf.Clamp01((time - k1.time) / (k2.time - k1.time)) : 0f;

            Vector3 position = CatmullRom(k0.position, k1.position, k2.position, k3.position, u);
            Vector3 lookAt = CatmullRom(k0.lookAt, k1.lookAt, k2.lookAt, k3.lookAt, u);
            float s = time * swaySpeed;
            position += new Vector3(Mathf.PerlinNoise(s, 0.3f) - 0.5f, (Mathf.PerlinNoise(0.7f, s) - 0.5f) * 0.5f, 0f) * (2f * swayAmount);

            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            cam.fieldOfView = Mathf.Lerp(k1.fov, k2.fov, Mathf.SmoothStep(0f, 1f, u));
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
