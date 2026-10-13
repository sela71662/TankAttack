using System.Collections;
using UnityEngine;

// 아군 스폰. 건물(building)에 붙어서 아군 탱크를 만들고 방어 범위 안을 돌아다니게 한다
public class AllySpawner : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform home;                // 방어 중심 (코어)
    public Transform[] heavyPosts;        // Heavy가 멈춰 설 지점 (각 다리 앞). 순서대로 배정한다
    public GameObject[] allyPrefabs;      // TankInfo.kind로 종류를 구분한다
    public TankKind[] startingAllies;     // 시작할 때 자동으로 스폰할 아군
    public float startDelay = 1.0f;
    public float staggerDelay = 1.5f;     // 연속 스폰 간격

    int nextPost;

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

        var mover = go.AddComponent<AllyMover>();
        mover.home = home;
        if (kind == TankKind.Heavy && heavyPosts != null && heavyPosts.Length > 0)
            mover.post = heavyPosts[nextPost++ % heavyPosts.Length];
        return go;
    }

    [ContextMenu("테스트: Original 아군 스폰")]
    void SpawnOriginal() { Spawn(TankKind.Original); }

    [ContextMenu("테스트: Heavy 아군 스폰")]
    void SpawnHeavy() { Spawn(TankKind.Heavy); }
}
