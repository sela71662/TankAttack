using System;
using UnityEngine;

// 체력. 탱크는 아군일 때 baseHp의 3배, 구조물(코어, 게이트)은 배율 없이 baseHp를 그대로 쓴다
public class Health : MonoBehaviour
{
    public float baseHp = 100.0f;
    public bool useTeamMultiplier = true; // 탱크는 true, 코어/게이트는 false
    public bool destroyOnDeath = true;
    public float hpScale = 1.0f;          // 웨이브 보정용 배율 (Wave 7 적: 10배)
    public Health redirect;               // 지정하면 받은 피해를 이 대상에게 넘긴다 (건물 -> 코어)

    public float maxHp { get; private set; }
    public float hp { get; private set; }
    public bool IsDead => hp <= 0;

    public event Action<Health> onDeath;

    void Awake()
    {
        Refresh();
    }

    // 팀이 바뀌었을 때 최대 체력을 다시 계산하고 가득 채운다
    public void Refresh()
    {
        maxHp = CalcMaxHp();
        hp = maxHp;
    }

    float CalcMaxHp()
    {
        float mul = hpScale;
        var tm = GetComponent<TeamMember>();
        if (useTeamMultiplier && tm != null && TeamConfig.IsAlly(tm.team))
            mul *= TeamConfig.allyHpMultiplier * AllyUpgrades.HpMul;
        return baseHp * mul;
    }

    // 아군 강화처럼 최대 체력만 바뀔 때: 현재 체력 비율을 유지한다
    public void RescaleKeepRatio()
    {
        float ratio = maxHp > 0 ? hp / maxHp : 1.0f;
        maxHp = CalcMaxHp();
        hp = maxHp * ratio;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        hp = Mathf.Min(maxHp, hp + amount);
    }

    public void TakeDamage(float damage)
    {
        // 건물처럼 피해를 다른 대상(코어)이 대신 받는 경우
        if (redirect != null) { redirect.TakeDamage(damage); return; }
        if (IsDead) return;

        hp = Mathf.Max(0, hp - damage);
        if (hp > 0) return;

        onDeath?.Invoke(this);
        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
