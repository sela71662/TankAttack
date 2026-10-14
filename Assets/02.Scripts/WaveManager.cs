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

    // 웨이브 시작 때 건물에서 자동으로 소환되는 아군
    [System.Serializable]
    public class AllyEntry
    {
        public TankKind kind;
        public int count;
        public AllyEntry(TankKind k, int c) { kind = k; count = c; }
    }

    [System.Serializable]
    public class Wave
    {
        public string label;
        public List<Entry> entries = new List<Entry>();
        public List<AllyEntry> allies = new List<AllyEntry>();
        public float hpScale = 1.0f;   // 체력 배율
        public float sizeScale = 1.0f; // 크기 배율
    }

    public enum State { Waiting, Spawning, Fighting, Intermission, Victory, Defeat }

    public SpawnGate gateA;
    public SpawnGate gateB;
    public Health coreHealth;
    public AllySpawner allySpawner;   // 웨이브마다 자동 소환되는 아군을 만든다
    public GameObject[] enemyPrefabs; // TankInfo.kind로 종류를 구분한다
    public List<Wave> waves = new List<Wave>();

    public int startGold = 0;
    public float itemDropChance = 0.15f; // 적을 처치했을 때 아이템이 떨어질 확률

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

        GoldWallet.Reset(startGold);
        AllyUpgrades.Reset();
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
            if (allySpawner != null) allySpawner.SpawnBatch(waves[i].allies);
            yield return SpawnWave(waves[i], 1.0f + speedGrowthPerWave * i);

            if (state == State.Defeat) yield break;
            state = State.Fighting;
            while (AliveCount > 0 && state != State.Defeat)
                yield return null;

            if (state == State.Defeat) yield break;
            GoldWallet.Add(GoldWallet.WaveBonus(currentWave));
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
            var enemy = q.gate.Spawn(q.prefab, wave.hpScale, speedScale, wave.sizeScale);
            alive.Add(enemy);
            HookKillReward(enemy);
            // 큰 탱크는 길어서 간격을 더 둔다. 도로에서 앞차에 막히는 일을 줄인다
            yield return new WaitForSeconds(spawnInterval * wave.sizeScale);
        }
    }

    // 적이 죽으면 골드를 주고 확률로 아이템을 떨어뜨린다
    void HookKillReward(GameObject enemy)
    {
        var health = enemy.GetComponent<Health>();
        var info = enemy.GetComponent<TankInfo>();
        if (health == null || info == null) return;

        TankKind kind = info.kind;
        health.onDeath += h =>
        {
            GoldWallet.Add(GoldWallet.KillReward(kind));
            ItemPickup.TryDrop(h.transform.position, itemDropChance);
        };
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
        // W1: 플레이어 혼자 Gate A의 Original 2대를 상대한다 (조작 연습)
        waves.Add(MakeWave("조작 연습", 1f, null,
            new[] { new Entry(TankKind.Original, 2, 0) }));
        // 이후 웨이브: 아군은 건물에서 자동 소환(누적), 적은 양쪽 게이트에서 나온다. 적 체력은 2배.
        // 수와 체력은 골드 보상을 보며 조절한다
        waves.Add(MakeWave("첫 방어선", enemyHpScale,
            new[] { new AllyEntry(TankKind.Original, 1), new AllyEntry(TankKind.Heavy, 1) },
            new[] { new Entry(TankKind.Original, 2, 2), new Entry(TankKind.Heavy, 1, 0) }));
        waves.Add(MakeWave("다방향 압박", enemyHpScale,
            new[] { new AllyEntry(TankKind.Original, 2), new AllyEntry(TankKind.Heavy, 1), new AllyEntry(TankKind.Light, 1), new AllyEntry(TankKind.UFO, 1) },
            new[] { new Entry(TankKind.Original, 3, 2), new Entry(TankKind.Heavy, 1, 1), new Entry(TankKind.Light, 0, 1) }));
        waves.Add(MakeWave("공중 위협", enemyHpScale,
            new[] { new AllyEntry(TankKind.Original, 3), new AllyEntry(TankKind.Heavy, 1), new AllyEntry(TankKind.Light, 1), new AllyEntry(TankKind.UFO, 1) },
            new[] { new Entry(TankKind.Original, 4, 4), new Entry(TankKind.Heavy, 2, 1), new Entry(TankKind.Light, 1, 2), new Entry(TankKind.UFO, 1, 1) }));
        waves.Add(MakeWave("총력전", enemyHpScale,
            new[] { new AllyEntry(TankKind.Original, 3), new AllyEntry(TankKind.Heavy, 1), new AllyEntry(TankKind.Light, 1), new AllyEntry(TankKind.UFO, 1), new AllyEntry(TankKind.MegaBall, 1) },
            new[] { new Entry(TankKind.Original, 4, 4), new Entry(TankKind.Heavy, 2, 1), new Entry(TankKind.Light, 1, 2), new Entry(TankKind.UFO, 1, 0), new Entry(TankKind.MegaBall, 0, 1) }));
    }

    const float enemyHpScale = 2.0f; // W2부터 적 체력 배율

    static Wave MakeWave(string label, float hp, AllyEntry[] allies, Entry[] entries)
    {
        var w = new Wave { label = label, hpScale = hp };
        w.entries.AddRange(entries);
        if (allies != null) w.allies.AddRange(allies);
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
        GUI.Label(new Rect(0, 8, Screen.width, 90), $"{line}\n{core}\nGold {GoldWallet.Gold}", style);
    }
}
