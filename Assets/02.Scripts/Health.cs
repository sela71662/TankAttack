using System;
using UnityEngine;

// 체력. 탱크는 아군일 때 baseHp의 3배, 구조물(코어, 게이트)은 배율 없이 baseHp를 그대로 쓴다
public class Health : MonoBehaviour
{
    public float baseHp = 100.0f;
    public bool useTeamMultiplier = true; // 탱크는 true, 코어/게이트는 false
    public bool destroyOnDeath = true;
    public float hpScale = 1.0f;          // 웨이브 보정용 배율 (Wave 7 적: 10배)

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
        float mul = hpScale;
        var tm = GetComponent<TeamMember>();
        if (useTeamMultiplier && tm != null && TeamConfig.IsAlly(tm.team))
            mul *= TeamConfig.allyHpMultiplier;

        maxHp = baseHp * mul;
        hp = maxHp;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        hp = Mathf.Max(0, hp - damage);
        if (hp > 0) return;

        onDeath?.Invoke(this);
        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
