using System.Collections.Generic;
using UnityEngine;

namespace Fireworks
{
    // 종류별 불꽃 프리팹을 풀에서 꺼내 쏜다.
    public class FireworkLauncher : MonoBehaviour
    {
        [Tooltip("ShellType 순서와 같아야 한다.")]
        public FireworkShell[] prefabs;
        public float groundHeight = 0.5f;
        public int prewarmPerType = 8;

        private List<FireworkShell>[] pools;

        private void Awake()
        {
            pools = new List<FireworkShell>[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                pools[i] = new List<FireworkShell>();
                for (int k = 0; k < prewarmPerType; k++) pools[i].Add(Create(i));
            }
        }

        // 하위 파티클 시스템은 처음 재생될 때 자기 Burst를 한 번 내보낸다.
        // 그 헛폭발이 보이지 않도록 수면 아래 먼 곳에 만든다(실제 발사 위치는 Launch가 월드 좌표로 준다).
        private FireworkShell Create(int index)
        {
            return Instantiate(prefabs[index], new Vector3(0f, -1000f, 0f), Quaternion.identity, transform);
        }

        public float LeadTime(ShellType type) => prefabs[(int)type].LeadTime;

        public void Launch(FireworkCue cue, float lateBy)
        {
            int index = (int)cue.type;
            FireworkShell shell = null;
            foreach (var candidate in pools[index])
                if (!candidate.IsBusy) { shell = candidate; break; }
            if (shell == null)
            {
                shell = Create(index);
                pools[index].Add(shell);
            }
            var ground = new Vector3(cue.x - cue.drift, groundHeight, cue.z);
            var burst = new Vector3(cue.x, cue.height, cue.z);
            shell.Launch(ground, burst, cue.color, cue.color2, cue.scale, lateBy);
        }
    }
}
