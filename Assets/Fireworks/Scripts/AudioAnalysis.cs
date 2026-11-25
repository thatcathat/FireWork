using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fireworks
{
    [Serializable]
    public struct Onset
    {
        public float time;        // 초
        public float strength;    // 0..1, 곡 안에서의 상대 세기
        public float brightness;  // 0..1, 낮은 음(0) ~ 높은 음(1)
    }

    // 음원 분석 결과. "Fireworks/1 Analyze Audio" 메뉴가 만든다.
    public class AudioAnalysis : ScriptableObject
    {
        public AudioClip clip;
        public float duration;
        public float hopSeconds;
        public float estimatedBpm;
        public float[] loudness = Array.Empty<float>();   // hop마다의 음량, 곡 최대치가 1
        public List<Onset> onsets = new List<Onset>();

        // time을 중심으로 window초 구간의 평균 음량.
        public float LoudnessAt(float time, float window)
        {
            if (loudness.Length == 0 || hopSeconds <= 0f) return 0f;
            int a = Mathf.Clamp(Mathf.FloorToInt((time - window * 0.5f) / hopSeconds), 0, loudness.Length - 1);
            int b = Mathf.Clamp(Mathf.CeilToInt((time + window * 0.5f) / hopSeconds), a, loudness.Length - 1);
            float sum = 0f;
            for (int i = a; i <= b; i++) sum += loudness[i];
            return sum / (b - a + 1);
        }
    }
}
