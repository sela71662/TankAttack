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
        // 코어보다 flyHeight만큼 높은 고도로 올려 놓는다
        if (core != null)
        {
            var p = transform.position;
            p.y = core.position.y + info.flyHeight;
            transform.position = p;
        }
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

        Vector3 flat = target.position - transform.position;
        flat.y = 0;
        if (flat.magnitude <= info.stopDistance) return; // 사거리 안에 들어오면 멈춰서 공격

        Vector3 dir = flat.normalized;
        transform.position += dir * info.moveSpeed * speedScale * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180.0f * Time.deltaTime);
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
