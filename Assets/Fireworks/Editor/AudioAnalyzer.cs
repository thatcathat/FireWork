using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fireworks.EditorTools
{
    // 음원에서 음량 곡선과 강한 음(온셋)을 뽑아 AudioAnalysis.asset으로 저장한다.
    // 다시 실행하면 같은 자산을 덮어쓴다. 요약은 Temp/FireworksAnalysis.txt에 쓴다.
    public static class AudioAnalyzer
    {
        public const string ClipPath = "Assets/Audio/Stars In Her Skies.mp3";
        public const string AnalysisPath = "Assets/Fireworks/Data/AudioAnalysis.asset";

        private const int FrameSize = 2048;
        private const int Hop = 1024;

        [MenuItem("Fireworks/1 Analyze Audio")]
        public static void Analyze()
        {
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Audio clip not found: " + ClipPath);
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !settings.preloadAudioData || importer.loadInBackground)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = clip.channels;
            var raw = new float[clip.samples * channels];
            if (!clip.GetData(raw, 0)) throw new InvalidOperationException("Could not read audio samples.");

            var mono = new float[clip.samples];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += raw[i * channels + c];
                mono[i] = sum / channels;
            }

            int frames = Mathf.Max(1, (mono.Length - FrameSize) / Hop);
            float hopSeconds = (float)Hop / clip.frequency;
            var rms = new float[frames];
            var flux = new float[frames];
            var centroid = new float[frames];

            var window = new float[FrameSize];
            for (int i = 0; i < FrameSize; i++) window[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (FrameSize - 1));
            var re = new float[FrameSize];
            var im = new float[FrameSize];
            int bins = FrameSize / 2;
            var prev = new float[bins];
            var cur = new float[bins];
            float binHz = (float)clip.frequency / FrameSize;

            for (int f = 0; f < frames; f++)
            {
                int start = f * Hop;
                double energy = 0;
                for (int i = 0; i < FrameSize; i++)
                {
                    float s = mono[start + i];
                    energy += s * s;
                    re[i] = s * window[i];
                    im[i] = 0f;
                }
                rms[f] = Mathf.Sqrt((float)(energy / FrameSize));
                Fft(re, im);

                float fl = 0f, magSum = 0f, weighted = 0f;
                for (int b = 1; b < bins; b++)
                {
                    float mag = Mathf.Sqrt(re[b] * re[b] + im[b] * im[b]);
                    cur[b] = Mathf.Log(1f + 100f * mag / FrameSize);
                    float d = cur[b] - prev[b];
                    if (d > 0f) fl += d;
                    magSum += mag;
                    weighted += mag * b * binHz;
                }
                flux[f] = fl;
                centroid[f] = magSum > 1e-6f ? weighted / magSum : 0f;
                var swap = prev; prev = cur; cur = swap;
            }

            // 음량: 곡의 상위 0.1% 값을 1로 맞춘다.
            var sorted = (float[])rms.Clone();
            Array.Sort(sorted);
            float peak = Mathf.Max(1e-6f, sorted[Mathf.Clamp((int)(sorted.Length * 0.999f), 0, sorted.Length - 1)]);
            var loudness = rms.Select(v => Mathf.Clamp01(v / peak)).ToArray();

            // 온셋: 주변 평균보다 뚜렷이 큰 국소 최대.
            int avgWin = Mathf.RoundToInt(0.4f / hopSeconds);
            int peakWin = Mathf.RoundToInt(0.12f / hopSeconds);
            float globalMean = flux.Average();
            var prefix = new double[frames + 1];
            for (int i = 0; i < frames; i++) prefix[i + 1] = prefix[i] + flux[i];
            var onsets = new List<Onset>();
            for (int i = 1; i < frames - 1; i++)
            {
                int a = Mathf.Max(0, i - avgWin), b = Mathf.Min(frames - 1, i + avgWin);
                float local = (float)((prefix[b + 1] - prefix[a]) / (b - a + 1));
                float threshold = local * 1.4f + globalMean * 0.1f;
                if (flux[i] <= threshold) continue;
                bool isMax = true;
                for (int j = Mathf.Max(0, i - peakWin); j <= Mathf.Min(frames - 1, i + peakWin) && isMax; j++)
                    if (flux[j] > flux[i]) isMax = false;
                if (!isMax) continue;
                float bright = Mathf.InverseLerp(Mathf.Log(300f), Mathf.Log(5000f), Mathf.Log(Mathf.Max(1f, centroid[i])));
                // 소리는 프레임 창 안쪽에서 시작하므로 프레임 시작 시각에 30ms를 더한다.
                onsets.Add(new Onset { time = i * hopSeconds + 0.03f, strength = (flux[i] - threshold) * Mathf.Sqrt(loudness[i]), brightness = bright });
            }
            if (onsets.Count > 0)
            {
                var strengths = onsets.Select(o => o.strength).OrderBy(v => v).ToArray();
                float top = Mathf.Max(1e-6f, strengths[Mathf.Clamp((int)(strengths.Length * 0.98f), 0, strengths.Length - 1)]);
                for (int i = 0; i < onsets.Count; i++)
                {
                    var o = onsets[i];
                    o.strength = Mathf.Clamp01(o.strength / top);
                    onsets[i] = o;
                }
            }

            float bpm = EstimateBpm(flux, hopSeconds, out string bpmCandidates);

            Directory.CreateDirectory(Path.GetDirectoryName(AnalysisPath));
            var asset = AssetDatabase.LoadAssetAtPath<AudioAnalysis>(AnalysisPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AudioAnalysis>();
                AssetDatabase.CreateAsset(asset, AnalysisPath);
            }
            asset.clip = clip;
            asset.duration = clip.length;
            asset.hopSeconds = hopSeconds;
            asset.estimatedBpm = bpm;
            asset.loudness = loudness;
            asset.onsets = onsets;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            WriteSummary(asset, bpmCandidates);
            Debug.Log($"Audio analysed: {clip.length:F1}s, {onsets.Count} onsets, about {bpm:F1} BPM. Summary: Temp/FireworksAnalysis.txt");
        }

        private static float EstimateBpm(float[] flux, float hopSeconds, out string candidates)
        {
            float mean = flux.Average();
            var x = flux.Select(v => v - mean).ToArray();
            var scores = new List<(float bpm, float score)>();
            for (float bpm = 50f; bpm <= 180f; bpm += 0.5f)
            {
                float lagF = 60f / bpm / hopSeconds;
                int lag = Mathf.RoundToInt(lagF);
                double sum = 0;
                for (int i = 0; i + lag * 2 < x.Length; i++) sum += x[i] * x[i + lag] + 0.5 * x[i] * x[i + lag * 2];
                scores.Add((bpm, (float)sum));
            }
            var best = scores.OrderByDescending(s => s.score).Take(8).ToList();
            candidates = string.Join(", ", best.Select(s => $"{s.bpm:F1}({s.score / best[0].score:F2})"));
            return best[0].bpm;
        }

        private static void WriteSummary(AudioAnalysis a, string bpmCandidates)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"duration={a.duration:F2}s hop={a.hopSeconds * 1000f:F1}ms onsets={a.onsets.Count} bpm={a.estimatedBpm:F1}");
            sb.AppendLine("bpm candidates: " + bpmCandidates);
            sb.AppendLine();
            sb.AppendLine("time   loud  onsets  strongest  bright  bar");
            const float bucket = 5f;
            for (float t = 0f; t < a.duration; t += bucket)
            {
                float loud = a.LoudnessAt(t + bucket * 0.5f, bucket);
                var inBucket = a.onsets.Where(o => o.time >= t && o.time < t + bucket).ToList();
                float strongest = inBucket.Count > 0 ? inBucket.Max(o => o.strength) : 0f;
                float bright = inBucket.Count > 0 ? inBucket.Average(o => o.brightness) : 0f;
                sb.AppendLine($"{(int)t / 60}:{(int)t % 60:00}  {loud:F2}  {inBucket.Count,4}    {strongest:F2}       {bright:F2}    {new string('#', Mathf.RoundToInt(loud * 50f))}");
            }
            sb.AppendLine();
            sb.AppendLine("strongest onsets (time strength brightness):");
            foreach (var o in a.onsets.OrderByDescending(o => o.strength).Take(80).OrderBy(o => o.time))
                sb.AppendLine($"{o.time,7:F2}  {o.strength:F2}  {o.brightness:F2}");
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "FireworksAnalysis.txt");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // 제자리 radix-2 FFT. 길이는 2의 거듭제곱이어야 한다.
        private static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    float tr = re[i]; re[i] = re[j]; re[j] = tr;
                    float ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                float ang = -2f * Mathf.PI / len;
                float wr = Mathf.Cos(ang), wi = Mathf.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1f, ci = 0f;
                    int half = len >> 1;
                    for (int k = 0; k < half; k++)
                    {
                        int u = i + k, v = u + half;
                        float xr = re[v] * cr - im[v] * ci;
                        float xi = re[v] * ci + im[v] * cr;
                        re[v] = re[u] - xr; im[v] = im[u] - xi;
                        re[u] += xr; im[u] += xi;
                        float ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }
    }
}
