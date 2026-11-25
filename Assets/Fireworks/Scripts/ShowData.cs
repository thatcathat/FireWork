using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fireworks
{
    // 프리팹 배열(FireworkLauncher.prefabs)의 순서와 같다.
    public enum ShellType { Peony, Willow, Ring, Palm, Crackle, Double, Comet, Fountain }

    [Serializable]
    public class FireworkCue
    {
        [Tooltip("터지는 시각(초). Fountain은 뿜기 시작하는 시각.")]
        public float time;
        public ShellType type;
        public float x;
        public float z;
        public float height = 50f;
        [Tooltip("발사 지점이 터지는 지점에서 옆으로 떨어진 거리.")]
        public float drift;
        public Color color = Color.white;
        public Color color2 = Color.white;
        public float scale = 1f;
    }

    [Serializable]
    public class ShowSection
    {
        public string name;
        public float startTime;
        [Tooltip("이보다 약한 온셋은 무시한다. 1보다 크면 자동 큐를 만들지 않는다.")]
        public float onsetThreshold = 0.1f;
        [Tooltip("온셋이 없는 곳을 채우는 간격(박). 0이면 채우지 않는다.")]
        public float fillBeats;
        public float fillStrength = 0.15f;
        [Tooltip("가장 강한 음에서 동시에 쏘는 수.")]
        public int maxShells = 1;
        public float heightMin = 40f;
        public float heightMax = 55f;
        [Tooltip("좌우로 퍼지는 범위(±).")]
        public float spread = 40f;
        public float scaleMin = 0.8f;
        public float scaleMax = 1f;
        [Tooltip("같은 종류를 여러 번 넣으면 그만큼 자주 나온다.")]
        public ShellType[] types = Array.Empty<ShellType>();
        public Color[] palette = Array.Empty<Color>();
    }

    [Serializable]
    public class CameraKey
    {
        public float time;
        public Vector3 position;
        public Vector3 lookAt;
        public float fov = 50f;
    }

    // 쇼 전체 데이터. autoCues는 "Fireworks/4 Compose Show"가 다시 만들고,
    // sections·cameraKeys·manualCues는 사람이 고치는 부분이라 Compose가 건드리지 않는다.
    public class ShowData : ScriptableObject
    {
        public AudioClip clip;
        public float bpm = 100f;
        [Tooltip("첫 마디의 첫 박 시각(초).")]
        public float firstBeatTime = 0.13f;
        public int seed = 20261123;
        [TextArea] public string credit;

        public List<ShowSection> sections = new List<ShowSection>();
        public List<CameraKey> cameraKeys = new List<CameraKey>();
        public List<FireworkCue> manualCues = new List<FireworkCue>();
        public List<FireworkCue> autoCues = new List<FireworkCue>();
    }
}
