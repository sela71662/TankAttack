using System.Collections.Generic;
using UnityEngine;

// 웨이브, 코어 체력, 골드, 플레이어 체력, 아이템 효과를 한 번에 그리는 IMGUI HUD (정식 UI 전까지 사용).
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

    static readonly Color GoldColor = UiKit.Gold;

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
        else UiKit.Round(r, fallback, r.height * 0.25f);
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
                color = g > lastGold ? GoldColor : new Color(1f, 0.45f, 0.4f),
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
        DrawWaveAndCore();
        DrawGold();
        DrawPlayerAndBuffs();
        DrawToasts();
        DrawEndBanner();
    }

    // 위쪽 가운데: 웨이브 진행과 코어 체력
    void DrawWaveAndCore()
    {
        float w = 540 * S, h = 108 * S;
        var box = new Rect((Screen.width - w) * 0.5f, 12 * S, w, h);
        UiKit.Panel(box, 14);

        var row = new Rect(box.x + 20 * S, box.y + 8 * S, w - 40 * S, 38 * S);
        switch (waves.state)
        {
            case WaveManager.State.Waiting:
                UiKit.Label(row, "READY", 28, UiKit.Accent);
                break;
            case WaveManager.State.Intermission:
                UiKit.Label(row, $"다음 웨이브까지  {Mathf.CeilToInt(waves.intermissionLeft)}초", 24, Color.white);
                break;
            case WaveManager.State.Victory:
                UiKit.Label(row, "VICTORY", 30, GoldColor);
                break;
            case WaveManager.State.Defeat:
                UiKit.Label(row, "DEFEAT", 30, UiKit.Danger);
                break;
            default:
                UiKit.Label(row, "WAVE", 15, UiKit.Dim, TextAnchor.MiddleLeft);
                UiKit.Label(new Rect(row.x + 58 * S, row.y - 2 * S, 160 * S, row.height), $"{waves.currentWave} / {waves.waves.Count}", 30, Color.white, TextAnchor.MiddleLeft);
                UiKit.Label(new Rect(row.xMax - 150 * S, row.y, 76 * S, row.height), "ENEMIES", 15, UiKit.Dim, TextAnchor.MiddleRight);
                UiKit.Label(new Rect(row.xMax - 66 * S, row.y - 2 * S, 66 * S, row.height), $"{waves.AliveCount}", 30, new Color(1f, 0.55f, 0.45f), TextAnchor.MiddleRight);
                break;
        }

        var core = waves.coreHealth;
        if (core != null)
        {
            var bar = new Rect(box.x + 20 * S, box.y + 56 * S, w - 40 * S, 30 * S);
            Color lo = new Color(0.95f, 0.25f, 0.2f), hi = new Color(0.25f, 0.9f, 0.9f);
            UiKit.Bar(bar, coreShown, Color.Lerp(lo, hi, coreShown), coreTrail);
            UiKit.Label(bar, $"CORE   {Mathf.CeilToInt(core.hp)} / {Mathf.CeilToInt(core.maxHp)}", 16, Color.white);
        }
        // 웨이브 사이 대기 시간은 얇은 막대로도 보여 준다
        if (waves.state == WaveManager.State.Intermission && waves.betweenWaves > 0)
        {
            var t = new Rect(box.x + 20 * S, box.y + 94 * S, w - 40 * S, 6 * S);
            UiKit.Bar(t, waves.intermissionLeft / waves.betweenWaves, UiKit.Accent);
        }
    }

    // 오른쪽 위: 골드와 늘고 준 양
    void DrawGold()
    {
        float w = 250 * S, h = 58 * S;
        var box = new Rect(Screen.width - w - 16 * S, 12 * S, w, h);
        UiKit.Panel(box, 14);

        float d = 40 * S;
        var coin = new Rect(box.x + 14 * S, box.y + (h - d) * 0.5f, d, d);
        DrawIcon(coin, "Coin", GoldColor);

        UiKit.Label(new Rect(coin.xMax + 12 * S, box.y, w - d - 40 * S, h), $"{GoldWallet.Gold:N0}", 30, GoldColor, TextAnchor.MiddleLeft);

        for (int i = 0; i < goldPops.Count; i++)
        {
            var p = goldPops[i];
            float t = (Time.unscaledTime - p.time) / PopupLife;
            var c = p.color; c.a = 1f - t * t;
            UiKit.Label(new Rect(box.x, box.yMax + 6 * S + t * 22 * S + i * 22 * S, w - 16 * S, 26 * S), p.text, 22, c, TextAnchor.MiddleRight);
        }
    }

    // 왼쪽 아래: 아군 강화 단계, 아이템 효과 남은 시간 (플레이어 체력은 탱크 위 체력바에 있다)
    void DrawPlayerAndBuffs()
    {
        var player = PlayerTankController.Instance; // 죽어서 부활을 기다리는 동안은 null이다

        float w = 330 * S, x = 16 * S;
        float ph = 112 * S;
        float y = Screen.height - ph - 16 * S;

        // 강화 카드
        var panel = new Rect(x, y, w, ph);
        UiKit.Panel(panel, 14);
        UiKit.Label(new Rect(panel.x + 16 * S, panel.y + 8 * S, 190 * S, 26 * S), "ALLY UPGRADE", 15, UiKit.Dim, TextAnchor.MiddleLeft);
        var pill = new Rect(panel.xMax - 96 * S, panel.y + 9 * S, 80 * S, 24 * S);
        UiKit.Pill(pill, new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.22f));
        UiKit.Outline(pill, new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.7f), Mathf.Max(1f, S), pill.height * 0.5f);
        UiKit.Label(pill, $"Lv {AllyUpgrades.Level} / {AllyUpgrades.MaxLevel}", 14, UiKit.Accent);

        var gauge = new Rect(panel.x + 16 * S, panel.y + 40 * S, w - 32 * S, 24 * S);
        var gaugeColor = new Color(1f, 0.8f, 0.2f);
        UiKit.Bar(gauge, gaugeShown, gaugeColor);
        UiKit.Label(gauge, AllyUpgrades.CanUpgrade ? $"{AllyUpgrades.Progress} / {AllyUpgrades.KillsPerLevel}" : "MAX", 14, Color.white);

        string reward = AllyUpgrades.CanUpgrade
            ? $"NEXT : Lv{AllyUpgrades.Level + 1} 공격력 +15%, 체력 +20%"
            : "최대 레벨";
        UiKit.Label(new Rect(panel.x + 16 * S, panel.y + 72 * S, w - 32 * S, 28 * S), reward, 15, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleLeft);

        // 남은 목숨: 강화 카드 바로 위
        float ly = y - 8 * S - 38 * S;
        var lives = new Rect(x, ly, w, 38 * S);
        UiKit.Panel(lives, 12);
        UiKit.Label(new Rect(lives.x + 16 * S, lives.y, 90 * S, lives.height), "LIVES", 15, UiKit.Dim, TextAnchor.MiddleLeft);
        int max = Mathf.Max(PlayerSpawner.MaxLives, 1);
        for (int i = 0; i < max; i++)
        {
            bool alive = i < PlayerSpawner.Lives;
            Color hc = alive ? new Color(1f, 0.28f, 0.32f) : new Color(0.28f, 0.30f, 0.36f, 0.9f);
            UiKit.Heart(new Vector2(lives.x + 118 * S + i * 36 * S, lives.y + lives.height * 0.5f), 24 * S, hc);
        }

        // 아이템 효과: 목숨 행 위로 쌓는다
        var buffs = player != null ? player.GetComponent<PlayerBuffs>() : null;
        if (buffs == null) return;
        float by = ly - 8 * S;
        foreach (ItemType t in new[] { ItemType.Damage, ItemType.FireRate, ItemType.Speed })
        {
            float left = buffs.Remaining(t);
            if (left <= 0) continue;
            by -= 46 * S;
            var row = new Rect(x, by, w, 40 * S);
            UiKit.Panel(row, 12);
            var col = ItemPickup.ColorOf(t);
            UiKit.Round(new Rect(row.x + 4 * S, row.y + 4 * S, 44 * S, row.height - 8 * S), new Color(col.r, col.g, col.b, 0.28f), 8 * S);
            DrawIcon(new Rect(row.x + 6 * S, row.y + 4 * S, 40 * S, row.height - 8 * S), t.ToString(), col);
            UiKit.Label(new Rect(row.x + 58 * S, row.y + 2 * S, 170 * S, 18 * S), itemNames[(int)t], 13, UiKit.Dim, TextAnchor.MiddleLeft);
            UiKit.Bar(new Rect(row.x + 58 * S, row.y + 23 * S, w - 140 * S, 10 * S), left / buffs.duration, col);
            UiKit.Label(new Rect(row.xMax - 70 * S, row.y, 58 * S, row.height), $"{left:F0}초", 16, Color.white, TextAnchor.MiddleRight);
        }
    }

    // 가운데 아래쪽에 아이템 획득 같은 짧은 알림을 알약 모양으로 쌓는다
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
            float pw = tw + iw + 44 * S, ph = 46 * S;
            var r = new Rect((Screen.width - pw) * 0.5f, y + rise, pw, ph);

            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            UiKit.Panel(r, 23, new Color(0.04f, 0.06f, 0.10f, 0.85f));
            UiKit.Round(new Rect(r.x + 5 * S, r.y + 5 * S, 5 * S, r.height - 10 * S), new Color(p.color.r, p.color.g, p.color.b, a), 3 * S);
            if (hasIcon)
                GUI.DrawTexture(new Rect(r.x + 20 * S, r.y + 2 * S, 42 * S, 42 * S), Icon(p.icon), ScaleMode.ScaleToFit, true);
            GUI.color = old;

            var c = p.color; c.a = a;
            UiKit.Label(new Rect(r.x + 20 * S + iw, r.y, tw + 8 * S, r.height), p.text, 24, c, TextAnchor.MiddleLeft);
            y -= 54 * S;
        }
    }

    void DrawEndBanner()
    {
        bool win = waves.state == WaveManager.State.Victory;
        if (!win) { endShownAt = -1f; return; } // 패배 화면은 EndingUI가 그린다
        if (endShownAt < 0) endShownAt = Time.unscaledTime;
        float a = Mathf.Clamp01((Time.unscaledTime - endShownAt) / 0.5f);

        Color accent = win ? GoldColor : UiKit.Danger;
        float h = 170 * S, y = Screen.height * 0.34f;
        var band = new Rect(0, y, Screen.width, h);
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, a);
        UiKit.Round(band, new Color(0, 0, 0, 0.68f), 0);
        UiKit.Round(new Rect(0, y, Screen.width, 3 * S), accent, 0);
        UiKit.Round(new Rect(0, y + h - 3 * S, Screen.width, 3 * S), accent, 0);
        GUI.color = old;

        var c = accent; c.a = a;
        UiKit.Label(new Rect(0, y + 14 * S, Screen.width, 100 * S), win ? "VICTORY" : "DEFEAT", 84, c);
        UiKit.Label(new Rect(0, y + 112 * S, Screen.width, 40 * S), win ? "모든 게이트를 파괴하고 코어를 지켜냈습니다" : "코어가 파괴되었습니다",
                    24, new Color(1, 1, 1, a));
    }
}
