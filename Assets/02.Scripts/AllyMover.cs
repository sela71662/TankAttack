using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 아군 탱크 이동. 건물 주변(방어 범위) 안에서 돌아다니다가 적 탱크가 보이면 다가가 교전한다.
// post가 지정되면(Heavy) 그 지점으로 가서 멈춘 채 사거리 안의 적에게만 사격한다.
// 적이 NavMesh 밖(도로/다리)에 있으면 섬 입구에서 NavMesh를 끄고 게이트 경로를 거꾸로 따라가 다리를 건너 교전하고,
// 적이 사라지면 같은 길로 돌아와 다시 NavMesh로 복귀한다
[RequireComponent(typeof(TankInfo))]
public class AllyMover : MonoBehaviour
{
    public Transform home;             // 방어 중심 (코어)
    public SpawnGate[] gates;          // 다리를 건널 때 거꾸로 따라갈 경로(게이트 route)
    public float routeEngageMargin = 8.0f; // 경로 위에서 적이 (stopDistance + 이 값) 안이면 멈춰서 사격한다
    public float routeGateRange = 25.0f;   // 적이 경로에서 이 거리 안에 있어야 그 경로로 출격한다
    public Transform post;             // 지정 시 이 지점에서 정지 (Heavy)
    public Transform guardPost;        // 지정 시 이 다리 앞에서 대기하다가 적이 보이면 교전한다
    public float guardRadius = 7.0f;   // 다리 앞에서 흩어져 서 있을 반경
    public float leashRadius = 80.0f;  // 중심에서 이 거리 밖으로는 가지 않는다
    public float roamRadius = 40.0f;   // 배회할 반경 (guardPost가 없을 때)
    public float detectRadius = 70.0f; // 이 안의 적 탱크를 발견한다
    public float speedScale = 1.0f;

    NavMeshAgent agent;
    TankInfo info;
    TeamMember me;
    float nextThink;
    float idleUntil;
    bool wasMoving;

    // 다리 건너기(경로 모드) 상태
    bool onRoute;
    List<Vector3> routePath;
    int routeIdx;
    bool retreating;
    bool holding;
    float lastEnemySeen;
    SpawnGate sortieGate;
    Vector3 sortieEntry;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();

        if (info.flying) return;

        // NavMesh 위로 위치를 맞춘 뒤 에이전트를 만든다
        if (NavUtil.SampleNearest(transform.position, 15.0f, out NavMeshHit hit))
            transform.position = hit.position;

        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 1.2f;
        agent.height = 2.6f;
        agent.speed = info.moveSpeed * speedScale;
        agent.angularSpeed = 120.0f;
        agent.acceleration = 12.0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    Vector3 HomePos => home != null ? home.position : transform.position;

    void Update()
    {
        if (info.flying) { FlyUpdate(); return; }
        if (onRoute) { RouteUpdate(); return; }
        if (agent == null || !agent.isOnNavMesh || Time.time < nextThink) return;
        nextThink = Time.time + 0.5f;
        Think();
    }

    void Think()
    {
        if (post != null)
        {
            agent.stoppingDistance = 1.5f;
            GoTo(post.position);
            return;
        }

        Transform enemy = FindEnemyTank(detectRadius);
        if (enemy != null)
        {
            if (OnIsland(enemy.position))
            {
                sortieGate = null;
                agent.stoppingDistance = info.stopDistance;
                GoTo(Leash(enemy.position));
                wasMoving = true;
                return;
            }

            // 적이 도로나 다리 위에 있다: 그 경로의 섬 입구까지 간 뒤 다리를 건넌다
            var gate = ClosestGate(enemy.position);
            if (gate != null)
            {
                if (sortieGate != gate)
                {
                    sortieGate = gate;
                    sortieEntry = gate.route[gate.route.Length - 1].position;
                }
                if (FlatDist(transform.position, sortieEntry) < 3.0f)
                {
                    BeginRoute(gate);
                    return;
                }
                agent.stoppingDistance = 1.0f;
                GoTo(sortieEntry);
                wasMoving = true;
                return;
            }
        }
        else
        {
            sortieGate = null;
        }

        // 적이 없으면 다리 앞(guardPost)으로 가서 대기한다. 도착하면 잠시 쉬었다가 근처의 다른 지점으로 자리를 옮긴다.
        // guardPost가 없으면 방어 범위 안을 돌아다닌다
        agent.stoppingDistance = 1.0f;
        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.0f;
        if (wasMoving && arrived)
        {
            wasMoving = false;
            idleUntil = Time.time + Random.Range(2.0f, 5.0f);
        }
        if (!wasMoving && Time.time >= idleUntil)
        {
            Vector3 center = guardPost != null ? guardPost.position : HomePos;
            float radius = guardPost != null ? guardRadius : roamRadius;
            Vector2 r = Random.insideUnitCircle * radius;
            GoTo(Leash(center + new Vector3(r.x, 0, r.y)));
            wasMoving = true;
        }
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }

    // NavMesh(섬) 위에 있는 지점인가
    static bool OnIsland(Vector3 p)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 3.0f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < 4.0f;
    }

    // pos와 가장 가까운 게이트 경로. routeGateRange 안에 있는 경로가 없으면 null
    SpawnGate ClosestGate(Vector3 pos)
    {
        if (gates == null) return null;
        SpawnGate best = null;
        float bestDist = routeGateRange;
        foreach (var g in gates)
        {
            if (g == null || g.route == null || g.route.Length < 2) continue;
            for (int i = 0; i < g.route.Length - 1; i++)
            {
                float d = DistToSegment(pos, g.route[i].position, g.route[i + 1].position);
                if (d < bestDist) { bestDist = d; best = g; }
            }
        }
        return best;
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
        return Vector3.Distance(p, a + ab * t);
    }

    // 섬 입구에서 NavMesh를 끄고 게이트 경로를 거꾸로(섬 입구 -> 게이트) 따라가기 시작한다
    void BeginRoute(SpawnGate gate)
    {
        var pts = new List<Vector3> { transform.position };
        for (int i = gate.route.Length - 1; i >= 0; i--) pts.Add(gate.route[i].position);
        routePath = EnemyMover.SmoothPath(pts, 12.0f, 10);
        routeIdx = 0;
        retreating = false;
        holding = false;
        lastEnemySeen = Time.time;
        agent.enabled = false;
        onRoute = true;
    }

    // 경로를 다 돌아왔거나 중단하고 NavMesh로 복귀한다
    void EndRoute()
    {
        onRoute = false;
        routePath = null;
        sortieGate = null;
        wasMoving = false;
        if (NavUtil.SampleNearest(transform.position, 20.0f, out NavMeshHit hit))
            transform.position = hit.position;
        agent.enabled = true;
    }

    void RouteUpdate()
    {
        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 0.4f;
            Transform enemy = FindEnemyTank(detectRadius);
            holding = false;
            if (enemy != null)
            {
                lastEnemySeen = Time.time;
                holding = FlatDist(transform.position, enemy.position) <= info.stopDistance + routeEngageMargin;
            }
            retreating = enemy == null && Time.time - lastEnemySeen > 2.5f;
        }
        if (holding) return; // 사거리 안에 적이 있으면 멈춰서 사격 (포탑은 AITurretCtrl이 돌린다)

        if (retreating)
        {
            routeIdx = Mathf.Min(routeIdx, routePath.Count - 1);
            if (routeIdx < 0) { EndRoute(); return; }
        }
        else if (routeIdx >= routePath.Count)
        {
            return; // 게이트 앞까지 갔다: 적이 올 때까지 기다린다
        }

        Vector3 target = routePath[routeIdx];
        Vector3 to = target - transform.position;
        float step = info.moveSpeed * speedScale * Time.deltaTime;
        if (to.magnitude <= step)
        {
            transform.position = target;
            routeIdx += retreating ? -1 : 1;
            return;
        }

        transform.position += to.normalized * step;
        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 240.0f * Time.deltaTime);
    }

    // 방어 중심에서 leashRadius 안으로 목표를 제한한다
    Vector3 Leash(Vector3 p)
    {
        Vector3 c = HomePos;
        Vector3 d = p - c; d.y = 0;
        return c + Vector3.ClampMagnitude(d, leashRadius) + Vector3.up * (p.y - c.y);
    }

    void GoTo(Vector3 pos)
    {
        // 건물 안쪽이나 NavMesh 밖이면 가장 가까운 이동 가능 지점으로 보낸다
        if (NavUtil.SampleNearest(pos, 20.0f, out NavMeshHit hit))
            agent.SetDestination(hit.position);
    }

    Transform FindEnemyTank(float radius)
    {
        Transform best = null;
        float bestDist = radius * radius;
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !me.IsEnemy(t)) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t.transform; }
        }
        return best;
    }

    // 비행형 아군(UFO): 가장 가까운 적 탱크, 없으면 적 구조물(게이트)을 향해 날아간다
    void FlyUpdate()
    {
        Transform target = FindEnemyTank(detectRadius * 1.5f);
        if (target == null)
        {
            float best = float.MaxValue;
            foreach (var t in TeamMember.All)
            {
                if (!t.isStructure || !me.IsEnemy(t)) continue;
                float d = (t.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; target = t.transform; }
            }
        }
        if (target == null) return;

        Vector3 flat = target.position - transform.position;
        flat.y = 0;
        float altitude = HomePos.y + info.flyHeight;
        var p = transform.position;
        p.y = Mathf.MoveTowards(p.y, altitude, 10.0f * Time.deltaTime);

        if (flat.magnitude > info.stopDistance)
        {
            Vector3 dir = flat.normalized;
            p += dir * info.moveSpeed * speedScale * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180.0f * Time.deltaTime);
        }
        transform.position = p;
    }
}
