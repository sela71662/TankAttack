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

    void OnGUI()
    {
        if (!Active || MapView.IsOpen) return;

        float s = Screen.height / 1080f;
        float w = 150 * s, h = 72 * s, gap = 6 * s;
        int count = summonKinds.Length + 1;
        float total = count * w + (count - 1) * gap;
        float x = (Screen.width - total) * 0.5f;
        float y = Screen.height - h - 14 * s;

        for (int i = 0; i < summonKinds.Length; i++)
        {
            var kind = summonKinds[i];
            int cost = GoldWallet.SummonCost(kind);
            if (Button(ref x, y, w, h, gap, s, $"{i + 1}", kind.ToString(), $"{cost}G", GoldWallet.Gold >= cost && allySpawner != null && allySpawner.CanBuy))
                Summon(kind);
        }
        if (Button(ref x, y, w, h, gap, s, "R", "Repair Core", $"{GoldWallet.repairCost}G",
                   GoldWallet.Gold >= GoldWallet.repairCost && coreHealth != null && coreHealth.hp < coreHealth.maxHp))
            RepairCore();
    }

    // 살 수 있는 버튼은 밝게, 골드가 모자라거나 더 살 수 없으면 어둡게 그린다 (눌러도 이유를 알려 준다)
    static bool Button(ref float x, float y, float w, float h, float gap, float s, string key, string label, string price, bool enabled)
    {
        var r = new Rect(x, y, w, h);
        x += w + gap;

        var old = GUI.color;
        GUI.color = enabled ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.55f, 0.55f, 0.55f, 0.85f);
        bool clicked = GUI.Button(r, GUIContent.none);
        GUI.color = old;

        var keyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        var priceStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };

        keyStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);
        nameStyle.normal.textColor = enabled ? Color.white : new Color(0.75f, 0.75f, 0.75f);
        priceStyle.normal.textColor = enabled ? new Color(1f, 0.85f, 0.25f) : new Color(0.7f, 0.6f, 0.3f);

        GUI.Label(new Rect(r.x + 6 * s, r.y + 3 * s, r.width, r.height), $"[{key}]", keyStyle);
        GUI.Label(new Rect(r.x, r.y + 18 * s, r.width, r.height), label, nameStyle);
        GUI.Label(new Rect(r.x, r.y, r.width, r.height - 4 * s), price, priceStyle);
        return clicked;
    }
}
