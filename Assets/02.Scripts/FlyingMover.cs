using UnityEngine;

// 적 UFO 이동. 지형과 호수를 무시하고 가장 가까운 아군 탱크(없으면 코어)를 향해 직선으로 날아간다
[RequireComponent(typeof(TankInfo))]
public class FlyingMover : MonoBehaviour
{
    public Transform core;
    public float speedScale = 1.0f;

    TankInfo info;
    TeamMember me;
    Transform target;
    float nextSearchTime;

    void Awake()
    {
        info = GetComponent<TankInfo>();
        me = GetComponent<TeamMember>();
    }

    void Start()
    {
        // 스폰 지면보다 flyHeight만큼 높은 고도로 올려 놓는다 (플레이어 시야에 들어오도록 낮게 난다)
        var p = transform.position;
        p.y += info.flyHeight;
        transform.position = p;
    }

    void Update()
    {
        if (target == null || Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + 0.5f;
            target = FindAllyTank();
            if (target == null) target = core;
        }
        if (target == null) return;

        // 목표(탱크) 머리 위 flyHeight 높이를 유지한다. 코어처럼 높이 떠 있는 목표는 지면 기준으로 난다
        bool airTarget = target != core && FlyUtil.IsAir(target);
        float baseY = target == core ? FlyUtil.GroundY(transform.position, target.position.y) : target.position.y;
        // 상대 UFO를 만나면 멈추지 않고 포탄을 피하며 상하좌우로 움직이면서 공격한다
        if (airTarget)
        {
            Vector3 vel = dodger.Step(transform.position, target, me.team, info.moveSpeed * speedScale * 1.2f);
            transform.position += vel * Time.deltaTime;
            Vector3 face = target.position - transform.position; face.y = 0;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 180.0f * Time.deltaTime);
            return;
        }

        var pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, baseY + info.flyHeight, 8.0f * Time.deltaTime);
        transform.position = pos;

        Vector3 flat = target.position - transform.position;
        flat.y = 0;

        // 사거리 안에 들어와 목표가 보이면 멈춰서 공격한다. 가려져 있으면 보일 때까지 더 다가간다 (서로 못 보고 멈추는 교착 방지)
        if (Time.time >= nextSightTime)
        {
            nextSightTime = Time.time + 0.3f;
            hasSight = HasSight(target);
        }
        if (flat.magnitude <= info.stopDistance && (hasSight || flat.magnitude < 4.0f)) return;

        Vector3 dir = flat.normalized;
        transform.position += dir * info.moveSpeed * speedScale * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180.0f * Time.deltaTime);
    }

    readonly AirDodger dodger = new AirDodger(); // 상대 UFO와 싸울 때의 회피 이동
    float nextSightTime;
    bool hasSight = true;

    // 목표가 지형/소품에 가리지 않고 보이는가. 탱크(TeamMember)는 시선을 막지 않는다
    bool HasSight(Transform t)
    {
        Vector3 from = transform.position;
        Vector3 to = t.position + Vector3.up * 1.2f;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.1f) return true;
        foreach (var h in Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(t)) continue;
            if (h.collider.GetComponentInParent<TeamMember>() != null) continue;
            return false;
        }
        return true;
    }

    // 거리 제한 없이 가장 가까운 아군 탱크
    Transform FindAllyTank()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !me.IsEnemy(t)) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t.transform; }
        }
        return best;
    }
}
