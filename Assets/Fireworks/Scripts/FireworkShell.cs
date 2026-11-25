using System;
using UnityEngine;

namespace Fireworks
{
    [Serializable]
    public class BurstPart
    {
        public ParticleSystem system;
        public Vector2 speed;
        public Vector2 size;
        [Tooltip("공기 저항 계수(크기 1 기준). 0이면 저항 없음.")]
        public float drag;
        public bool tint = true;
        public bool useSecondColor;
        public bool randomTilt;
    }

    // 불꽃 한 발. 상승 파티클 하나를 쏘면 수명이 끝날 때 Sub Emitter가 폭발을 만든다.
    // 상승 수명을 정해 두므로 터지는 시각이 발사 시각 + riseTime으로 고정된다.
    public class FireworkShell : MonoBehaviour
    {
        public float riseTime = 2f;
        public float busySeconds = 6f;
        public ParticleSystem rise;
        public float riseSize = 0.6f;
        public Color riseColor = new Color(1f, 0.85f, 0.65f, 1f);
        [Tooltip("상승 없이 지면에서 바로 재생하는 효과(분수).")]
        public ParticleSystem groundEffect;
        public BurstPart[] parts = Array.Empty<BurstPart>();

        private float busyUntil = -1f;

        public bool IsBusy => Time.time < busyUntil;
        public float LeadTime => rise != null ? riseTime : 0f;

        // lateBy: 발사가 예정보다 늦은 시간. 그만큼 상승을 줄여 터지는 시각을 지킨다.
        public void Launch(Vector3 ground, Vector3 burst, Color color, Color color2, float scale, float lateBy)
        {
            busyUntil = Time.time + busySeconds;
            float sizeScale = Mathf.Sqrt(scale);
            foreach (var part in parts)
            {
                var main = part.system.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(part.speed.x * scale, part.speed.y * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(part.size.x * sizeScale, part.size.y * sizeScale);
                if (part.drag > 0f)
                {
                    // 저항을 크기에 반비례하게 해야 퍼지는 반경이 크기에 비례한다.
                    var limit = part.system.limitVelocityOverLifetime;
                    limit.drag = part.drag / scale;
                }
                if (part.tint)
                {
                    Color c = part.useSecondColor ? color2 : color;
                    main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.35f));
                }
                if (part.randomTilt)
                {
                    var shape = part.system.shape;
                    // Circle 모양은 기본이 수평면이다. 90도 세워 카메라를 향하게 한 뒤 조금 기울인다.
                    shape.rotation = new Vector3(90f + UnityEngine.Random.Range(-35f, 35f), UnityEngine.Random.Range(-35f, 35f), 0f);
                }
            }

            if (rise != null)
            {
                float t = Mathf.Max(0.2f, riseTime - Mathf.Max(0f, lateBy));
                Vector3 d = burst - ground;
                var main = rise.main;
                main.gravityModifier = 2f * d.y / (t * t) / Physics.gravity.magnitude;   // 정점에서 속도가 0이 되게
                var emit = new ParticleSystem.EmitParams
                {
                    position = ground,
                    velocity = new Vector3(d.x / t, 2f * d.y / t, d.z / t),
                    startLifetime = t,
                    startSize = riseSize * sizeScale,
                    startColor = riseColor
                };
                rise.Emit(emit, 1);
            }
            else if (groundEffect != null)
            {
                groundEffect.transform.position = ground;
                groundEffect.Play(true);
            }
        }
    }
}
