using UnityEngine;

// 골드로 아군 소환, 코어 수리, 아군 강화를 하는 임시 IMGUI 상점 (정식 UI 전까지 사용)
// 숫자키 1~5: 소환, R: 코어 수리, U: 아군 강화. 커서가 풀려 있으면(Esc) 화면 버튼도 누를 수 있다
public class ShopUI : MonoBehaviour
{
    public WaveManager waves;
    public AllySpawner allySpawner;
    public Health coreHealth;

    static readonly TankKind[] summonKinds =
    {
        TankKind.Original, TankKind.Heavy, TankKind.Light, TankKind.UFO, TankKind.MegaBall
    };

    string message;
    float messageUntil;

    bool Active => waves != null && waves.state != WaveManager.State.Victory && waves.state != WaveManager.State.Defeat;

    void Update()
    {
        if (!Active) return;

        for (int i = 0; i < summonKinds.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Summon(summonKinds[i]);
        if (Input.GetKeyDown(KeyCode.R)) RepairCore();
        if (Input.GetKeyDown(KeyCode.U)) UpgradeAllies();
    }

    void Toast(string text)
    {
        message = text;
        messageUntil = Time.time + 2.0f;
    }

    void Summon(TankKind kind)
    {
        if (allySpawner == null) return;
        if (!allySpawner.CanBuy) { Toast("Ally limit reached"); return; }
        if (!GoldWallet.TrySpend(GoldWallet.SummonCost(kind))) { Toast("Not enough gold"); return; }
        allySpawner.Spawn(kind);
    }

    void RepairCore()
    {
        if (coreHealth == null || coreHealth.IsDead) return;
        if (coreHealth.hp >= coreHealth.maxHp) { Toast("Core is at full health"); return; }
        if (!GoldWallet.TrySpend(GoldWallet.repairCost)) { Toast("Not enough gold"); return; }
        coreHealth.Heal(coreHealth.maxHp * GoldWallet.repairRatio);
    }

    void UpgradeAllies()
    {
        if (!AllyUpgrades.CanUpgrade) { Toast("Upgrade is maxed"); return; }
        if (!GoldWallet.TrySpend(GoldWallet.UpgradeCost(AllyUpgrades.Level))) { Toast("Not enough gold"); return; }

        AllyUpgrades.LevelUp();
        // 이미 있는 아군 탱크의 최대 체력도 올린다
        foreach (var t in TeamMember.All)
        {
            if (t.isStructure || !TeamConfig.IsAlly(t.team)) continue;
            var h = t.GetComponent<Health>();
            if (h != null) h.RescaleKeepRatio();
        }
    }

    void OnGUI()
    {
        if (!Active) return;

        const float w = 150, h = 46, gap = 6;
        int count = summonKinds.Length + 2;
        float total = count * w + (count - 1) * gap;
        float x = (Screen.width - total) * 0.5f;
        float y = Screen.height - h - 12;

        for (int i = 0; i < summonKinds.Length; i++)
        {
            var kind = summonKinds[i];
            if (Button(ref x, y, w, h, gap, $"[{i + 1}] {kind}\n{GoldWallet.SummonCost(kind)}G"))
                Summon(kind);
        }
        if (Button(ref x, y, w, h, gap, $"[R] Repair Core\n{GoldWallet.repairCost}G"))
            RepairCore();
        string up = AllyUpgrades.CanUpgrade
            ? $"[U] Upgrade Lv{AllyUpgrades.Level}\n{GoldWallet.UpgradeCost(AllyUpgrades.Level)}G"
            : "[U] Upgrade MAX";
        if (Button(ref x, y, w, h, gap, up))
            UpgradeAllies();

        DrawBuffs();

        if (Time.time < messageUntil)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, y - 36, Screen.width, 30), message, style);
        }
    }

    static bool Button(ref float x, float y, float w, float h, float gap, string text)
    {
        bool clicked = GUI.Button(new Rect(x, y, w, h), text);
        x += w + gap;
        return clicked;
    }

    // 플레이어에게 걸린 아이템 효과의 남은 시간
    void DrawBuffs()
    {
        var player = PlayerTankController.Instance;
        var buffs = player != null ? player.GetComponent<PlayerBuffs>() : null;
        if (buffs == null) return;

        float y = 100;
        foreach (ItemType t in new[] { ItemType.Damage, ItemType.FireRate, ItemType.Speed })
        {
            float left = buffs.Remaining(t);
            if (left <= 0) continue;
            var old = GUI.color;
            GUI.color = ItemPickup.ColorOf(t);
            GUI.Label(new Rect(16, y, 300, 26), $"{t} {left:F0}s");
            GUI.color = old;
            y += 24;
        }
    }
}
