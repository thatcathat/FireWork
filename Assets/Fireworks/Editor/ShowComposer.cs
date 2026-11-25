using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fireworks.EditorTools
{
    // 분석 결과와 구간 표로 자동 큐(autoCues)를 만든다.
    // sections·cameraKeys·manualCues는 처음 만들 때만 기본값을 넣고, 이후에는 Reset 메뉴로만 바꾼다.
    public static class ShowComposer
    {
        public const string DataPath = "Assets/Fireworks/Data/ShowData.asset";
        private const float Bar = 2.4f;   // 100 BPM, 4박

        // 색
        private static readonly Color Ice = new Color(0.55f, 0.8f, 1f);
        private static readonly Color Warm = new Color(1f, 0.93f, 0.85f);
        private static readonly Color Lavender = new Color(0.75f, 0.6f, 1f);
        private static readonly Color Teal = new Color(0.2f, 1f, 0.8f);
        private static readonly Color Blue = new Color(0.3f, 0.55f, 1f);
        private static readonly Color Pink = new Color(1f, 0.45f, 0.7f);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.28f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.15f);
        private static readonly Color Magenta = new Color(1f, 0.25f, 0.75f);
        private static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);
        private static readonly Color Red = new Color(1f, 0.2f, 0.15f);
        private static readonly Color Violet = new Color(0.65f, 0.35f, 1f);
        private static readonly Color Green = new Color(0.35f, 1f, 0.4f);

        [MenuItem("Fireworks/4 Compose Show")]
        public static void Compose()
        {
            Debug.Log(ComposeReport());
        }

        [MenuItem("Fireworks/Reset/Sections And Camera To Defaults")]
        public static void ResetSections()
        {
            var data = GetOrCreateData();
            data.sections = DefaultSections();
            data.cameraKeys = DefaultCameraKeys(data.clip != null ? data.clip.length : 277.25f);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("Show sections and camera keys reset to defaults. Run 'Fireworks/4 Compose Show' to rebuild cues.");
        }

        [MenuItem("Fireworks/Reset/Manual Cues To Defaults")]
        public static void ResetManualCues()
        {
            var data = GetOrCreateData();
            data.manualCues = DefaultManualCues(data.firstBeatTime);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"Manual cues reset to defaults: {data.manualCues.Count}.");
        }

        public static ShowData GetOrCreateData()
        {
            var data = AssetDatabase.LoadAssetAtPath<ShowData>(DataPath);
            if (data != null) return data;
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            data = ScriptableObject.CreateInstance<ShowData>();
            data.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioAnalyzer.ClipPath);
            data.credit = "Stars In Her Skies — Scott Buckley\nCC BY 4.0 · scottbuckley.com.au";
            data.sections = DefaultSections();
            data.cameraKeys = DefaultCameraKeys(data.clip != null ? data.clip.length : 277.25f);
            data.manualCues = DefaultManualCues(data.firstBeatTime);
            AssetDatabase.CreateAsset(data, DataPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        public static string ComposeReport()
        {
            var analysis = AssetDatabase.LoadAssetAtPath<AudioAnalysis>(AudioAnalyzer.AnalysisPath);
            if (analysis == null) throw new InvalidOperationException("Run 'Fireworks/1 Analyze Audio' first.");
            var data = GetOrCreateData();

            var rng = new System.Random(data.seed);
            float beat = 60f / data.bpm;
            float sixteenth = beat / 4f;
            var cues = new List<FireworkCue>();
            var report = new StringBuilder();
            int lastLane = -10;

            for (int si = 0; si < data.sections.Count; si++)
            {
                var s = data.sections[si];
                float end = si + 1 < data.sections.Count ? data.sections[si + 1].startTime : analysis.duration;
                int before = cues.Count;
                if (s.types.Length > 0 && s.palette.Length > 0)
                {
                    var hits = new List<Vector2>();   // x = 시각, y = 세기
                    foreach (var o in analysis.onsets)
                    {
                        if (o.time < s.startTime || o.time >= end || o.strength < s.onsetThreshold) continue;
                        float grid = data.firstBeatTime + Mathf.Round((o.time - data.firstBeatTime) / sixteenth) * sixteenth;
                        hits.Add(new Vector2(Mathf.Abs(grid - o.time) < 0.07f ? grid : o.time, o.strength));
                    }
                    if (s.fillBeats > 0f)
                        for (float t = s.startTime; t < end - 0.05f; t += beat * s.fillBeats)
                            if (!hits.Any(h => Mathf.Abs(h.x - t) < 0.2f)) hits.Add(new Vector2(t, s.fillStrength));

                    hits.Sort((a, b) => a.x.CompareTo(b.x));
                    for (int i = hits.Count - 1; i > 0; i--)
                        if (hits[i].x - hits[i - 1].x < 0.1f)
                        {
                            hits[i - 1] = new Vector2(hits[i - 1].x, Mathf.Max(hits[i].y, hits[i - 1].y));
                            hits.RemoveAt(i);
                        }

                    foreach (var hit in hits)
                    {
                        float strength = Mathf.Clamp01(hit.y);
                        int n = 1 + Mathf.RoundToInt(Mathf.Pow(strength, 1.5f) * (s.maxShells - 1));
                        float scale = Mathf.Lerp(s.scaleMin, s.scaleMax, strength);
                        if (n >= 3)
                        {
                            // 강한 음: 좌우 대칭 부채꼴로 동시에 쏜다.
                            var type = Pick(rng, s.types);
                            Color a = Pick(rng, s.palette), b = Pick(rng, s.palette);
                            for (int k = 0; k < n; k++)
                            {
                                float u = (k + 0.5f) / n;
                                float arch = 1f - Mathf.Abs(u * 2f - 1f);
                                cues.Add(new FireworkCue
                                {
                                    time = hit.x, type = type,
                                    x = Mathf.Lerp(-s.spread, s.spread, u) + Range(rng, -2f, 2f),
                                    z = Range(rng, -6f, 6f),
                                    height = Mathf.Lerp(s.heightMin, s.heightMax, 0.25f + 0.75f * arch),
                                    drift = Range(rng, -3f, 3f),
                                    color = k % 2 == 0 ? a : b, color2 = k % 2 == 0 ? b : a, scale = scale
                                });
                            }
                            if (strength >= 0.95f && s.maxShells >= 5)
                                cues.Add(new FireworkCue { time = hit.x, type = ShellType.Double, x = 0f, z = 4f, height = s.heightMax + 6f, color = Gold, color2 = Warm, scale = s.scaleMax * 1.25f });
                        }
                        else
                        {
                            for (int k = 0; k < n; k++)
                            {
                                int lane;
                                do { lane = rng.Next(0, 9); } while (Mathf.Abs(lane - lastLane) < 2);
                                lastLane = lane;
                                cues.Add(new FireworkCue
                                {
                                    time = hit.x, type = Pick(rng, s.types),
                                    x = Mathf.Lerp(-s.spread, s.spread, lane / 8f) + Range(rng, -3f, 3f),
                                    z = Range(rng, -8f, 8f),
                                    height = Range(rng, s.heightMin, s.heightMax),
                                    drift = Range(rng, -4f, 4f),
                                    color = Pick(rng, s.palette), color2 = Pick(rng, s.palette), scale = scale * Range(rng, 0.92f, 1.08f)
                                });
                            }
                        }
                    }
                }
                int count = cues.Count - before;
                report.AppendLine($"{s.name,-10} {s.startTime,7:F2}-{end,7:F2}  auto cues {count,4}  ({count / Mathf.Max(0.01f, end - s.startTime):F2}/s)");
            }

            data.autoCues = cues;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            report.Insert(0, $"Show composed: {cues.Count} auto cues + {data.manualCues.Count} manual cues.\n");
            return report.ToString();
        }

        // ---------- 기본값(직접 다듬은 부분) ----------

        private static List<ShowSection> DefaultSections()
        {
            float first = 0.13f;
            return new List<ShowSection>
            {
                // 인트로와 피날레·아웃트로는 수동 큐만 쓴다.
                new ShowSection { name = "Intro", startTime = 0f, onsetThreshold = 2f },
                new ShowSection
                {
                    name = "Quiet", startTime = first + 7 * Bar, onsetThreshold = 0.012f, fillBeats = 8f, maxShells = 1,
                    heightMin = 36f, heightMax = 48f, spread = 34f, scaleMin = 0.62f, scaleMax = 0.8f,
                    types = new[] { ShellType.Peony, ShellType.Comet, ShellType.Comet, ShellType.Ring },
                    palette = new[] { Ice, Warm, Lavender }
                },
                new ShowSection
                {
                    name = "Flow", startTime = first + 27 * Bar, onsetThreshold = 0.025f, fillBeats = 4f, maxShells = 2,
                    heightMin = 42f, heightMax = 58f, spread = 48f, scaleMin = 0.75f, scaleMax = 1f,
                    types = new[] { ShellType.Peony, ShellType.Peony, ShellType.Ring, ShellType.Willow, ShellType.Crackle, ShellType.Comet },
                    palette = new[] { Teal, Blue, Pink, Warm }
                },
                new ShowSection
                {
                    name = "Build", startTime = first + 47 * Bar, onsetThreshold = 0.08f, fillBeats = 2f, maxShells = 3,
                    heightMin = 45f, heightMax = 65f, spread = 58f, scaleMin = 0.85f, scaleMax = 1.2f,
                    types = new[] { ShellType.Peony, ShellType.Double, ShellType.Crackle, ShellType.Ring, ShellType.Palm },
                    palette = new[] { Gold, Orange, Magenta, Cyan }
                },
                new ShowSection
                {
                    name = "ClimaxA", startTime = first + 67 * Bar, onsetThreshold = 0.1f, fillBeats = 2f, maxShells = 5,
                    heightMin = 50f, heightMax = 74f, spread = 68f, scaleMin = 0.95f, scaleMax = 1.35f,
                    types = new[] { ShellType.Peony, ShellType.Double, ShellType.Palm, ShellType.Crackle, ShellType.Willow, ShellType.Ring },
                    palette = new[] { Red, Gold, Warm, Violet, Green }
                },
                new ShowSection
                {
                    name = "ClimaxB", startTime = first + 87 * Bar, onsetThreshold = 0.1f, fillBeats = 1f, maxShells = 7,
                    heightMin = 50f, heightMax = 80f, spread = 78f, scaleMin = 1f, scaleMax = 1.45f,
                    types = new[] { ShellType.Peony, ShellType.Double, ShellType.Double, ShellType.Palm, ShellType.Crackle, ShellType.Willow, ShellType.Ring },
                    palette = new[] { Red, Gold, Warm, Violet, Green, Cyan, Magenta }
                },
                new ShowSection { name = "Finale", startTime = first + 103 * Bar, onsetThreshold = 2f },
            };
        }

        private static List<CameraKey> DefaultCameraKeys(float duration)
        {
            float first = 0.13f;
            return new List<CameraKey>
            {
                new CameraKey { time = 0f, position = new Vector3(0f, 3f, -70f), lookAt = new Vector3(0f, 18f, 0f), fov = 38f },
                new CameraKey { time = first + 7 * Bar, position = new Vector3(0f, 4f, -85f), lookAt = new Vector3(0f, 26f, 0f), fov = 42f },
                new CameraKey { time = first + 27 * Bar, position = new Vector3(-25f, 6f, -105f), lookAt = new Vector3(0f, 32f, 0f), fov = 46f },
                new CameraKey { time = first + 47 * Bar, position = new Vector3(20f, 7f, -115f), lookAt = new Vector3(0f, 36f, 0f), fov = 50f },
                new CameraKey { time = first + 67 * Bar, position = new Vector3(0f, 8f, -135f), lookAt = new Vector3(0f, 40f, 0f), fov = 54f },
                new CameraKey { time = first + 87 * Bar, position = new Vector3(-15f, 10f, -150f), lookAt = new Vector3(0f, 42f, 0f), fov = 56f },
                new CameraKey { time = first + 103 * Bar, position = new Vector3(0f, 10f, -160f), lookAt = new Vector3(0f, 44f, 0f), fov = 58f },
                new CameraKey { time = 262f, position = new Vector3(0f, 9f, -165f), lookAt = new Vector3(0f, 46f, 0f), fov = 58f },
                new CameraKey { time = duration, position = new Vector3(0f, 14f, -175f), lookAt = new Vector3(0f, 62f, 0f), fov = 56f },
            };
        }

        private static List<FireworkCue> DefaultManualCues(float first)
        {
            var cues = new List<FireworkCue>();
            float BarTime(float bar) => first + bar * Bar;
            void Add(float time, ShellType type, float x, float height, Color color, Color color2, float scale, float z = 0f, float drift = 0f)
                => cues.Add(new FireworkCue { time = time, type = type, x = x, z = z, height = height, drift = drift, color = color, color2 = color2, scale = scale });
            void Fountains(float time, int count, float spread, float step, Color color)
            {
                for (int i = 0; i < count; i++)
                    Add(time + i * step, ShellType.Fountain, Mathf.Lerp(-spread, spread, count == 1 ? 0.5f : i / (count - 1f)), 0f, color, color, 1f);
            }

            // 인트로: 음악이 떠오르는 동안 작은 혜성 몇 발.
            Add(BarTime(2), ShellType.Comet, -16f, 24f, Ice, Warm, 0.7f, 0f, -3f);
            Add(BarTime(4), ShellType.Comet, 14f, 28f, Warm, Ice, 0.7f, 0f, 3f);
            Add(BarTime(5), ShellType.Comet, -7f, 26f, Lavender, Warm, 0.7f, 0f, -2f);
            Add(BarTime(6), ShellType.Comet, 9f, 32f, Ice, Warm, 0.75f, 0f, 2f);

            // 절정 직전 한 마디: 분수가 왼쪽에서 오른쪽으로 번지고, 첫 타격에 대형 이중 폭발.
            Fountains(BarTime(66), 7, 60f, 0.3f, Gold);
            Add(BarTime(67), ShellType.Double, 0f, 80f, Gold, Warm, 1.6f, 6f);

            // 절정 중간과 후반 시작: 분수 일제 점화 + 중앙 대형.
            Fountains(BarTime(77), 7, 62f, 0f, Warm);
            Add(BarTime(77), ShellType.Palm, 0f, 80f, Gold, Warm, 1.6f, 6f);
            Fountains(BarTime(87), 9, 76f, 0f, Gold);
            Add(BarTime(87), ShellType.Willow, 0f, 84f, Gold, Warm, 1.7f, 6f);

            // 피날레 두 마디: 0.3초 간격으로 좌우를 오가며 점점 높이.
            Color[] finale = { Red, Gold, Warm, Violet, Green, Cyan, Magenta };
            ShellType[] finaleTypes = { ShellType.Peony, ShellType.Crackle, ShellType.Ring, ShellType.Double };
            for (int i = 0; i < 16; i++)
            {
                float u = i / 15f;
                float x = Mathf.Lerp(-72f, 72f, Mathf.PingPong(i / 5f, 1f));
                Add(BarTime(103) + i * 0.3f, finaleTypes[i % finaleTypes.Length], x, Mathf.Lerp(46f, 76f, u), finale[i % finale.Length], finale[(i + 3) % finale.Length], 1.15f, (i % 3 - 1) * 5f);
            }

            // 마지막 타격: 금빛 버들 부채 + 이중 폭발 + 반짝이 + 분수.
            float last = BarTime(105);
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f;
                Add(last, ShellType.Willow, Mathf.Lerp(-82f, 82f, u), Mathf.Lerp(56f, 86f, 1f - Mathf.Abs(u * 2f - 1f)), Gold, Warm, 1.7f, 4f);
            }
            for (int i = 0; i < 5; i++)
                Add(last, ShellType.Double, Mathf.Lerp(-60f, 60f, i / 4f), i % 2 == 0 ? 62f : 72f, Warm, Gold, 1.5f, -4f);
            Fountains(last, 9, 76f, 0f, Gold);
            for (int i = 0; i < 5; i++)
            {
                Add(last + 0.6f, ShellType.Crackle, Mathf.Lerp(-66f, 66f, i / 4f), 58f + (i % 2) * 14f, Warm, Warm, 1.5f);
                Add(last + 1.2f, ShellType.Crackle, Mathf.Lerp(-50f, 50f, i / 4f), 70f - (i % 2) * 14f, Gold, Warm, 1.4f, 5f);
            }

            // 여운: 잔향 위로 버들 몇 발, 마지막은 작은 혜성 하나.
            Add(255.5f, ShellType.Willow, 0f, 72f, Gold, Warm, 1.4f);
            Add(258.5f, ShellType.Willow, -30f, 60f, Gold, Warm, 1.1f);
            Add(258.8f, ShellType.Willow, 30f, 62f, Gold, Warm, 1.1f);
            Add(262.5f, ShellType.Comet, 0f, 40f, Warm, Ice, 0.8f);
            return cues;
        }

        private static T Pick<T>(System.Random rng, T[] items) => items[rng.Next(items.Length)];
        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
