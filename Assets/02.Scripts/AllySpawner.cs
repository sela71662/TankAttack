using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 아군 스폰. 건물(building)에 붙어서 아군 탱크를 만들고 방어 범위 안을 돌아다니게 한다
public class AllySpawner : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform home;                // 방어 중심 (코어)
    public Transform[] heavyPosts;        // 각 다리 섬쪽 입구. Heavy는 여기서 멈춰 서고, 나머지는 여기를 지키며 적을 기다린다
    public GameObject[] allyPrefabs;      // TankInfo.kind로 종류를 구분한다
    public TankKind[] startingAllies;     // 시작할 때 자동으로 스폰할 아군
    public float startDelay = 1.0f;
    public float staggerDelay = 1.5f;     // 연속 스폰 간격
    public SpawnGate[] gates;             // 아군이 다리를 건널 때 따라갈 적 게이트 경로
    public int maxAllies = 20;            // 구매로 소환할 수 있는 아군 수 상한 (웨이브 자동 소환은 상한을 무시한다)

    int nextHeavyPost;
    int nextGuardPost;
    readonly List<GameObject> alive = new List<GameObject>();

    public int AliveCount
    {
        get { alive.RemoveAll(g => g == null); return alive.Count; }
    }

    public bool CanBuy => AliveCount < maxAllies;

    // 웨이브 시작 때 자동으로 소환할 아군을 간격을 두고 스폰한다
    public void SpawnBatch(IEnumerable<WaveManager.AllyEntry> entries)
    {
        var kinds = new List<TankKind>();
        foreach (var e in entries)
            for (int i = 0; i < e.count; i++) kinds.Add(e.kind);
        if (kinds.Count > 0) StartCoroutine(SpawnList(kinds));
    }

    IEnumerator SpawnList(List<TankKind> kinds)
    {
        foreach (var kind in kinds)
        {
            Spawn(kind);
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    public static Team AllyTeam => TeamConfig.playerTeam;

    void Start()
    {
        if (startingAllies != null && startingAllies.Length > 0)
            StartCoroutine(SpawnStarting());
    }

    IEnumerator SpawnStarting()
    {
        yield return new WaitForSeconds(startDelay);
        foreach (var kind in startingAllies)
        {
            Spawn(kind);
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    GameObject FindPrefab(TankKind kind)
    {
        foreach (var p in allyPrefabs)
        {
            var info = p != null ? p.GetComponent<TankInfo>() : null;
            if (info != null && info.kind == kind) return p;
        }
        return null;
    }

    // 4단계(보상 UI)에서도 이 함수로 아군을 만든다
    public GameObject Spawn(TankKind kind)
    {
        var prefab = FindPrefab(kind);
        if (prefab == null)
        {
            Debug.LogWarning($"AllySpawner: {kind} 프리팹이 allyPrefabs에 없다", this);
            return null;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        var go = Instantiate(prefab, pos, rot);

        go.GetComponent<TeamMember>().SetTeam(AllyTeam);
        var health = go.GetComponent<Health>();
        if (health != null) health.Refresh();

        alive.Add(go);
        var mover = go.AddComponent<AllyMover>();
        mover.home = home;
        mover.gates = gates;
        // Heavy는 다리 앞을 지키고, 나머지 지상 아군은 다리를 건너 적 게이트를 공격하러 간다
        var info = go.GetComponent<TankInfo>();
        mover.assault = kind != TankKind.Heavy && (info == null || !info.flying);
        if (heavyPosts != null && heavyPosts.Length > 0)
        {
            // Heavy는 다리 앞에 서서 움직이지 않고, 나머지는 다리를 번갈아 맡아 지킨다 (적이 오는 쪽을 고르게 막는다)
            if (kind == TankKind.Heavy) mover.post = heavyPosts[nextHeavyPost++ % heavyPosts.Length];
            else mover.guardPost = heavyPosts[nextGuardPost++ % heavyPosts.Length];
        }
        return go;
    }

    [ContextMenu("테스트: Original 아군 스폰")]
    void SpawnOriginal() { Spawn(TankKind.Original); }

    [ContextMenu("테스트: Heavy 아군 스폰")]
    void SpawnHeavy() { Spawn(TankKind.Heavy); }
}
