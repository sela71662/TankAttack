using UnityEngine;

// 섬에 놓인 고정 대포(canon_001). 아군 편이며 범위 안의 가장 가까운 적 탱크를 향해 돌아 포탄을 쏜다
public class AutoCannon : MonoBehaviour
{
    public Transform barrel;                 // 돌아갈 대포 몸체 (canon 자식)
    public GameObject projectilePrefab;      // 발사할 포탄 (Proj_Armor)
    public float range = 50.0f;             // 받침 중심에서의 수평 사정거리
    public float cooldown = 1.5f;
    public float turnSpeed = 180.0f;         // 초당 회전 각도
    public Vector3 muzzleLocal = new Vector3(0, 0, 0.56f); // barrel 기준 포구 위치 (메시 길이 방향이 +Z)

    float nextFire;
    Quaternion restRot;
    bool restInit;
    AudioClip fireSfx;

    void Awake()
    {
        fireSfx = Resources.Load<AudioClip>("CannonFire");
    }

    void Update()
    {
        if (barrel == null || projectilePrefab == null) return;

        if (!restInit)
        {
            restRot = barrel.rotation;
            restInit = true;
        }

        Transform target = FindTarget();
        if (target == null)
        {
            // 적이 없으면 처음 방향으로 돌아간다
            barrel.rotation = Quaternion.RotateTowards(barrel.rotation, restRot, turnSpeed * 0.5f * Time.deltaTime);
            return;
        }

        // 조준 방향은 포구가 아니라 회전 중심(barrel)에서 잡는다. 포구는 몸체와 같이 돌아서 가까운 적일수록 방향이 흔들린다
        Vector3 aim = LeadPoint(target);
        Vector3 dir = aim - barrel.position;
        if (dir.sqrMagnitude < 0.01f) return;

        var want = Quaternion.LookRotation(dir);
        barrel.rotation = Quaternion.RotateTowards(barrel.rotation, want, turnSpeed * Time.deltaTime);

        // 포구가 목표를 거의 향했을 때만 쏜다
        if (Time.time < nextFire || Quaternion.Angle(barrel.rotation, want) > 5.0f) return;
        nextFire = Time.time + cooldown;

        Vector3 muzzle = barrel.TransformPoint(muzzleLocal);
        if (fireSfx != null) AudioSource.PlayClipAtPoint(fireSfx, muzzle, 1.0f);
        var go = Instantiate(projectilePrefab, muzzle, barrel.rotation);
        var p = go.GetComponent<Projectile>();
        if (p != null) p.Init(TeamConfig.playerTeam);
    }

    // 이동하는 적은 포탄이 도착할 시간만큼 앞쪽을 겨눈다 (목표 속도는 프레임 간 위치 변화로 추정)
    Transform velTarget;
    Vector3 lastTargetPos, targetVel;
    float projectileSpeed;

    Vector3 LeadPoint(Transform target)
    {
        Vector3 p = target.position;
        if (target != velTarget)
        {
            velTarget = target;
            lastTargetPos = p;
            targetVel = Vector3.zero;
        }
        else if (Time.deltaTime > 0.0001f)
        {
            Vector3 v = (p - lastTargetPos) / Time.deltaTime;
            lastTargetPos = p;
            targetVel = Vector3.Lerp(targetVel, v, Mathf.Clamp01(5.0f * Time.deltaTime));
        }

        if (projectileSpeed <= 0)
        {
            var proj = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
            projectileSpeed = proj != null ? proj.speed : 0;
        }
        Vector3 aim = p + Vector3.up * 1.0f;
        if (projectileSpeed <= 0) return aim;

        // 도착 시간을 두 번 다시 계산해 정확도를 높인다
        Vector3 flatVel = new Vector3(targetVel.x, 0, targetVel.z);
        Vector3 lead = aim;
        for (int i = 0; i < 2; i++)
        {
            float time = Vector3.Distance(barrel.position, lead) / projectileSpeed;
            lead = aim + flatVel * time;
        }
        return lead;
    }

    // 범위 안의 가장 가까운 적 탱크 (구조물 제외)
    Transform FindTarget()
    {
        Transform best = null;
        float bestDist = range * range;
        Vector3 c = transform.position;
        foreach (var t in TeamMember.All)
        {
            if (t == null || t.isStructure || TeamConfig.IsAlly(t.team)) continue;
            // 다리를 다 건너 섬에 들어온 적은 쏘지 않는다
            var mover = t.GetComponent<EnemyMover>();
            if (mover != null && mover.CrossedBridge) continue;
            Vector3 d = t.transform.position - c; d.y = 0;
            float sq = d.sqrMagnitude;
            if (sq < bestDist) { bestDist = sq; best = t.transform; }
        }
        return best;
    }
}
