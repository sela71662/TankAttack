using UnityEngine;
using UnityEngine.AI;

// 아군 탱크 이동. 건물 주변(방어 범위) 안에서 돌아다니다가 적 탱크가 보이면 다가가 교전한다.
// post가 지정되면(Heavy) 그 지점으로 가서 멈춘 채 사거리 안의 적에게만 사격한다.
// 방어 범위(leashRadius) 밖으로는 나가지 않는다
[RequireComponent(typeof(TankInfo))]
public class AllyMover : MonoBehaviour
{
    public Transform home;             // 방어 중심 (코어)
    public Transform post;             // 지정 시 이 지점에서 정지
    public float leashRadius = 80.0f;  // 중심에서 이 거리 밖으로는 가지 않는다
    public float roamRadius = 40.0f;   // 배회할 반경
    public float detectRadius = 70.0f; // 이 안의 적 탱크를 발견한다
    public float speedScale = 1.0f;

    NavMeshAgent agent;
    TankInfo info;
    TeamMember me;
    float nextThink;
    float idleUntil;
    bool wasMoving;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();

        if (info.flying) return;

        // NavMesh 위로 위치를 맞춘 뒤 에이전트를 만든다
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
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
            agent.stoppingDistance = info.stopDistance;
            GoTo(Leash(enemy.position));
            wasMoving = true;
            return;
        }

        // 적이 없으면 방어 범위 안을 돌아다닌다. 도착하면 잠시 쉬었다가 다음 지점을 고른다
        agent.stoppingDistance = 1.0f;
        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.0f;
        if (wasMoving && arrived)
        {
            wasMoving = false;
            idleUntil = Time.time + Random.Range(2.0f, 5.0f);
        }
        if (!wasMoving && Time.time >= idleUntil)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            GoTo(Leash(HomePos + new Vector3(r.x, 0, r.y)));
            wasMoving = true;
        }
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
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 20.0f, NavMesh.AllAreas))
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
