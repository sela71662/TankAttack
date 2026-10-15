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

    static readonly Color Panel = new Color(0.05f, 0.07f, 0.1f, 0.72f);
    static readonly Color GoldColor = new Color(1.0f, 0.82f, 0.2f);

    static readonly string[] itemNames = { "ATTACK UP", "FIRE RATE UP", "HEAL", "SPEED UP" };

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
        else Fill(r, fallback);
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
        string text = type == ItemType.Heal ? $"HEAL +{healRatio * 100:F0}%" : $"{itemNames[(int)type]}  {duration:F0}s";
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
    }

    float S => Screen.height / 1080f;

    GUIStyle Style(int size, TextAnchor anchor, FontStyle fs = FontStyle.Bold)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, Mathf.RoundToInt(size * S)),
            alignment = anchor,
            fontStyle = fs,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Bar(Rect r, float ratio, Color fill)
    {
        Fill(r, new Color(0, 0, 0, 0.6f));
        Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(ratio), r.height - 2), fill);
    }

    // 그림자를 깔아 어떤 배경에서도 읽히게 한다
    static void Text(Rect r, string text, GUIStyle style, Color color)
    {
        var old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.8f * color.a);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (waves == null) return;
        DrawWaveAndCore();
        DrawGold();
        DrawPlayerAndBuffs();
        DrawToasts();
        DrawEndBanner();
    }

    // 위쪽 가운데: 웨이브 진행과 코어 체력
    void DrawWaveAndCore()
    {
        float w = 460 * S, h = 92 * S;
        var box = new Rect((Screen.width - w) * 0.5f, 10 * S, w, h);
        Fill(box, Panel);

        string line = waves.state switch
        {
            WaveManager.State.Waiting => "READY",
            WaveManager.State.Intermission => $"NEXT WAVE IN {Mathf.CeilToInt(waves.intermissionLeft)}s",
            WaveManager.State.Victory => "VICTORY",
            WaveManager.State.Defeat => "DEFEAT",
            _ => $"WAVE {waves.currentWave}/{waves.waves.Count}    ENEMIES {waves.AliveCount}"
        };
        Text(new Rect(box.x, box.y + 4 * S, w, 34 * S), line, Style(26, TextAnchor.MiddleCenter), Color.white);

        var core = waves.coreHealth;
        if (core != null)
        {
            float ratio = core.maxHp > 0 ? core.hp / core.maxHp : 0;
            var bar = new Rect(box.x + 16 * S, box.y + 46 * S, w - 32 * S, 18 * S);
            Bar(bar, ratio, Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(0.2f, 0.9f, 0.9f), ratio));
            Text(bar, $"CORE  {Mathf.CeilToInt(core.hp)} / {Mathf.CeilToInt(core.maxHp)}", Style(15, TextAnchor.MiddleCenter), Color.white);
        }
    }

    // 오른쪽 위: 골드와 늘고 준 양
    void DrawGold()
    {
        float w = 230 * S, h = 52 * S;
        var box = new Rect(Screen.width - w - 14 * S, 10 * S, w, h);
        Fill(box, Panel);

        // 동전 아이콘
        float d = 40 * S;
        var coin = new Rect(box.x + 10 * S, box.y + (h - d) * 0.5f, d, d);
        DrawIcon(coin, "Coin", GoldColor);

        Text(new Rect(coin.xMax + 10 * S, box.y, w - d - 34 * S, h), $"{GoldWallet.Gold}", Style(30, TextAnchor.MiddleLeft), GoldColor);

        for (int i = 0; i < goldPops.Count; i++)
        {
            var p = goldPops[i];
            float t = (Time.unscaledTime - p.time) / PopupLife;
            var c = p.color; c.a = 1f - t * t;
            Text(new Rect(box.x, box.yMax + 4 * S + t * 22 * S + i * 20 * S, w - 14 * S, 26 * S), p.text, Style(22, TextAnchor.MiddleRight), c);
        }
    }

    // 왼쪽 아래: 플레이어 체력, 아군 강화 단계, 아이템 효과 남은 시간
    void DrawPlayerAndBuffs()
    {
        var player = PlayerTankController.Instance;
        if (player == null) return;

        float w = 300 * S, x = 14 * S;
        float y = Screen.height - 150 * S;

        var buffs = player.GetComponent<PlayerBuffs>();
        if (buffs != null)
        {
            foreach (ItemType t in new[] { ItemType.Damage, ItemType.FireRate, ItemType.Speed })
            {
                float left = buffs.Remaining(t);
                if (left <= 0) continue;
                y -= 40 * S;
                var row = new Rect(x, y, w, 34 * S);
                Fill(row, Panel);
                var col = ItemPickup.ColorOf(t);
                Fill(new Rect(row.x, row.y, 46 * S, row.height), col * 0.35f);
                DrawIcon(new Rect(row.x + 4 * S, row.y + 1 * S, 38 * S, row.height - 2 * S), t.ToString(), col);
                Bar(new Rect(row.x + 54 * S, row.y + 10 * S, w - 120 * S, 14 * S), left / buffs.duration, col);
                Text(new Rect(row.xMax - 58 * S, row.y, 52 * S, row.height), $"{left:F0}s", Style(16, TextAnchor.MiddleRight), Color.white);
            }
        }

        var hp = player.GetComponent<Health>();
        float by = Screen.height - 140 * S;
        var panel = new Rect(x, by, w, 62 * S);
        Fill(panel, Panel);
        if (hp != null)
        {
            float ratio = hp.maxHp > 0 ? hp.hp / hp.maxHp : 0;
            var bar = new Rect(panel.x + 10 * S, panel.y + 10 * S, w - 20 * S, 22 * S);
            Bar(bar, ratio, Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(0.25f, 0.9f, 0.3f), ratio));
            Text(bar, $"HP  {Mathf.CeilToInt(hp.hp)} / {Mathf.CeilToInt(hp.maxHp)}", Style(15, TextAnchor.MiddleCenter), Color.white);
        }
        string up = AllyUpgrades.CanUpgrade ? $"ALLY UPGRADE  Lv {AllyUpgrades.Level}/{AllyUpgrades.MaxLevel}" : "ALLY UPGRADE  MAX";
        Text(new Rect(panel.x + 10 * S, panel.y + 34 * S, w - 20 * S, 24 * S), up, Style(15, TextAnchor.MiddleLeft), new Color(0.8f, 0.9f, 1f));
    }

    // 가운데 아래쪽에 아이템 획득 같은 짧은 알림을 쌓는다
    void DrawToasts()
    {
        float y = Screen.height * 0.62f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var p = toasts[i];
            float t = (Time.unscaledTime - p.time) / ToastLife;
            var c = p.color; c.a = Mathf.Clamp01(1f - Mathf.Max(0, t - 0.7f) / 0.3f);
            Text(new Rect(0, y, Screen.width, 40 * S), p.text, Style(26, TextAnchor.MiddleCenter), c);
            if (p.icon != null && Icon(p.icon) != null)
            {
                // 글자 왼쪽에 아이콘. 글자 폭을 재서 붙인다
                float tw = Style(26, TextAnchor.MiddleCenter).CalcSize(new GUIContent(p.text)).x;
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, c.a);
                GUI.DrawTexture(new Rect((Screen.width - tw) * 0.5f - 46 * S, y - 2 * S, 42 * S, 42 * S), Icon(p.icon), ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }
            y -= 44 * S;
        }
    }

    void DrawEndBanner()
    {
        if (waves.state != WaveManager.State.Victory && waves.state != WaveManager.State.Defeat) return;
        bool win = waves.state == WaveManager.State.Victory;
        Fill(new Rect(0, Screen.height * 0.36f, Screen.width, 120 * S), new Color(0, 0, 0, 0.6f));
        Text(new Rect(0, Screen.height * 0.36f, Screen.width, 120 * S), win ? "VICTORY" : "DEFEAT",
             Style(72, TextAnchor.MiddleCenter), win ? GoldColor : new Color(1f, 0.35f, 0.3f));
    }
}
