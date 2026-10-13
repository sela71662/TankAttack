using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 웨이브 진행. 두 게이트에서 적을 시간차로 스폰하고, 모두 처치하면 다음 웨이브로 넘어간다.
// 코어가 파괴되면 패배, 마지막 웨이브를 막아내면 승리
public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public TankKind kind;
        public int gateA; // Gate A(남쪽 도로 끝)에서 스폰할 수
        public int gateB; // Gate B(북쪽 도로 끝)에서 스폰할 수
        public Entry(TankKind k, int a, int b) { kind = k; gateA = a; gateB = b; }
    }

    [System.Serializable]
    public class Wave
    {
        public string label;
        public List<Entry> entries = new List<Entry>();
        public float hpScale = 1.0f;   // 체력 배율
        public float sizeScale = 1.0f; // 크기 배율
    }

    public enum State { Waiting, Spawning, Fighting, Intermission, Victory, Defeat }

    public SpawnGate gateA;
    public SpawnGate gateB;
    public Health coreHealth;
    public GameObject[] enemyPrefabs; // TankInfo.kind로 종류를 구분한다
    public List<Wave> waves = new List<Wave>();

    public bool autoStart = true;
    public float startDelay = 10.0f;
    public float betweenWaves = 15.0f;
    public float spawnInterval = 1.5f;       // 연속 스폰 간격 (크기 배율만큼 늘어난다)
    public float speedGrowthPerWave = 0.08f; // 웨이브마다 이동 속도가 이만큼씩 빨라진다

    public State state { get; private set; } = State.Waiting;
    public int currentWave { get; private set; } // 1부터 시작, 0은 아직 시작 전
    public float intermissionLeft { get; private set; }

    public event System.Action<int> onWaveStart;
    public event System.Action<int> onWaveCleared;
    public event System.Action onVictory;
    public event System.Action onDefeat;

    readonly Dictionary<TankKind, GameObject> prefabByKind = new Dictionary<TankKind, GameObject>();
    readonly List<GameObject> alive = new List<GameObject>();

    void Awake()
    {
        foreach (var p in enemyPrefabs)
        {
            var info = p != null ? p.GetComponent<TankInfo>() : null;
            if (info != null) prefabByKind[info.kind] = p;
        }
        if (waves.Count == 0) FillDefaultWaves();
    }

    void Start()
    {
        if (coreHealth != null) coreHealth.onDeath += _ => Defeat();
        if (autoStart) StartWaves();
    }

    public void StartWaves()
    {
        if (state == State.Waiting) StartCoroutine(Run());
    }

    public int AliveCount
    {
        get { alive.RemoveAll(g => g == null); return alive.Count; }
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < waves.Count; i++)
        {
            if (state == State.Defeat) yield break;

            currentWave = i + 1;
            state = State.Spawning;
            onWaveStart?.Invoke(currentWave);
            yield return SpawnWave(waves[i], 1.0f + speedGrowthPerWave * i);

            if (state == State.Defeat) yield break;
            state = State.Fighting;
            while (AliveCount > 0 && state != State.Defeat)
                yield return null;

            if (state == State.Defeat) yield break;
            onWaveCleared?.Invoke(currentWave);

            if (i < waves.Count - 1)
            {
                state = State.Intermission;
                for (intermissionLeft = betweenWaves; intermissionLeft > 0; intermissionLeft -= Time.deltaTime)
                {
                    if (state == State.Defeat) yield break;
                    yield return null;
                }
            }
        }

        state = State.Victory;
        onVictory?.Invoke();
    }

    IEnumerator SpawnWave(Wave wave, float speedScale)
    {
        // (게이트, 프리팹) 목록을 만들어 섞어서 종류와 방향이 고루 나오게 한다
        var queue = new List<(SpawnGate gate, GameObject prefab)>();
        foreach (var e in wave.entries)
        {
            if (!prefabByKind.TryGetValue(e.kind, out var prefab))
            {
                Debug.LogWarning($"WaveManager: {e.kind} 프리팹이 enemyPrefabs에 없다");
                continue;
            }
            for (int i = 0; i < e.gateA; i++) queue.Add((gateA, prefab));
            for (int i = 0; i < e.gateB; i++) queue.Add((gateB, prefab));
        }
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }

        foreach (var q in queue)
        {
            if (state == State.Defeat) yield break;
            alive.Add(q.gate.Spawn(q.prefab, wave.hpScale, speedScale, wave.sizeScale));
            // 큰 탱크는 길어서 간격을 더 둔다. 도로에서 앞차에 막히는 일을 줄인다
            yield return new WaitForSeconds(spawnInterval * wave.sizeScale);
        }
    }

    void Defeat()
    {
        if (state == State.Defeat || state == State.Victory) return;
        state = State.Defeat;
        onDefeat?.Invoke();
    }

    [ContextMenu("기본 웨이브 채우기")]
    public void FillDefaultWaves()
    {
        waves.Clear();
        // W1: 한쪽 게이트(A)에서만 Original 2대
        waves.Add(MakeWave("기초 방어", 1f, 1f,
            new Entry(TankKind.Original, 2, 0)));
        waves.Add(MakeWave("기초 방어", 1f, 1f,
            new Entry(TankKind.Original, 3, 3)));
        waves.Add(MakeWave("다방향 압박", 1f, 1f,
            new Entry(TankKind.Original, 2, 2), new Entry(TankKind.Heavy, 1, 1), new Entry(TankKind.UFO, 1, 1)));
        waves.Add(MakeWave("다방향 압박", 1f, 1f,
            new Entry(TankKind.Original, 2, 2), new Entry(TankKind.Heavy, 2, 2), new Entry(TankKind.UFO, 2, 1)));
        waves.Add(MakeWave("진형 붕괴 위기", 1f, 1f,
            new Entry(TankKind.Original, 2, 2), new Entry(TankKind.Light, 2, 2), new Entry(TankKind.MegaBall, 1, 2)));
        waves.Add(MakeWave("진형 붕괴 위기", 1f, 1f,
            new Entry(TankKind.Original, 2, 3), new Entry(TankKind.Light, 3, 2), new Entry(TankKind.MegaBall, 2, 2), new Entry(TankKind.UFO, 1, 1)));
        // 총력전: 모든 종류가 각 방향에서 동시에, 크기 2배 / 체력 10배
        waves.Add(MakeWave("총력전", 10f, 2f,
            new Entry(TankKind.Original, 2, 2), new Entry(TankKind.Light, 2, 2), new Entry(TankKind.Heavy, 2, 2),
            new Entry(TankKind.MegaBall, 2, 2), new Entry(TankKind.UFO, 2, 2)));
    }

    static Wave MakeWave(string label, float hp, float size, params Entry[] entries)
    {
        var w = new Wave { label = label, hpScale = hp, sizeScale = size };
        w.entries.AddRange(entries);
        return w;
    }

    // 간단한 상태 표시 (UI는 4단계에서 정식으로 만든다)
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperCenter };
        string core = coreHealth != null ? $"Core {coreHealth.hp:F0}/{coreHealth.maxHp:F0}" : "";
        string line = state switch
        {
            State.Waiting => "Ready",
            State.Intermission => $"Next wave in {Mathf.CeilToInt(intermissionLeft)}s",
            State.Victory => "VICTORY",
            State.Defeat => "DEFEAT",
            _ => $"Wave {currentWave}/{waves.Count}   Enemies {AliveCount}"
        };
        GUI.Label(new Rect(0, 8, Screen.width, 60), $"{line}\n{core}", style);
    }
}
