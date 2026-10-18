using UnityEngine;

// 골드로 아군 소환, 코어 수리를 하는 IMGUI 상점 (MenuKit의 밀리터리 HUD 스타일). 아군 강화는 적 처치 게이지로 오른다 (AllyUpgrades)
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
        MenuKit.A = 1f;

        float s = UiKit.S;
        float w = 140 * s, h = 82 * s, gap = 8 * s;
        int count = summonKinds.Length + 1;
        float total = count * w + (count - 1) * gap;
        float x = (Screen.width - total) * 0.5f;
        float y = Screen.height - h - 16 * s;

        // 가장 긴 이름이 이름 칸에 들어가는 가장 큰 글자 크기를 모든 카드가 함께 쓴다
        const string RepairLabel = "Repair Core";
        float nameWidth = w - (NameX + 6) * s;
        int nameSize = 18;
        foreach (var k in summonKinds)
            while (nameSize > 10 && MenuKit.TextWidth(k.ToString().ToUpperInvariant(), nameSize, MenuKit.Bold) > nameWidth) nameSize--;
        while (nameSize > 10 && MenuKit.TextWidth(RepairLabel.ToUpperInvariant(), nameSize, MenuKit.Bold) > nameWidth) nameSize--;

        for (int i = 0; i < summonKinds.Length; i++)
        {
            var kind = summonKinds[i];
            int cost = GoldWallet.SummonCost(kind);
            if (Card(ref x, y, w, h, gap, nameSize, $"{i + 1}", kind.ToString(), cost, GoldWallet.Gold >= cost && allySpawner != null && allySpawner.CanBuy))
                Summon(kind);
        }
        if (Card(ref x, y, w, h, gap, nameSize, "R", RepairLabel, GoldWallet.repairCost,
                 GoldWallet.Gold >= GoldWallet.repairCost && coreHealth != null && coreHealth.hp < coreHealth.maxHp))
            RepairCore();
    }

    const float NameX = 40f; // 카드 왼쪽 끝에서 이름이 시작하는 위치 (1080p 기준 px)

    // 키 배지, 이름, 가격이 있는 카드. 살 수 있으면 밝게, 골드가 모자라거나 더 살 수 없으면 어둡게 그린다 (눌러도 이유를 알려 준다)

    static bool Card(ref float x, float y, float w, float h, float gap, int nameSize, string key, string label, int price, bool enabled)
    {
        float s = UiKit.S;
        var r = new Rect(x, y, w, h);
        x += w + gap;

        bool hover = r.Contains(Event.current.mousePosition);
        Color accent = enabled ? MenuKit.Amber : MenuKit.Dim;
        MenuKit.HudPanel(r, enabled ? accent : new Color(accent.r, accent.g, accent.b, 0.5f));
        if (enabled && hover) MenuKit.Fill(r, new Color(MenuKit.Amber.r, MenuKit.Amber.g, MenuKit.Amber.b, 0.14f));

        // 키 표시: 살 수 있으면 호박색 칸에 어두운 글자, 아니면 테두리만
        var badge = new Rect(r.x + 8 * s, r.y + 8 * s, 26 * s, 24 * s);
        if (enabled)
        {
            MenuKit.Fill(badge, MenuKit.Amber);
            MenuKit.Text(badge, key, 17, MenuKit.Ink, MenuKit.Bold, TextAnchor.MiddleCenter);
        }
        else
        {
            MenuKit.Chip(badge, key, MenuKit.Dim, 17);
        }

        // 이름: 키 칸 오른쪽. 모든 카드가 같은 글자 크기(nameSize)를 쓴다
        Color nameColor = enabled ? MenuKit.Paper : MenuKit.Dim;
        MenuKit.Text(new Rect(r.x + NameX * s, r.y + 6 * s, w - (NameX + 6) * s, 32 * s), label.ToUpperInvariant(), nameSize, nameColor, MenuKit.Bold);

        // 가격: 구분선 아래에 숫자와 G
        MenuKit.HLine(r.x + 10 * s, r.yMax - 38 * s, w - 20 * s, MenuKit.Line);
        Color priceColor = enabled ? MenuKit.Amber : new Color(MenuKit.Amber.r, MenuKit.Amber.g, MenuKit.Amber.b, 0.45f);
        MenuKit.Text(new Rect(r.x + 12 * s, r.yMax - 36 * s, w - 24 * s, 32 * s), $"{price}", 28, priceColor, MenuKit.Bold);
        MenuKit.Text(new Rect(r.x + 12 * s, r.yMax - 36 * s, w - 24 * s, 32 * s), "G", 18, priceColor, MenuKit.Medium, TextAnchor.MiddleRight);

        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }
}
