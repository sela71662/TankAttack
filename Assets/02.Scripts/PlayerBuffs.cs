using UnityEngine;

// 플레이어 탱크에 붙어 아이템 효과(공격력, 연사, 이동 속도 증가는 일정 시간, 회복은 즉시)를 적용한다
public class PlayerBuffs : MonoBehaviour
{
    public float duration = 15.0f;
    public float damageBonus = 1.5f;     // 공격력 배율
    public float fireRateBonus = 1.6f;   // 연사 속도 배율 (재발사 대기 시간을 이 값으로 나눈다)
    public float speedBonus = 1.4f;      // 이동 속도 배율
    public float healRatio = 0.4f;       // 최대 체력 대비 회복 비율

    readonly float[] until = new float[4];
    TankShooter shooter;
    PlayerTankController ctrl;
    Health health;

    void Awake()
    {
        shooter = GetComponent<TankShooter>();
        ctrl = GetComponent<PlayerTankController>();
        health = GetComponent<Health>();
    }

    public void Apply(ItemType type)
    {
        if (type == ItemType.Heal)
        {
            if (health != null) health.Heal(health.maxHp * healRatio);
            return;
        }
        until[(int)type] = Time.time + duration;
    }

    public float Remaining(ItemType type)
    {
        return Mathf.Max(0, until[(int)type] - Time.time);
    }

    void Update()
    {
        if (shooter != null)
        {
            shooter.damageScale = Remaining(ItemType.Damage) > 0 ? damageBonus : 1.0f;
            shooter.cooldownScale = Remaining(ItemType.FireRate) > 0 ? 1.0f / fireRateBonus : 1.0f;
        }
        if (ctrl != null)
            ctrl.speedScale = Remaining(ItemType.Speed) > 0 ? speedBonus : 1.0f;
    }
}
