using System.Collections.Generic;
using UnityEngine;

// 웨이브, 코어 체력, 골드, 플레이어 체력, 아이템 효과를 한 번에 그리는 IMGUI HUD.
// 인트로/엔딩과 같은 MenuKit의 밀리터리 HUD 스타일(각진 패널, 칸 막대, 호박색 강조).
// WaveManager가 시작할 때 스스로 붙인다. 상점 버튼은 ShopUI가 그린다
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public WaveManager waves;

    struct Popup { public string text; public Color color; public float time; public string icon; }

    readonly List<Popup> goldPops = new List<Popup>();
    readonly List<Popup> toasts = new List<Popup>();
    int lastGold;

    const float PopupLife = 1.4f;
    const float ToastLife = 2.2f;

    static readonly Color GoldColor = MenuKit.Amber;

    // 막대가 부드럽게 차고 줄도록 화면에 보이는 값을 따로 둔다 (coreTrail은 줄어들기 전 값의 잔상)
    float coreShown = 1f, coreTrail = 1f, gaugeShown;
    float endShownAt = -1f;

    static readonly string[] itemNames = { "공격력 증가", "연사 속도 증가", "체력 회복", "이동 속도 증가" };

    // Resources/ItemIcons/<이름>.png. 없으면 null이라 색 칸으로 대신한다
    static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

    static Texture2D Icon(string name)
    {
        if (!iconCache.TryGetValue(name, out var t))
        {
            t = Resources.Load<Texture2D>("ItemIcons/" + name);
            iconCache[name] = t;
        }
        return t;
    }

    static void DrawIcon(Rect r, string name, Color fallback)
    {
        var tex = Icon(name);
        if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
        else MenuKit.Fill(r, fallback);
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        lastGold = GoldWallet.Gold;
    }

    // 아이템을 먹었을 때 화면에 알린다
    public static void Pickup(ItemType type, float duration, float healRatio)
    {
        if (Instance == null) return;
        string text = type == ItemType.Heal ? $"체력 회복 +{healRatio * 100:F0}%" : $"{itemNames[(int)type]}  {duration:F0}초";
        Instance.AddToast(text, ItemPickup.ColorOf(type), type.ToString());
    }

    public static void Toast(string text)
    {
        if (Instance != null) Instance.AddToast(text, Color.white, null);
    }

    void AddToast(string text, Color color, string icon)
    {
        toasts.Add(new Popup { text = text, color = color, time = Time.unscaledTime, icon = icon });
        if (toasts.Count > 4) toasts.RemoveAt(0);
    }

    void Update()
    {
        // 골드가 늘거나 줄 때마다 숫자를 띄운다
        int g = GoldWallet.Gold;
        if (g != lastGold)
        {
            goldPops.Add(new Popup
            {
                text = g > lastGold ? $"+{g - lastGold}" : $"{g - lastGold}",
                color = g > lastGold ? GoldColor : MenuKit.Alert,
                time = Time.unscaledTime
            });
            if (goldPops.Count > 5) goldPops.RemoveAt(0);
            lastGold = g;
        }
        goldPops.RemoveAll(p => Time.unscaledTime - p.time > PopupLife);
        toasts.RemoveAll(p => Time.unscaledTime - p.time > ToastLife);

        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-10f * dt);
        if (waves != null && waves.coreHealth != null && waves.coreHealth.maxHp > 0)
        {
            float ratio = Mathf.Clamp01(waves.coreHealth.hp / waves.coreHealth.maxHp);
            coreShown = Mathf.Lerp(coreShown, ratio, k);
            coreTrail = coreTrail > ratio ? Mathf.MoveTowards(coreTrail, ratio, 0.25f * dt) : ratio;
        }
        float gt = AllyUpgrades.CanUpgrade ? (float)AllyUpgrades.Progress / AllyUpgrades.KillsPerLevel : 1f;
        gaugeShown = Mathf.Lerp(gaugeShown, gt, k);
    }

    float S => UiKit.S;

    void OnGUI()
    {
        if (waves == null || MapView.IsOpen) return; // 전체 맵을 볼 때는 맵 글자만 보인다
        MenuKit.A = 1f;
        DrawWaveAndCore();
        DrawGold();
        DrawPlayerAndBuffs();
        DrawToasts();
        DrawEndBanner();
    }

    // 체력 비율에 따라 초록 -> 호박 -> 빨강
    static Color HealthColor(float ratio)
    {
        Color lo = MenuKit.Alert, mid = MenuKit.Amber, hi = new Color(0.62f, 0.85f, 0.40f);
        return ratio < 0.5f ? Color.Lerp(lo, mid, ratio / 0.5f) : Color.Lerp(mid, hi, (ratio - 0.5f) / 0.5f);
    }

    // 위쪽 가운데: 웨이브 진행과 코어 체력
    void DrawWaveAndCore()
    {
        float w = 580 * S, h = 100 * S;
        var box = new Rect((Screen.width - w) * 0.5f, 12 * S, w, h);
        MenuKit.HudPanel(box, MenuKit.Amber);

        var row = new Rect(box.x + 22 * S, box.y + 8 * S, w - 44 * S, 40 * S);
        switch (waves.state)
        {
            case WaveManager.State.Waiting:
                MenuKit.Text(row, MenuKit.Spaced("READY"), 30, MenuKit.Amber, MenuKit.Bold);
                break;
            case WaveManager.State.Intermission:
                MenuKit.Text(row, MenuKit.Spaced("NEXT WAVE"), 17, MenuKit.Dim, MenuKit.Medium);
                MenuKit.Text(row, $"{Mathf.CeilToInt(waves.intermissionLeft)}s", 38, MenuKit.Paper, MenuKit.Bold, TextAnchor.MiddleRight);
                break;
            case WaveManager.State.Victory:
                MenuKit.Text(row, MenuKit.Spaced("VICTORY"), 32, MenuKit.Amber, MenuKit.Bold);
                break;
            case WaveManager.State.Defeat:
                MenuKit.Text(row, MenuKit.Spaced("DEFEAT"), 32, MenuKit.Alert, MenuKit.Bold);
                break;
            default:
                MenuKit.Text(row, MenuKit.Spaced("WAVE"), 17, MenuKit.Dim, MenuKit.Medium);
                MenuKit.Text(new Rect(row.x + 84 * S, row.y - 2 * S, 170 * S, row.height), $"{waves.currentWave} / {waves.waves.Count}", 38, MenuKit.Paper, MenuKit.Bold);
                MenuKit.Text(new Rect(row.xMax - 190 * S, row.y, 130 * S, row.height), MenuKit.Spaced("ENEMIES"), 17, MenuKit.Dim, MenuKit.Medium, TextAnchor.MiddleRight);
                MenuKit.Text(new Rect(row.xMax - 56 * S, row.y - 2 * S, 56 * S, row.height), $"{waves.AliveCount}", 38, MenuKit.Alert, MenuKit.Bold, TextAnchor.MiddleRight);
                break;
        }

        var core = waves.coreHealth;
        if (core != null)
        {
            var bar = new Rect(box.x + 22 * S, box.y + 54 * S, w - 44 * S, 28 * S);
            MenuKit.BlockBar(bar, coreShown, HealthColor(coreShown), coreTrail);
            MenuKit.TextOutlined(bar, $"CORE   {Mathf.CeilToInt(core.hp)} / {Mathf.CeilToInt(core.maxHp)}", 18, Color.white, MenuKit.Bold, TextAnchor.MiddleCenter);
        }
        // 웨이브 사이 대기 시간은 얇은 막대로도 보여 준다
        if (waves.state == WaveManager.State.Intermission && waves.betweenWaves > 0)
        {
            var t = new Rect(box.x + 22 * S, box.y + 90 * S, w - 44 * S, 3 * S);
            MenuKit.Fill(t, MenuKit.Line);
            MenuKit.Fill(new Rect(t.x, t.y, t.width * Mathf.Clamp01(waves.intermissionLeft / waves.betweenWaves), t.height), MenuKit.Amber);
        }
    }

    // 오른쪽 위: 골드와 늘고 준 양
    void DrawGold()
    {
        float w = 250 * S, h = 56 * S;
        var box = new Rect(Screen.width - w - 16 * S, 12 * S, w, h);
        MenuKit.HudPanel(box, MenuKit.Amber);

        float d = 38 * S;
        var coin = new Rect(box.x + 16 * S, box.y + (h - d) * 0.5f, d, d);
        DrawIcon(coin, "Coin", GoldColor);

        MenuKit.Text(new Rect(coin.xMax + 14 * S, box.y - 2 * S, w - d - 50 * S, h), $"{GoldWallet.Gold:N0}", 34, GoldColor, MenuKit.Bold);

        for (int i = 0; i < goldPops.Count; i++)
        {
            var p = goldPops[i];
            float t = (Time.unscaledTime - p.time) / PopupLife;
            var c = p.color; c.a = 1f - t * t;
            MenuKit.Text(new Rect(box.x, box.yMax + 6 * S + t * 22 * S + i * 22 * S, w - 16 * S, 26 * S), p.text, 24, c, MenuKit.Bold, TextAnchor.MiddleRight);
        }
    }

    // 왼쪽 아래: 아군 강화 단계, 아이템 효과 남은 시간 (플레이어 체력은 탱크 위 체력바에 있다)
    void DrawPlayerAndBuffs()
    {
        var player = PlayerTankController.Instance; // 죽어서 부활을 기다리는 동안은 null이다

        float w = 340 * S, x = 16 * S;
        float ph = 112 * S;
        float y = Screen.height - ph - 16 * S;

        // 강화 카드
        var panel = new Rect(x, y, w, ph);
        MenuKit.HudPanel(panel, MenuKit.Amber);
        MenuKit.Text(new Rect(panel.x + 18 * S, panel.y + 8 * S, 220 * S, 26 * S), MenuKit.Spaced("ALLY UPGRADE"), 15, MenuKit.Dim, MenuKit.Medium);
        MenuKit.Chip(new Rect(panel.xMax - 98 * S, panel.y + 9 * S, 82 * S, 24 * S), $"LV {AllyUpgrades.Level}/{AllyUpgrades.MaxLevel}", MenuKit.Amber);

        var gauge = new Rect(panel.x + 18 * S, panel.y + 40 * S, w - 36 * S, 26 * S);
        MenuKit.BlockBar(gauge, gaugeShown, MenuKit.Amber, -1f, 14f);
        MenuKit.TextOutlined(gauge, AllyUpgrades.CanUpgrade ? $"{AllyUpgrades.Progress} / {AllyUpgrades.KillsPerLevel}" : "MAX", 16, Color.white, MenuKit.Bold, TextAnchor.MiddleCenter);

        string reward = AllyUpgrades.CanUpgrade
            ? $"NEXT : Lv{AllyUpgrades.Level + 1} 공격력 +15%, 체력 +20%"
            : "최대 레벨";
        MenuKit.Text(new Rect(panel.x + 18 * S, panel.y + 72 * S, w - 36 * S, 28 * S), reward, 15, new Color(1f, 0.86f, 0.50f));

        // 남은 목숨: 강화 카드 바로 위
        float ly = y - 8 * S - 40 * S;
        var lives = new Rect(x, ly, w, 40 * S);
        MenuKit.HudPanel(lives, MenuKit.Amber);
        MenuKit.Text(new Rect(lives.x + 18 * S, lives.y, 110 * S, lives.height), MenuKit.Spaced("LIVES"), 15, MenuKit.Dim, MenuKit.Medium);
        int max = Mathf.Max(PlayerSpawner.MaxLives, 1);
        for (int i = 0; i < max; i++)
        {
            bool alive = i < PlayerSpawner.Lives;
            var blk = new Rect(lives.x + 126 * S + i * 38 * S, lives.y + 11 * S, 28 * S, lives.height - 22 * S);
            MenuKit.Fill(blk, alive ? MenuKit.Alert : new Color(1, 1, 1, 0.10f));
        }

        // 아이템 효과: 목숨 행 위로 쌓는다
        var buffs = player != null ? player.GetComponent<PlayerBuffs>() : null;
        if (buffs == null) return;
        float by = ly - 8 * S;
        foreach (ItemType t in new[] { ItemType.Damage, ItemType.FireRate, ItemType.Speed })
        {
            float left = buffs.Remaining(t);
            if (left <= 0) continue;
            by -= 48 * S;
            var row = new Rect(x, by, w, 42 * S);
            var col = ItemPickup.ColorOf(t);
            MenuKit.HudPanel(row, col);
            MenuKit.Fill(new Rect(row.x + 6 * S, row.y + 5 * S, 42 * S, row.height - 10 * S), new Color(col.r, col.g, col.b, 0.22f));
            DrawIcon(new Rect(row.x + 7 * S, row.y + 5 * S, 40 * S, row.height - 10 * S), t.ToString(), col);
            MenuKit.Text(new Rect(row.x + 60 * S, row.y + 2 * S, 170 * S, 18 * S), itemNames[(int)t], 13, MenuKit.Dim);
            MenuKit.BlockBar(new Rect(row.x + 60 * S, row.y + 23 * S, w - 150 * S, 12 * S), left / buffs.duration, col, -1f, 8f);
            MenuKit.Text(new Rect(row.xMax - 74 * S, row.y, 60 * S, row.height), $"{left:F0}s", 22, MenuKit.Paper, MenuKit.Bold, TextAnchor.MiddleRight);
        }
    }

    // 가운데 아래쪽에 아이템 획득 같은 짧은 알림을 쌓는다
    void DrawToasts()
    {
        float y = Screen.height * 0.64f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var p = toasts[i];
            float t = (Time.unscaledTime - p.time) / ToastLife;
            float a = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01(1f - Mathf.Max(0, t - 0.75f) / 0.25f);
            float rise = (1f - Mathf.Clamp01(t / 0.15f)) * 14 * S; // 아래에서 살짝 올라오며 나타난다

            bool hasIcon = p.icon != null && Icon(p.icon) != null;
            float tw = UiKit.TextWidth(p.text, 24) + 2 * S;
            float iw = hasIcon ? 44 * S : 0;
            float pw = tw + iw + 52 * S, ph = 46 * S;
            var r = new Rect((Screen.width - pw) * 0.5f, y + rise, pw, ph);

            MenuKit.A = a;
            MenuKit.HudPanel(r, p.color);
            MenuKit.Fill(new Rect(r.x, r.y, 5 * S, r.height), p.color);
            if (hasIcon)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(new Rect(r.x + 20 * S, r.y + 2 * S, 42 * S, 42 * S), Icon(p.icon), ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }
            MenuKit.Text(new Rect(r.x + 24 * S + iw, r.y, tw + 8 * S, r.height), p.text, 24, p.color);
            MenuKit.A = 1f;
            y -= 54 * S;
        }
    }

    void DrawEndBanner()
    {
        bool win = waves.state == WaveManager.State.Victory;
        if (!win) { endShownAt = -1f; return; } // 패배 화면은 Ending 씬이 보여 준다
        if (endShownAt < 0) endShownAt = Time.unscaledTime;
        float a = Mathf.Clamp01((Time.unscaledTime - endShownAt) / 0.5f);

        float h = 170 * S, y = Screen.height * 0.34f;
        MenuKit.A = a;
        MenuKit.Fill(new Rect(0, y, Screen.width, h), new Color(MenuKit.Ink.r, MenuKit.Ink.g, MenuKit.Ink.b, 0.78f));
        MenuKit.Fill(new Rect(0, y, Screen.width, 3 * S), MenuKit.Amber);
        MenuKit.Fill(new Rect(0, y + h - 3 * S, Screen.width, 3 * S), MenuKit.Amber);

        MenuKit.Text(new Rect(0, y + 10 * S, Screen.width, 110 * S), "VICTORY", 92, MenuKit.Amber, MenuKit.TitleFont, TextAnchor.MiddleCenter);
        MenuKit.Text(new Rect(0, y + 118 * S, Screen.width, 40 * S), "모든 게이트를 파괴하고 코어를 지켜냈습니다", 24, MenuKit.Paper, null, TextAnchor.MiddleCenter);
        MenuKit.A = 1f;
    }
}
