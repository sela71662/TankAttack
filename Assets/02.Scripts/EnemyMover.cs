using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 적 지상 탱크 이동.
// 1) 도로와 다리는 NavMesh 없이 게이트가 정한 경로(웨이포인트)를 그대로 따라간다 (다리에 틈이 있어 NavMesh가 끊긴다)
// 2) 경로가 끝나 섬에 도착하면 NavMeshAgent를 켜서 아군 탱크나 코어를 향해 자유롭게 이동한다
[RequireComponent(typeof(TankInfo))]
public class EnemyMover : MonoBehaviour
{
    public Transform[] route;      // 고정 경로 (도로 -> 다리 -> 섬 입구)
    public Transform approach;     // 코어 근처에서 NavMesh가 닿는 지점 (코어는 건물 위에 떠 있다)
    public float speedScale = 1.0f;
    public float aggroRadius = 150.0f; // 아군 탱크 우선형(Light)이 섬 안에서 아군 탱크를 쫓는 거리. 코어 우선형(Original/Heavy/MegaBall)은 쓰지 않는다
    public float turnSpeed = 120.0f;  // 도/초
    public float followGap = 5.0f;    // 앞 탱크와 이 거리(탱크 크기 배율 적용) 안이면 멈춘다
    public float cornerCut = 12.0f;   // 도로 코너를 둥글게 깎을 최대 길이(m). 클수록 완만하게 돈다
    public float routeTurnSpeed = 240.0f; // 경로를 따라갈 때 차체 회전 속도(도/초). 곡선을 놓치지 않을 만큼 빠르게

    NavMeshAgent agent;
    TankInfo info;
    TeamMember me;
    List<Vector3> path;               // 웨이포인트를 곡선으로 다듬은 실제 주행 경로
    int routeIndex;
    float nextRetargetTime;

    // 경로(도로와 다리)를 다 지나 섬에 도착했는가
    public bool CrossedBridge => path != null && routeIndex >= path.Count;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();
        wander = info.kind == TankKind.MegaBall; // MegaBall은 일직선으로 가지 않고 이리저리 움직이며 공격한다
    }

    // MegaBall의 자유 이동
    public bool wander;
    public float wanderRadius = 40.0f;     // 섬에서 코어 주변으로 돌아다니는 반경(m)
    readonly RoadWeaver weaver = new RoadWeaver(); // 도로에서 경로 좌우로 흔들리며 간다
    float nextWanderTime;

    void Update()
    {
        if (path == null && route != null && route.Length > 0) BuildPath();

        if (path != null && routeIndex < path.Count)
        {
            FollowRoute();
            return;
        }

        if (agent == null)
        {
            if (!BeginFreeMove()) return;
        }
        MoveFreely();
    }

    // 현재 위치에서 시작해 웨이포인트를 지나는 경로를 만들고, 코너를 둥글게 깎는다
    void BuildPath()
    {
        var pts = new List<Vector3> { transform.position };
        foreach (var w in route) pts.Add(w.position);
        path = SmoothPath(pts, cornerCut, 10);
    }

    // 꺾이는 점마다 앞뒤로 cut 만큼 떨어진 두 점 사이를 2차 베지어 곡선(꺾이는 점이 제어점)으로 잇는다.
    // cut은 이웃 구간 길이의 절반을 넘지 않으므로, 구간이 짧은 다리 입구 같은 곳에서는 경로에서 많이 벗어나지 않는다
    public static List<Vector3> SmoothPath(IList<Vector3> pts, float cut, int samples)
    {
        var result = new List<Vector3>();
        if (pts.Count < 3) { result.AddRange(pts); return result; }

        result.Add(pts[0]);
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Vector3 p = pts[i];
            Vector3 inDir = p - pts[i - 1];
            Vector3 outDir = pts[i + 1] - p;
            float inLen = inDir.magnitude, outLen = outDir.magnitude;
            if (inLen < 0.01f || outLen < 0.01f) continue;
            inDir /= inLen; outDir /= outLen;

            // 거의 직선이면 그대로 둔다
            if (Vector3.Angle(inDir, outDir) < 3.0f) { result.Add(p); continue; }

            float c = Mathf.Min(cut, inLen * 0.5f, outLen * 0.5f);
            Vector3 a = p - inDir * c;
            Vector3 b = p + outDir * c;
            for (int s = 0; s <= samples; s++)
            {
                float t = s / (float)samples;
                // 2차 베지어: (1-t)^2 a + 2(1-t)t p + t^2 b
                result.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * p + t * t * b);
            }
        }
        result.Add(pts[pts.Count - 1]);
        return result;
    }

    void FollowRoute()
    {
        Vector3 target = path[routeIndex];
        Vector3 to = target - transform.position;
        float step = info.moveSpeed * speedScale * Time.deltaTime;

        // 앞에 다른 탱크가 있으면 겹치지 않도록 멈춰서 기다린다
        // (MegaBall은 옆으로 비켜 있으면 앞차를 추월한다)
        // 앞쪽에 보이는 상대 탱크가 사거리 안에 들어오면 더 다가가지 않고 멈춰서 사격한다 (Light는 포가 없어 계속 접근)
        if (info.kind != TankKind.Light && EngagedAhead(new Vector3(to.x, 0, to.z)))
            return;
        bool overtaking = wander && Mathf.Abs(weaver.Lateral) > 1.5f;
        if (!overtaking && IsBlockedAhead(new Vector3(to.x, 0, to.z)))
            return;

        if (to.magnitude <= step)
        {
            transform.position = target;
            routeIndex++;
            return;
        }

        transform.position += to.normalized * step;
        if (wander) weaver.Apply(transform, new Vector3(to.x, 0, to.z));

        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), routeTurnSpeed * Time.deltaTime);
    }

    public float routeHoldMax = 28.0f;     // 경로 위에서 멈추는 거리의 상한 (종류와 상관없이 같은 지점에서 멈춘다)
    public float routeEngageMargin = 8.0f; // 경로 위에서 상대 탱크가 (stopDistance + 이 값) 안이면 멈춰서 사격한다
    float nextEngageCheck;
    bool engaged;

    bool EngagedAhead(Vector3 flatDir)
    {
        if (Time.time < nextEngageCheck) return engaged;
        nextEngageCheck = Time.time + 0.3f;
        engaged = false;
        if (flatDir.sqrMagnitude < 0.0001f) return false;
        flatDir.Normalize();

        float reach = Mathf.Min(info.stopDistance + routeEngageMargin, routeHoldMax);
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !me.IsEnemy(t)) continue;
            var ti = t.GetComponent<TankInfo>();
            if (ti != null && ti.flying) continue;
            Vector3 d = t.transform.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > reach * reach || Vector3.Dot(flatDir, d.normalized) < 0.0f) continue;

            // 지형이나 소품에 가려 서로 못 보면 멈추지 않는다 (교착 방지). 탱크는 시선을 막지 않는다
            Vector3 from = transform.position + Vector3.up * 1.5f;
            Vector3 to = t.transform.position + Vector3.up * 1.2f;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            bool blocked = false;
            foreach (var h in Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(t.transform)) continue;
                if (h.collider.GetComponentInParent<TeamMember>() != null) continue;
                blocked = true;
                break;
            }
            if (!blocked) { engaged = true; break; }
        }
        return engaged;
    }

    // 진행 방향 앞쪽(약 60도 안)에 다른 탱크가 gap 거리 안에 있으면 막힌 것으로 본다.
    // 아군/적군 모두 해당하고, 뒤따르는 탱크만 멈추므로 서로 마주 보고 영영 멈추는 일은 없다
    bool IsBlockedAhead(Vector3 flatDir)
    {
        if (flatDir.sqrMagnitude < 0.0001f) return false;
        flatDir.Normalize();

        foreach (var other in TeamMember.All)
        {
            if (other.isStructure || other.gameObject == gameObject) continue;

            Vector3 d = other.transform.position - transform.position;
            d.y = 0;
            // 같은 자리에 겹쳐 스폰된 탱크는 서로 막지 않는다 (먼저 빠져나가게 둔다)
            if (d.sqrMagnitude < 0.25f) continue;

            float gap = followGap * Mathf.Max(transform.lossyScale.x, other.transform.lossyScale.x);
            if (d.sqrMagnitude > gap * gap) continue;

            if (Vector3.Dot(flatDir, d.normalized) > 0.5f)
                return true;
        }
        return false;
    }

    // 섬에 도착: 가장 가까운 NavMesh 위로 옮기고 에이전트를 만든다
    bool BeginFreeMove()
    {
        if (!NavUtil.SampleNearest(transform.position, 20.0f, out NavMeshHit hit))
        {
            Debug.LogWarning($"{name}: 섬 입구 근처에 NavMesh가 없다", this);
            enabled = false;
            return false;
        }

        transform.position = hit.position;
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 1.2f;
        agent.height = 2.6f;
        agent.speed = info.moveSpeed * speedScale;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12.0f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        return agent.isOnNavMesh;
    }

    // 코어 우선형은 곧장 코어로, 아군 탱크 우선형은 가까운 아군 탱크가 있으면 그쪽으로(없으면 코어)
    void MoveFreely()
    {
        if (agent == null || !agent.isOnNavMesh || Time.time < nextRetargetTime) return;
        nextRetargetTime = Time.time + 0.5f;

        // MegaBall: 코어 주변을 이리저리 돌아다닌다. 몇 초마다 코어 근처의 무작위 지점으로 목적지를 바꾼다
        if (wander && approach != null)
        {
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.0f;
            if (Time.time >= nextWanderTime || arrived)
            {
                nextWanderTime = Time.time + Random.Range(2.5f, 5.0f);
                Vector2 r = Random.insideUnitCircle * wanderRadius;
                Vector3 goal = approach.position + new Vector3(r.x, 0, r.y);
                agent.stoppingDistance = 1.0f;
                if (NavUtil.SampleNearest(goal, 15.0f, out NavMeshHit wh)) agent.SetDestination(wh.position);
            }
            return;
        }

        // 아군 탱크 우선형은 한 번 정한 목표가 살아 있는 동안 바꾸지 않는다 (목표가 번갈아 바뀌며 흔들리는 것 방지)
        Transform target = null;
        if (!info.PrefersCore)
        {
            if (huntTarget == null || !huntTarget.gameObject.activeInHierarchy) huntTarget = FindNearbyEnemyTank();
            target = huntTarget;
        }
        if (target == null) target = approach;
        if (target == null) return;

        agent.stoppingDistance = info.stopDistance;
        // 목표가 NavMesh 밖(건물 안쪽 등)이어도 가장 가까운 이동 가능 지점으로 보낸다
        if (NavUtil.SampleNearest(target.position, 15.0f, out NavMeshHit hit))
            agent.SetDestination(hit.position);
    }

    Transform huntTarget;

    Transform FindNearbyEnemyTank()
    {
        Transform best = null;
        float bestDist = aggroRadius * aggroRadius;
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !me.IsEnemy(t)) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t.transform; }
        }
        return best;
    }
}
