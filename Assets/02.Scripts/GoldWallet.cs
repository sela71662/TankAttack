using UnityEngine;

// 골드 보유량과 보상/비용 표. 난이도를 조절할 때는 이 파일의 값을 고친다
public static class GoldWallet
{
    public static int Gold { get; private set; }

    public static void Reset(int start)
    {
        Gold = start;
    }

    public static void Add(int amount)
    {
        Gold += Mathf.Max(0, amount);
    }

    public static bool TrySpend(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    // 적 한 대를 처치했을 때 받는 골드
    public static int KillReward(TankKind kind)
    {
        switch (kind)
        {
            case TankKind.Original: return 10;
            case TankKind.Light: return 15;
            case TankKind.Heavy: return 25;
            case TankKind.MegaBall: return 20;
            case TankKind.UFO: return 20;
            default: return 10;
        }
    }

    // 웨이브를 막아냈을 때 받는 보너스
    public static int WaveBonus(int wave)
    {
        return 20 + 10 * wave;
    }

    // 아군 소환 비용
    public static int SummonCost(TankKind kind)
    {
        switch (kind)
        {
            case TankKind.Original: return 50;
            case TankKind.Light: return 80;
            case TankKind.Heavy: return 150;
            case TankKind.MegaBall: return 120;
            case TankKind.UFO: return 100;
            default: return 50;
        }
    }

    public const int gateReward = 150; // 적 스폰 게이트를 파괴했을 때

    public const int repairCost = 100;
    public const float repairRatio = 0.25f; // 코어 최대 체력의 이 비율만큼 회복

    public static int UpgradeCost(int level)
    {
        return 150 * (level + 1);
    }
}

// 아군 강화 단계. 아군 전체(플레이어 포함)의 체력과 공격력에 적용된다
public static class AllyUpgrades
{
    public const int MaxLevel = 5;
    public static int Level { get; private set; }

    public static float DamageMul => 1.0f + 0.15f * Level;
    public static float HpMul => 1.0f + 0.2f * Level;

    public const int KillsPerLevel = 8;      // 적을 이만큼 처치할 때마다 한 레벨 오른다
    public static int Progress { get; private set; } // 현재 레벨 게이지에 쌓인 처치 수

    public static void Reset() { Level = 0; Progress = 0; }
    public static bool CanUpgrade => Level < MaxLevel;
    public static void LevelUp() { if (CanUpgrade) Level++; }

    // 적 처치 1회. 게이지가 차면 레벨이 오르고 이미 있는 아군 탱크의 최대 체력도 올린다
    public static void AddKill()
    {
        if (!CanUpgrade) return;
        Progress++;
        if (Progress < KillsPerLevel) return;

        Progress = 0;
        LevelUp();
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !TeamConfig.IsAlly(t.team)) continue;
            var h = t.GetComponent<Health>();
            if (h != null) h.RescaleKeepRatio();
        }
        GameHUD.Toast($"레벨 업!  Lv {Level}");
    }
}
