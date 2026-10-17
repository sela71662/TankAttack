using UnityEngine;

// 가장 가까운 적 팀 탱크를 찾아 조준하고 발사한다 (Tank_Alternative_Model 외 포를 쏘는 탱크용).
// 지형이나 소품에 가려진 적은 고르지 않고, 직선으로 날아가는 포탄(Armor, Shell)은 적의 이동을 예측해 앞쪽을 겨눈다
[RequireComponent(typeof(TurretAimer), typeof(TankShooter), typeof(TeamMember))]
public class AITurretCtrl : MonoBehaviour
{
    public float range = 60.0f;         // 사거리
    public float aimTolerance = 3.0f;   // 이 각도(도) 안으로 들어오면 발사
    public float targetHeight = 1.2f;   // 목표 바닥에서 조준할 높이
    public float retargetInterval = 0.5f;
    public bool attackStructures = false; // 적 탱크가 없을 때 게이트 같은 구조물을 공격한다 (UFO)
    public bool leadTarget = true;      // 이동하는 적의 앞쪽을 겨눈다

    TurretAimer aimer;
    TankShooter shooter;
    TeamMember me;
    TankInfo info;
    TeamMember target;
    float nextSearchTime;
    float projectileSpeed;              // 0이면 예측 조준을 하지 않는다 (호밍, 튕기는 포탄)

    // 목표 속도 추정
    Vector3 lastTargetPos;
    Vector3 targetVel;
    TeamMember velTarget;

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
        me = GetComponent<TeamMember>();
        info = GetComponent<TankInfo>();

        var proj = shooter.projectilePrefab != null ? shooter.projectilePrefab.GetComponent<Projectile>() : null;
        if (proj != null && (proj.kind == ProjectileKind.Armor || proj.kind == ProjectileKind.Shell))
            projectileSpeed = proj.speed;
    }

    // 적 Original/Heavy/MegaBall은 사거리 안에 코어가 있으면 탱크보다 코어를 먼저 쏜다
    bool CoreFirst => info != null && info.PrefersCore && !TeamConfig.IsAlly(me.team);

    Vector3 FireOrigin => shooter.firePos != null ? shooter.firePos.position : aimer.HubWorld;

    void Update()
    {
        if (target == null || Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + retargetInterval;
            target = FindNearestEnemy();
        }
        if (target == null) return;

        Vector3 aim = AimPoint(target);
        aimer.AimAt(aim);

        // 발사 직전에도 가려졌는지 다시 본다 (가려진 채로 쏘면 포탄이 지형에 터진다)
        if (aimer.IsAimed(aimTolerance) && MuzzleClear() && HasLineOfFire(FireOrigin, aim))
            shooter.TryFire();
    }

    // 겨눌 기준점. 구조물은 콜라이더 위쪽(게이트는 아치라서 가운데를 쏘면 빈 곳으로 빠진다), 탱크는 바닥에서 targetHeight 위
    Vector3 TargetPoint(TeamMember t)
    {
        if (t.isStructure)
        {
            var col = t.GetComponentInChildren<Collider>();
            if (col != null && col.enabled)
            {
                var b = col.bounds;
                return b.center + Vector3.up * (b.extents.y * 0.5f);
            }
        }
        return t.transform.position + Vector3.up * targetHeight;
    }

    Vector3 AimPoint(TeamMember t)
    {
        Vector3 p = TargetPoint(t);

        // 목표 속도: 프레임 사이 이동량을 부드럽게 평균한다
        if (velTarget != t)
        {
            velTarget = t;
            targetVel = Vector3.zero;
        }
        else if (Time.deltaTime > 0)
        {
            Vector3 v = (t.transform.position - lastTargetPos) / Time.deltaTime;
            targetVel = Vector3.Lerp(targetVel, v, Mathf.Clamp01(5.0f * Time.deltaTime));
        }
        lastTargetPos = t.transform.position;

        if (!leadTarget || projectileSpeed <= 0 || t.isStructure) return p;

        // 포탄이 닿는 시간만큼 앞을 겨눈다 (두 번 반복하면 충분히 맞는다)
        Vector3 lead = p;
        for (int i = 0; i < 2; i++)
        {
            float time = Vector3.Distance(FireOrigin, lead) / projectileSpeed;
            lead = p + new Vector3(targetVel.x, 0, targetVel.z) * time;
        }
        return lead;
    }

    // 포구가 지형이나 소품에 묻혀 있지 않은가 (묻힌 채로 쏘면 포탄이 그 자리에서 터진다. 레이캐스트는 콜라이더 안에서 시작하면 그 콜라이더를 못 찾는다)
    bool MuzzleClear()
    {
        foreach (var c in Physics.OverlapSphere(FireOrigin, 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(transform)) continue;
            var tm = c.GetComponentInParent<TeamMember>();
            if (tm == null) return false;
        }
        return true;
    }

    // 발사 위치에서 목표 지점까지 지형/소품/물에 막히지 않는가. 아군과 포탄은 통과하므로 무시한다
    bool HasLineOfFire(Vector3 from, Vector3 point)
    {
        Vector3 dir = point - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        float nearest = float.MaxValue;
        bool blocked = false;
        foreach (var h in Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= nearest) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            var tm = h.collider.GetComponentInParent<TeamMember>();
            if (tm != null && !me.IsEnemy(tm)) continue;   // 아군은 포탄이 통과한다
            nearest = h.distance;
            blocked = tm == null;                          // 적(목표 포함)에 먼저 닿으면 쏴도 된다
        }
        return !blocked;
    }

    bool CanAttackStructures()
    {
        if (TeamConfig.IsAlly(me.team)) return true;
        var mover = GetComponent<EnemyMover>();
        return mover == null || mover.CrossedBridge;   // 이동기가 없는 UFO 등은 제한하지 않는다
    }

    // 보이는 적 탱크를 우선하고, 없을 때만 구조물을 고른다
    TeamMember FindNearestEnemy()
    {
        TeamMember bestTank = null, bestStruct = null;
        float tankDist = range * range, structDist = range * range;
        foreach (var t in TeamMember.All)
        {
            if (!me.IsEnemy(t)) continue;
            // 지상 적은 다리를 다 건너 섬에 들어오기 전에는 코어, 건물 같은 구조물을 공격하지 못한다
            if (t.isStructure && !CanAttackStructures()) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (t.isStructure ? d >= structDist : d >= tankDist) continue;
            // 목표 고르기는 포탑 회전 중심에서 본다 (포구는 아직 다른 쪽을 향하고 있을 수 있다)
            if (!HasLineOfFire(aimer.HubWorld, TargetPoint(t))) continue;
            if (t.isStructure) { structDist = d; bestStruct = t; }
            else { tankDist = d; bestTank = t; }
        }
        if (CoreFirst && bestStruct != null) return bestStruct;
        if (bestTank != null) return bestTank;
        return attackStructures ? bestStruct : null;
    }
}
