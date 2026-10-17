using UnityEngine;

// 적 스폰 게이트. 도로 끝에 놓이고, 여기서 생성된 적은 route(도로 -> 다리)를 따라 코어로 향한다
[RequireComponent(typeof(TeamMember), typeof(Health))]
public class SpawnGate : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform[] route;
    public Transform core;      // 공격/비행 목표
    public Transform approach;  // 지상 탱크가 코어로 접근할 때 NavMesh 위의 목표 지점
    public GameObject destroyEffect; // 파괴될 때 터지는 큰 폭발
    public float blastScale = 4.0f;  // 폭발 크기 배율. 100m 밖에서도 보이도록 크게
    public float smokeScale = 3.0f;  // 연기 크기 배율
    public GameObject smokeEffect;   // 파괴된 뒤 그 자리에 계속 피어오르는 연기 (Smoke01)

    // 게이트가 속한 팀은 항상 적 팀이다
    public static Team EnemyTeam => TeamConfig.playerTeam == Team.Red ? Team.Yellow : Team.Red;

    public bool IsDestroyed { get; private set; }

    void Awake()
    {
        var tm = GetComponent<TeamMember>();
        tm.isStructure = true;
        tm.SetTeam(EnemyTeam);

        // 파괴돼도 오브젝트는 남긴다 (경로 웨이포인트가 자식이라 이미 나온 적과 아군이 계속 쓴다)
        var health = GetComponent<Health>();
        health.destroyOnDeath = false;
        health.onDeath += _ => OnDestroyedByAllies();
    }

    // 파괴되면 더 이상 적이 나오지 않고, 표적에서도 빠진다
    void OnDestroyedByAllies()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        GoldWallet.Add(GoldWallet.gateReward);
        NoticeUI.Show($"{name.Trim()} DESTROYED", $"+{GoldWallet.gateReward}G", new Color(1f, 0.8f, 0.2f));
        if (destroyEffect != null)
        {
            var blast = Instantiate(destroyEffect, transform.position + Vector3.up * 3f, Quaternion.identity);
            ScaleEffect(blast, blastScale);
            Destroy(blast, 6.0f); // 폭발은 한 번만 터지고 사라진다 (연기만 승리할 때까지 남는다)
        }
        if (smokeEffect != null)
        {
            var smoke = Instantiate(smokeEffect, transform.position, Quaternion.identity);
            ScaleEffect(smoke, smokeScale);
            foreach (var ps in smoke.GetComponentsInChildren<ParticleSystem>()) ps.Play(); // Play On Awake가 꺼진 프리팹도 나오게 한다
            persistentEffects.Add(smoke);
        }
        GetComponent<TeamMember>().enabled = false; // TeamMember.All에서 빠진다
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        var bar = GetComponent<HealthBar>();
        if (bar != null) bar.enabled = false;
    }

    // 파괴된 게이트에서 계속 나오는 이펙트. 승리하면 WaveManager가 지운다
    static readonly System.Collections.Generic.List<GameObject> persistentEffects = new System.Collections.Generic.List<GameObject>();

    public static void ClearEffects()
    {
        foreach (var e in persistentEffects)
            if (e != null) Destroy(e);
        persistentEffects.Clear();
    }

    // 파티클 크기까지 함께 키운다 (Transform scale은 Hierarchy 모드에서만 파티클 크기에 반영된다)
    static void ScaleEffect(GameObject fx, float scale)
    {
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        fx.transform.localScale *= scale;
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
