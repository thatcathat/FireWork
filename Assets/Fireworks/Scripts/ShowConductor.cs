using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fireworks
{
    // 음악을 재생하고 곡 시각에 맞춰 큐를 발사한다.
    // 각 큐는 터지는 시각에서 상승 시간을 뺀 시점에 쏜다. 시각 기준은 오디오 DSP 시계다.
    public class ShowConductor : MonoBehaviour
    {
        public ShowData data;
        public AudioSource audioSource;
        public FireworkLauncher launcher;
        public CameraDirector cameraDirector;
        public CanvasGroup creditGroup;
        public Text creditText;
        public float startDelay = 1f;
        [Tooltip("확인용: 곡의 이 시각부터 재생한다.")]
        public float startAtSeconds;

        private struct Scheduled
        {
            public float launchTime;
            public FireworkCue cue;
        }

        private readonly List<Scheduled> queue = new List<Scheduled>();
        private int next;
        private double dspStart;
        private float songTime;

        public float SongTime => songTime;

        private void Start()
        {
            AddCues(data.autoCues);
            AddCues(data.manualCues);
            queue.Sort((a, b) => a.launchTime.CompareTo(b.launchTime));
            while (next < queue.Count && queue[next].launchTime < startAtSeconds) next++;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.clip = data.clip;
            audioSource.time = Mathf.Clamp(startAtSeconds, 0f, data.clip.length - 0.1f);
            dspStart = AudioSettings.dspTime + startDelay;
            audioSource.PlayScheduled(dspStart);
            songTime = startAtSeconds - startDelay;

            if (creditText != null) creditText.text = data.credit;
            if (creditGroup != null) creditGroup.alpha = 0f;
            if (cameraDirector != null) cameraDirector.Evaluate(data.cameraKeys, songTime);
        }

        private void AddCues(List<FireworkCue> cues)
        {
            foreach (var cue in cues)
                queue.Add(new Scheduled { launchTime = cue.time - launcher.LeadTime(cue.type), cue = cue });
        }

        private void Update()
        {
            // 프레임 사이에는 델타 시간으로 진행하고, DSP 시계와 벌어지면 맞춘다.
            float dspSong = (float)(AudioSettings.dspTime - dspStart) + startAtSeconds;
            songTime += Time.unscaledDeltaTime;
            if (Mathf.Abs(songTime - dspSong) > 0.05f) songTime = dspSong;

            while (next < queue.Count && queue[next].launchTime <= songTime)
            {
                launcher.Launch(queue[next].cue, songTime - queue[next].launchTime);
                next++;
            }

            if (cameraDirector != null) cameraDirector.Evaluate(data.cameraKeys, songTime);
            if (creditGroup != null)
            {
                float length = data.clip.length;
                creditGroup.alpha = Mathf.Max(Window(songTime, 2f, 4.5f, 10f, 13f), Window(songTime, length - 20f, length - 17f, length - 3f, length - 0.5f));
            }
        }

        private static float Window(float t, float fadeInStart, float fadeInEnd, float fadeOutStart, float fadeOutEnd)
        {
            return Mathf.Min(Mathf.InverseLerp(fadeInStart, fadeInEnd, t), 1f - Mathf.InverseLerp(fadeOutStart, fadeOutEnd, t));
        }
    }
}
