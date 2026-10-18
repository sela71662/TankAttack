using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 아군 탱크 이동. 건물 주변(방어 범위) 안에서 돌아다니다가 적 탱크가 보이면 다가가 교전한다.
// post가 지정되면(Heavy) 그 지점으로 가서 멈춘 채 사거리 안의 적에게만 사격한다.
// 적이 섬 밖(도로/다리)에 있으면 섬 입구에서 NavMesh를 끄고 게이트 경로를 거꾸로 따라가 다리를 건너 교전하고,
// 적이 사라지면 같은 길로 돌아와 다시 NavMesh로 복귀한다.
// assault가 켜져 있으면 섬에 적이 없을 때 스스로 다리를 건너 도로를 따라 게이트까지 가서 적과 게이트를 공격하고,
// 그 게이트가 파괴되면 섬으로 돌아와 남은 게이트로 간다
[RequireComponent(typeof(TankInfo))]
public class AllyMover : MonoBehaviour
{
    public Transform home;             // 방어 중심 (코어)
    public SpawnGate[] gates;          // 다리를 건널 때 거꾸로 따라갈 경로(게이트 route)
    public float routeHoldMax = 28.0f;     // 경로 위에서 멈추는 거리의 상한. 정지거리가 긴 Heavy도 다른 탱크와 같은 지점에서 멈춰 간격이 벌어지지 않는다
    public float routeEngageMargin = 8.0f; // 경로 위에서 적이 (stopDistance + 이 값) 안이면 멈춰서 사격한다
    public float routeGateRange = 25.0f;   // 적이 경로에서 이 거리 안에 있어야 그 경로로 출격한다
    public Transform post;             // 지정 시 이 지점에서 정지 (Heavy)
    public Transform guardPost;        // 지정 시 이 다리 앞에서 대기하다가 적이 보이면 교전한다
    public float guardRadius = 7.0f;   // 다리 앞에서 흩어져 서 있을 반경
    public float leashRadius = 80.0f;  // 중심에서 이 거리 밖으로는 가지 않는다
    public float roamRadius = 40.0f;   // 배회할 반경 (guardPost가 없을 때)
    public float detectRadius = 70.0f; // 이 안의 적 탱크를 발견한다
    public float speedScale = 1.0f;
    public bool assault = false;       // 적 게이트로 공격하러 간다 (Heavy 외 지상 아군)
    public float gateStandOff = 18.0f; // 게이트 스폰 지점에서 이만큼 떨어진 곳까지 간다
    public Vector2 islandMin = new Vector2(-205.0f, 82.0f); // 섬 범위 (x, z). 도로와 다리는 섬이 아니다
    public Vector2 islandMax = new Vector2(-75.0f, 182.0f);

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
    readonly AirDodger dodger = new AirDodger(); // 상대 UFO와 싸울 때의 회피 이동
    readonly RoadWeaver weaver = new RoadWeaver(); // MegaBall이 도로에서 좌우로 흔들리며 가는 이동
    Vector3 strafeOffset;                          // MegaBall이 적 주위에서 노리는 옆쪽 지점
    float nextStrafe;
    SpawnGate postGate;               // post가 지키는 다리의 게이트
    bool postReleased;                 // 게이트가 파괴되어 post를 떠났다
    SpawnGate routeGate;             // 지금 따라가는 경로의 게이트
    bool assaultRoute;                 // 게이트 공격 중 (적이 없어도 돌아오지 않는다)

    static bool Alive(SpawnGate g) => g != null && !g.IsDestroyed;

    static int spawnCounter;           // 생성 순서대로 선호 게이트를 번갈아 준다
    public static void ResetStatics() { spawnCounter = 0; } // 씬을 다시 불러올 때 호출한다
    int gatePref;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();
        gatePref = spawnCounter++;

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
        // 지키던 다리의 게이트가 파괴되면 자리를 떠나 다른 아군처럼 남은 게이트를 공격한다
        if (post != null)
        {
            if (postGate == null) postGate = NearestGateEntry(post.position);
            if (postGate != null && !Alive(postGate) && Alive(AssaultGate()))
            {
                post = null;
                postReleased = true; // 근처 적을 먼저 처치한 뒤 assault를 켠다
            }
        }
        if (postReleased && !assault && FindEnemyTank(detectRadius) == null) assault = true;

        if (post != null)
        {
            agent.stoppingDistance = 1.5f;
            GoTo(post.position);
            return;
        }

        // 다리를 건넌 MegaBall은 게이트 쪽 도로와 주변을 돌아다니며 적을 공격한다
        if (roaming) { RoamOutside(); return; }

        // 섬 안의 적이 먼저다 (코어 방어)
        Transform enemy = FindEnemyTank(detectRadius);
        if (enemy != null && OnIsland(enemy.position))
        {
            sortieGate = null;
            // 사거리 안이어도 지형/소품에 가려 쏠 수 없으면 계속 다가간다 (서로 못 보고 멈춰 서는 교착 방지)
            agent.stoppingDistance = HasSight(enemy) ? info.stopDistance : 3.0f;
            Vector3 goal = enemy.position;
            // MegaBall은 적에게 일직선으로 가지 않고 몇 초마다 옆쪽 지점을 바꿔 가며 움직인다
            if (info.kind == TankKind.MegaBall)
            {
                if (Time.time >= nextStrafe)
                {
                    nextStrafe = Time.time + Random.Range(2.0f, 4.0f);
                    Vector2 r = Random.insideUnitCircle * 18.0f;
                    strafeOffset = new Vector3(r.x, 0, r.y);
                }
                goal += strafeOffset;
            }
            GoTo(Leash(goal));
            wasMoving = true;
            return;
        }

        // 적이 도로나 다리 위에 있으면 그 경로로, 아니면 공격형은 살아 있는 게이트로 출격한다
        SpawnGate gate = enemy != null ? ClosestGate(enemy.position) : null;
        if (gate == null && assault) gate = AssaultGate();
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
        sortieGate = null;

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

    // 적이 지형/소품에 가리지 않고 보이는가. 탱크(TeamMember)는 시선을 막지 않는다
    bool HasSight(Transform enemy)
    {
        Vector3 from = transform.position + Vector3.up * 2.0f;
        Vector3 to = enemy.position + Vector3.up * 1.2f;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.1f) return true;
        foreach (var h in Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(enemy)) continue;
            if (h.collider.GetComponentInParent<TeamMember>() != null) continue;
            return false;
        }
        return true;
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }

    // 섬 위의 지점인가. NavMesh는 도로와 다리에도 있으므로 섬 범위로 판단하고, 다리 데크 위는 섬이 아니다
    bool OnIsland(Vector3 p)
    {
        if (p.x < islandMin.x || p.x > islandMax.x || p.z < islandMin.y || p.z > islandMax.y) return false;
        if (Physics.Raycast(p + Vector3.up * 3.0f, Vector3.down, out RaycastHit h, 6.0f, ~0, QueryTriggerInteraction.Ignore)
            && h.collider.name.Trim().StartsWith("Bridge")) return false;
        return true;
    }

    // 공격할 게이트. 아군마다 선호하는 게이트를 나눠 두 다리로 고르게 가고, 그쪽이 파괴됐으면 남은 쪽으로 간다
    SpawnGate AssaultGate()
    {
        if (gates == null || gates.Length == 0) return null;
        int pref = gatePref % gates.Length;
        for (int k = 0; k < gates.Length; k++)
        {
            var g = gates[(pref + k) % gates.Length];
            if (Alive(g) && g.route != null && g.route.Length >= 2) return g;
        }
        return null;
    }

    // 경로의 섬쪽 입구가 pos와 가장 가까운 게이트 (post가 지키는 다리의 게이트)
    SpawnGate NearestGateEntry(Vector3 pos)
    {
        if (gates == null) return null;
        SpawnGate best = null;
        float bestDist = float.MaxValue;
        foreach (var g in gates)
        {
            if (g == null || g.route == null || g.route.Length < 2) continue;
            float d = FlatDist(pos, g.route[g.route.Length - 1].position);
            if (d < bestDist) { bestDist = d; best = g; }
        }
        return best;
    }

    // pos와 가장 가까운 게이트 경로. routeGateRange 안에 있는 경로가 없으면 null
    SpawnGate ClosestGate(Vector3 pos)
    {
        if (gates == null) return null;
        SpawnGate best = null;
        float bestDist = routeGateRange;
        foreach (var g in gates)
        {
            if (!Alive(g) || g.route == null || g.route.Length < 2) continue;
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
        // 공격형은 경로 시작점에서 더 나아가 게이트 스폰 지점 앞(gateStandOff)까지 간다
        if (assault)
        {
            Vector3 first = gate.route[0].position;
            Vector3 spawn = gate.spawnPoint != null ? gate.spawnPoint.position : gate.transform.position;
            Vector3 d = spawn - first; d.y = 0;
            if (d.magnitude > gateStandOff + 2.0f) pts.Add(first + d.normalized * (d.magnitude - gateStandOff));
        }
        routePath = EnemyMover.SmoothPath(pts, 12.0f, 10);
        routeGate = gate;
        assaultRoute = assault;
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
                holding = FlatDist(transform.position, enemy.position) <= Mathf.Min(info.stopDistance + routeEngageMargin, routeHoldMax) && HasSight(enemy);
            }
            // 공격형은 게이트가 파괴되어야 돌아온다. 그 외에는 적이 안 보이면 돌아온다
            retreating = assaultRoute ? !Alive(routeGate) : (enemy == null && Time.time - lastEnemySeen > 2.5f);
        }
        if (holding) return; // 사거리 안에 적이 있으면 멈춰서 사격 (포탑은 AITurretCtrl이 돌린다)

        if (retreating)
        {
            routeIdx = Mathf.Min(routeIdx, routePath.Count - 1);
            if (routeIdx < 0) { EndRoute(); return; }
        }
        else if (routeIdx >= routePath.Count)
        {
            // MegaBall은 게이트 앞에서 멈추지 않고 NavMesh로 내려가 주변을 돌아다닌다
            if (info.kind == TankKind.MegaBall && assaultRoute && Alive(routeGate)) { BeginRoam(); return; }
            return; // 게이트 앞까지 갔다: 게이트와 나오는 적을 쏜다 (포탑은 AITurretCtrl이 돌린다)
        }

        Vector3 target = routePath[routeIdx];
        Vector3 to = target - transform.position;
        float step = info.moveSpeed * speedScale * Time.deltaTime;
        // 같은 방향으로 가는 앞 탱크와 겹치지 않도록 뒤따르는 쪽이 멈춰서 기다린다
        if (IsBlockedAhead(new Vector3(to.x, 0, to.z))) return;
        if (to.magnitude <= step)
        {
            transform.position = target;
            routeIdx += retreating ? -1 : 1;
            return;
        }

        transform.position += to.normalized * step;
        if (info.kind == TankKind.MegaBall) weaver.Apply(transform, new Vector3(to.x, 0, to.z)); // 줄지어 가는 탱크 옆에서 좌우로 움직인다
        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 240.0f * Time.deltaTime);
    }

    public float followGap = 5.0f; // 경로 위에서 앞 탱크와 이 거리(탱크 크기 배율 적용) 안이면 멈춘다

    // 진행 방향 앞쪽(약 60도 안)에 같은 방향으로 가는 다른 탱크가 gap 안에 있으면 막힌 것으로 본다.
    // 마주 오는 탱크는 세지 않아(복귀 중인 아군 등) 서로 막혀 멈추는 일이 없다
    bool IsBlockedAhead(Vector3 flatDir)
    {
        if (flatDir.sqrMagnitude < 0.0001f) return false;
        flatDir.Normalize();
        foreach (var other in TeamMember.All)
        {
            if (other.isStructure || other.gameObject == gameObject) continue;
            var oi = other.GetComponent<TankInfo>();
            if (oi != null && oi.flying) continue;

            Vector3 d = other.transform.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude < 0.25f) continue; // 같은 자리에 겹쳐 스폰된 탱크는 서로 막지 않는다
            float gap = followGap * Mathf.Max(transform.lossyScale.x, other.transform.lossyScale.x);
            if (d.sqrMagnitude > gap * gap) continue;
            if (Vector3.Dot(flatDir, d.normalized) <= 0.5f) continue;
            if (Vector3.Dot(flatDir, other.transform.forward) < -0.2f) continue; // 마주 보고 오는 탱크
            return true;
        }
        return false;
    }

    // 다리를 건넌 MegaBall의 자유 배회 (게이트 쪽 도로와 주변)
    public float outsideRoamRadius = 60.0f; // 게이트 앞 경로 끝에서 이 거리 안을 돌아다닌다
    bool roaming;
    Vector3 roamCenter;

    void BeginRoam()
    {
        roamCenter = routePath[routePath.Count - 1];
        roaming = true;
        onRoute = false;
        wasMoving = false;
        idleUntil = 0;
        if (NavUtil.SampleNearest(transform.position, 20.0f, out NavMeshHit hit))
            transform.position = hit.position;
        agent.enabled = true;
    }

    void RoamOutside()
    {
        // 게이트가 파괴되면 가장 가까운 경로 지점으로 돌아가 경로를 거꾸로 따라 섬으로 복귀한다
        if (!Alive(routeGate))
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < routePath.Count; i++)
            {
                float d = FlatDist(transform.position, routePath[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (bestD < 4.0f)
            {
                roaming = false;
                agent.enabled = false;
                onRoute = true;
                retreating = true;
                holding = false;
                routeIdx = best;
                return;
            }
            agent.stoppingDistance = 1.0f;
            GoTo(routePath[best]);
            return;
        }

        // 섬 밖(도로, 다리)의 적 탱크를 쫓는다. 섬 안의 적은 다른 아군이 맡는다
        Transform enemy = FindEnemyTank(detectRadius);
        if (enemy != null && !OnIsland(enemy.position))
        {
            agent.stoppingDistance = HasSight(enemy) ? info.stopDistance : 3.0f;
            if (Time.time >= nextStrafe)
            {
                nextStrafe = Time.time + Random.Range(2.0f, 4.0f);
                Vector2 r = Random.insideUnitCircle * 18.0f;
                strafeOffset = new Vector3(r.x, 0, r.y);
            }
            GoTo(ClampToRoam(enemy.position + strafeOffset));
            wasMoving = true;
            return;
        }

        // 적이 없으면 게이트 주변의 무작위 지점을 돌아다닌다
        agent.stoppingDistance = 1.0f;
        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.0f;
        if (wasMoving && arrived)
        {
            wasMoving = false;
            idleUntil = Time.time + Random.Range(0.5f, 2.0f);
        }
        if (!wasMoving && Time.time >= idleUntil)
        {
            Vector2 r = Random.insideUnitCircle * outsideRoamRadius;
            GoTo(roamCenter + new Vector3(r.x, 0, r.y));
            wasMoving = true;
        }
    }

    Vector3 ClampToRoam(Vector3 p)
    {
        Vector3 d = p - roamCenter; d.y = 0;
        return roamCenter + Vector3.ClampMagnitude(d, outsideRoamRadius * 1.5f) + Vector3.up * (p.y - roamCenter.y);
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
    Vector3 flyVel; // 비행 중 부드러운 가감속용 속도

    void FlyUpdate()
    {
        // 적 탱크 우선: 맵 어디에 있든 가장 가까운 적 탱크를 쫓는다. 탱크가 하나도 없을 때만 게이트를 공격한다
        Transform target = FindEnemyTank(100000.0f);
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
        // 목표(적 탱크나 게이트) 위 flyHeight 높이를 유지한다. 상대 UFO는 같은 높이로 맞춘다
        bool airTarget = FlyUtil.IsAir(target);

        // 상대 UFO를 만나면 멈추지 않고 포탄을 피하며 상하좌우로 움직이면서 공격한다
        if (airTarget)
        {
            Vector3 vel = dodger.Step(transform.position, target, me.team, info.moveSpeed * speedScale * 0.8f);
            vel += FlyUtil.Separation(transform, me.team, 10.0f) * info.moveSpeed * speedScale * 0.5f;
            transform.position = FlyUtil.ClampAboveGround(transform.position + vel * Time.deltaTime, 4.0f);
            Vector3 face = target.position - transform.position; face.y = 0;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 180.0f * Time.deltaTime);
            return;
        }

        float altitude = target.position.y + info.flyHeight;
        var p = transform.position;
        p.y = Mathf.MoveTowards(p.y, altitude, 10.0f * Time.deltaTime);

        Vector3 desiredFly = Vector3.zero;
        if (flat.magnitude > info.stopDistance)
        {
            Vector3 dir = flat.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180.0f * Time.deltaTime);
            desiredFly = dir * info.moveSpeed * speedScale;
        }
        // 같은 목표를 쫓는 아군 UFO끼리 겹치지 않게 서로 밀어낸다
        desiredFly += FlyUtil.Separation(transform, me.team, 10.0f) * info.moveSpeed * speedScale;
        flyVel = Vector3.MoveTowards(flyVel, Vector3.ClampMagnitude(desiredFly, info.moveSpeed * speedScale), 6.0f * Time.deltaTime);
        p += flyVel * Time.deltaTime;
        transform.position = FlyUtil.ClampAboveGround(p, 4.0f);
    }
}
