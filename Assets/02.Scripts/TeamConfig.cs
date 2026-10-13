using UnityEngine;

// 팀 관련 공통 설정. 어느 팀이 플레이어(아군)인지와 비대칭 스펙 배율을 둔다
public static class TeamConfig
{
    public static Team playerTeam = Team.Yellow;

    public const float allyHpMultiplier = 3.0f;     // 아군 체력은 적군의 3배
    public const float enemyDamageMultiplier = 0.6f; // 적군 공격력은 아군보다 낮다

    public static bool IsAlly(Team t)
    {
        return t == playerTeam;
    }

    public static float DamageMultiplier(Team t)
    {
        return IsAlly(t) ? 1.0f : enemyDamageMultiplier;
    }
}
