using UnityEngine;

// 적 스폰 게이트. 도로 끝에 놓이고, 여기서 생성된 적은 route(도로 -> 다리)를 따라 코어로 향한다
[RequireComponent(typeof(TeamMember), typeof(Health))]
public class SpawnGate : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform[] route;
    public Transform core;      // 공격/비행 목표
    public Transform approach;  // 지상 탱크가 코어로 접근할 때 NavMesh 위의 목표 지점

    // 게이트가 속한 팀은 항상 적 팀이다
    public static Team EnemyTeam => TeamConfig.playerTeam == Team.Red ? Team.Yellow : Team.Red;

    void Awake()
    {
        var tm = GetComponent<TeamMember>();
        tm.isStructure = true;
        tm.SetTeam(EnemyTeam);
    }

    // hpScale: 체력 배율, speedScale: 이동 속도 배율, sizeScale: 크기 배율 (Wave 7: 2배)
    public GameObject Spawn(GameObject prefab, float hpScale = 1.0f, float speedScale = 1.0f, float sizeScale = 1.0f)
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 look = route != null && route.Length > 0 ? route[0].position - pos : transform.forward;
        look.y = 0;
        Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

        var go = Instantiate(prefab, pos, rot);
        go.transform.localScale *= sizeScale;

        go.GetComponent<TeamMember>().SetTeam(EnemyTeam);
        var health = go.GetComponent<Health>();
        health.hpScale = hpScale;
        health.Refresh();

        var info = go.GetComponent<TankInfo>();
        if (info != null && info.flying)
        {
            var f = go.AddComponent<FlyingMover>();
            f.core = core;
            f.speedScale = speedScale;
        }
        else
        {
            var m = go.AddComponent<EnemyMover>();
            m.route = route;
            m.approach = approach != null ? approach : core;
            m.speedScale = speedScale;
        }
        return go;
    }

    void OnDrawGizmos()
    {
        if (route == null || route.Length == 0) return;
        Gizmos.color = Color.red;
        Vector3 prev = spawnPoint != null ? spawnPoint.position : transform.position;
        foreach (var w in route)
        {
            if (w == null) continue;
            Gizmos.DrawLine(prev, w.position);
            Gizmos.DrawSphere(w.position, 0.8f);
            prev = w.position;
        }
    }
}
