using UnityEngine;

// 골드로 아군 소환, 코어 수리를 하는 임시 IMGUI 상점 (정식 UI 전까지 사용). 아군 강화는 적 처치 게이지로 오른다 (AllyUpgrades)
// 숫자키 1~5: 소환, R: 코어 수리. 커서가 풀려 있으면(Esc) 화면 버튼도 누를 수 있다
public class ShopUI : MonoBehaviour
{
    public WaveManager waves;
    public AllySpawner allySpawner;
    public Health coreHealth;

    static readonly TankKind[] summonKinds =
    {
        TankKind.Original, TankKind.Heavy, TankKind.Light, TankKind.UFO, TankKind.MegaBall
    };

    bool Active => waves != null && waves.state != WaveManager.State.Victory && waves.state != WaveManager.State.Defeat;

    void Update()
    {
        if (!Active) return;

        for (int i = 0; i < summonKinds.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Summon(summonKinds[i]);
        if (Input.GetKeyDown(KeyCode.R)) RepairCore();
    }

    static void Toast(string text)
    {
        GameHUD.Toast(text);
    }

    void Summon(TankKind kind)
    {
        if (allySpawner == null) return;
        if (!allySpawner.CanBuy) { Toast("아군 수 한도에 도달했습니다"); return; }
        if (!GoldWallet.TrySpend(GoldWallet.SummonCost(kind))) { Toast("골드가 부족합니다"); return; }
        allySpawner.Spawn(kind);
    }

    void RepairCore()
    {
        if (coreHealth == null || coreHealth.IsDead) return;
        if (coreHealth.hp >= coreHealth.maxHp) { Toast("코어 체력이 가득 찼습니다"); return; }
        if (!GoldWallet.TrySpend(GoldWallet.repairCost)) { Toast("골드가 부족합니다"); return; }
        coreHealth.Heal(coreHealth.maxHp * GoldWallet.repairRatio);
    }

    void OnGUI()
    {
        if (!Active || MapView.IsOpen) return;

        float s = UiKit.S;
        float w = 140 * s, h = 82 * s, gap = 8 * s;
        int count = summonKinds.Length + 1;
        float total = count * w + (count - 1) * gap;
        float x = (Screen.width - total) * 0.5f;
        float y = Screen.height - h - 16 * s;

        for (int i = 0; i < summonKinds.Length; i++)
        {
            var kind = summonKinds[i];
            int cost = GoldWallet.SummonCost(kind);
            if (Card(ref x, y, w, h, gap, $"{i + 1}", kind.ToString(), cost, GoldWallet.Gold >= cost && allySpawner != null && allySpawner.CanBuy))
                Summon(kind);
        }
        if (Card(ref x, y, w, h, gap, "R", "Repair Core", GoldWallet.repairCost,
                 GoldWallet.Gold >= GoldWallet.repairCost && coreHealth != null && coreHealth.hp < coreHealth.maxHp))
            RepairCore();
    }

    // 키 배지, 이름, 가격 칩이 있는 카드. 살 수 있으면 밝게, 골드가 모자라거나 더 살 수 없으면 어둡게 그린다 (눌러도 이유를 알려 준다)
    static bool Card(ref float x, float y, float w, float h, float gap, string key, string label, int price, bool enabled)
    {
        float s = UiKit.S;
        var r = new Rect(x, y, w, h);
        x += w + gap;

        bool hover = r.Contains(Event.current.mousePosition);
        var bg = enabled ? new Color(0.06f, 0.10f, 0.16f, hover ? 0.95f : 0.85f) : new Color(0.05f, 0.06f, 0.08f, 0.78f);
        UiKit.Panel(r, 12, bg);
        if (enabled) UiKit.Outline(r, new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, hover ? 0.9f : 0.35f), Mathf.Max(1f, 1.5f * s), 12 * s);

        // 키 배지
        var badge = new Rect(r.x + 8 * s, r.y + 8 * s, 28 * s, 24 * s);
        UiKit.Round(badge, enabled ? new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.9f) : new Color(0.4f, 0.4f, 0.45f, 0.8f), 7 * s);
        UiKit.Label(badge, key, 15, new Color(0.03f, 0.05f, 0.08f), TextAnchor.MiddleCenter);

        Color nameColor = enabled ? Color.white : new Color(0.62f, 0.64f, 0.68f);
        UiKit.Label(new Rect(r.x + 40 * s, r.y + 6 * s, w - 46 * s, 28 * s), label, 16, nameColor, TextAnchor.MiddleLeft);

        // 가격 칩
        var chip = new Rect(r.x + 8 * s, r.yMax - 34 * s, w - 16 * s, 26 * s);
        UiKit.Pill(chip, new Color(0f, 0f, 0f, 0.35f));
        UiKit.Label(chip, $"{price} G", 17, enabled ? UiKit.Gold : new Color(0.65f, 0.58f, 0.34f));

        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }
}
