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
    public float aggroRadius = 35.0f; // 섬 안에서 이 거리 안의 아군 탱크를 우선 추격한다
    public float turnSpeed = 120.0f;  // 도/초
    public float followGap = 4.5f;    // 앞 탱크와 이 거리(탱크 크기 배율 적용) 안이면 멈춘다

    NavMeshAgent agent;
    TankInfo info;
    TeamMember me;
    int routeIndex;
    float nextRetargetTime;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();
    }

    void Update()
    {
        if (route != null && routeIndex < route.Length)
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

    void FollowRoute()
    {
        Vector3 target = route[routeIndex].position;
        Vector3 to = target - transform.position;
        float step = info.moveSpeed * speedScale * Time.deltaTime;

        // 앞에 다른 탱크가 있으면 겹치지 않도록 멈춰서 기다린다
        if (IsBlockedAhead(new Vector3(to.x, 0, to.z)))
            return;

        if (to.magnitude <= step)
        {
            transform.position = target;
            routeIndex++;
            return;
        }

        transform.position += to.normalized * step;

        Vector3 flat = new Vector3(to.x, 0, to.z);
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
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
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20.0f, NavMesh.AllAreas))
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

    // 가까운 아군 탱크가 있으면 그쪽으로, 없으면 코어 쪽으로
    void MoveFreely()
    {
        if (agent == null || !agent.isOnNavMesh || Time.time < nextRetargetTime) return;
        nextRetargetTime = Time.time + 0.5f;

        Transform target = FindNearbyEnemyTank();
        if (target == null) target = approach;
        if (target == null) return;

        agent.stoppingDistance = info.stopDistance;
        // 목표가 NavMesh 밖(건물 안쪽 등)이어도 가장 가까운 이동 가능 지점으로 보낸다
        if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    Transform FindNearbyEnemyTank()
    {
        Transform best = null;
        float bestDist = aggroRadius * aggroRadius;
        foreach (var t in FindObjectsByType<TeamMember>())
        {
            if (t.isStructure || !me.IsEnemy(t)) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t.transform; }
        }
        return best;
    }
}
