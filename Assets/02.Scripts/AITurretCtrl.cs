using UnityEngine;

// 가장 가까운 적 팀 탱크를 찾아 조준하고 발사한다 (Tank_Alternative_Model 외 포를 쏘는 탱크용)
[RequireComponent(typeof(TurretAimer), typeof(TankShooter), typeof(TeamMember))]
public class AITurretCtrl : MonoBehaviour
{
    public float range = 60.0f;         // 사거리
    public float aimTolerance = 3.0f;   // 이 각도(도) 안으로 들어오면 발사
    public float targetHeight = 1.2f;   // 목표 바닥에서 조준할 높이
    public float retargetInterval = 0.5f;
    public bool attackStructures = false; // 적 탱크가 없을 때 게이트 같은 구조물을 공격한다 (UFO)

    TurretAimer aimer;
    TankShooter shooter;
    TeamMember me;
    TeamMember target;
    float nextSearchTime;

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
        me = GetComponent<TeamMember>();
    }

    void Update()
    {
        if (target == null || Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + retargetInterval;
            target = FindNearestEnemy();
        }
        if (target == null) return;

        aimer.AimAt(target.transform.position + Vector3.up * targetHeight);

        if (aimer.IsAimed(aimTolerance))
            shooter.TryFire();
    }

    // 적 탱크를 우선하고, 없을 때만 구조물을 고른다
    TeamMember FindNearestEnemy()
    {
        TeamMember bestTank = null, bestStruct = null;
        float tankDist = range * range, structDist = range * range;
        foreach (var t in FindObjectsByType<TeamMember>())
        {
            if (!me.IsEnemy(t)) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (t.isStructure)
            {
                if (d < structDist) { structDist = d; bestStruct = t; }
            }
            else if (d < tankDist) { tankDist = d; bestTank = t; }
        }
        if (bestTank != null) return bestTank;
        return attackStructures ? bestStruct : null;
    }
}
