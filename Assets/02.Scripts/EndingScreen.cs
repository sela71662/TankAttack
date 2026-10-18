using UnityEngine;

// 엔딩 씬 화면. GameFlow.LastEnd의 결과에 따라 승리 / 코어 파괴 / 목숨 소진 화면을 보여 준다.
// 배경은 MapBackdrop이 불러온 메인 씬이다. 코어 파괴는 그 위에 폭발과 연기를 새로 재생한다.
// 레이아웃은 인트로와 같은 MenuKit의 밀리터리 HUD 스타일: 왼쪽에 결과, 통계, 메뉴
public class EndingScreen : MonoBehaviour
{
    float openedAt;
    MapBackdrop backdrop;
    GameFlow.EndStats stats;
    MenuList menu;
    readonly ScreenFader fader = new ScreenFader();

    void Awake()
    {
        GameFlow.InMenuScene = true;
        stats = GameFlow.LastEnd;
        backdrop = MapBackdrop.Ensure(gameObject);

        // 결과마다 카메라 거리와 속도를 달리해 분위기를 바꾼다
        switch (stats.result)
        {
            case GameFlow.EndResult.Victory:
                backdrop.orbitRadius = 110f; backdrop.orbitHeight = 35f; backdrop.orbitSpeed = 5f;
                break;
            case GameFlow.EndResult.CoreDestroyed:
                backdrop.orbitRadius = 70f; backdrop.orbitHeight = 25f; backdrop.orbitSpeed = 3f;
                break;
            default:
                backdrop.orbitRadius = 130f; backdrop.orbitHeight = 45f; backdrop.orbitSpeed = 3f;
                break;
        }
        backdrop.onReady += OnMapReady;

        bool dead = stats.result == GameFlow.EndResult.PlayerDead;
        menu = new MenuList(new[] { dead ? "RESTART" : "RETRY", "MAIN MENU" }, new[] { "다시 시작", "메인 메뉴" });
    }

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        openedAt = Time.unscaledTime;
    }

    void OnDestroy()
    {
        if (backdrop != null) backdrop.onReady -= OnMapReady;
    }

    // 코어 파괴: 코어를 감추고 그 자리에서 폭발 + 계속 피어오르는 연기
    void OnMapReady()
    {
        if (stats.result != GameFlow.EndResult.CoreDestroyed || backdrop.Core == null) return;

        foreach (var r in backdrop.Core.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var gate = FindFirstObjectByType<SpawnGate>();
        if (gate == null) return;

        Vector3 pos = backdrop.Core.position;
        if (gate.destroyEffect != null)
        {
            var blast = Instantiate(gate.destroyEffect, pos, Quaternion.identity);
            SpawnGate.ScaleEffect(blast, gate.blastScale);
            Destroy(blast, 6.0f);
        }
        if (gate.smokeEffect != null)
        {
            // 연기는 코어 아래 지면에서 올라온다
            Vector3 ground = Physics.Raycast(pos, Vector3.down, out var hit, 100f) ? hit.point : pos;
            var smoke = Instantiate(gate.smokeEffect, ground, Quaternion.identity);
            SpawnGate.ScaleEffect(smoke, gate.smokeScale);
            foreach (var ps in smoke.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        }
    }

    void Update()
    {
        fader.Tick();
        if (fader.Busy) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { fader.Go(GameFlow.ToIntro); return; }
        Activate(menu.HandleKeys());
    }

    void Activate(int index)
    {
        if (index == 0) fader.Go(GameFlow.Restart);
        else if (index == 1) fader.Go(GameFlow.ToIntro);
    }

    void OnGUI()
    {
        float s = UiKit.S;
        float W = Screen.width, H = Screen.height;
        bool win = stats.result == GameFlow.EndResult.Victory;
        bool core = stats.result == GameFlow.EndResult.CoreDestroyed;
        Color accent = win ? MenuKit.Amber : MenuKit.Alert;

        float t = Time.unscaledTime - openedAt;
        MenuKit.A = Mathf.Clamp01(t / 0.8f);

        // 맵을 불러오기 전에는 검은 화면, 불러온 뒤에는 서서히 걷히는 막
        float black = backdrop.Ready ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - backdrop.ReadyAt) / 1.4f)) : 1f;

        MenuKit.Shade(win ? 0.88f : 0.94f, win ? 0.45f : 0.65f);
        if (core) MenuKit.Wash(new Color(0.30f, 0.02f, 0.02f, 0.22f)); // 코어 파괴: 붉은 기운
        else if (!win) MenuKit.Wash(new Color(0f, 0f, 0f, 0.35f));    // 목숨 소진: 어둡게
        MenuKit.ScreenFrame();

        float x = 130 * s;
        MenuKit.VLine(x - 34 * s, 78 * s, H - 156 * s, MenuKit.Line);

        // 결과
        MenuKit.Tag(x, 84 * s, "MISSION REPORT", accent);
        string title = win ? "VICTORY" : core ? "CORE LOST" : "YOU DIED";
        string sub = win ? "모든 게이트를 파괴하고 코어를 지켜냈습니다"
                   : core ? "코어가 파괴되었습니다"
                   : "플레이어가 모든 목숨을 잃었습니다";
        MenuKit.Text(new Rect(x - 5 * s, H * 0.12f, W, 130 * s), title, 112, accent, MenuKit.TitleFont);
        MenuKit.Fill(new Rect(x, H * 0.12f + 142 * s, 96 * s, 4 * s), accent);
        MenuKit.Text(new Rect(x, H * 0.12f + 158 * s, 1200 * s, 36 * s), sub, 24, MenuKit.Paper);

        // 통계: 한 줄씩 차례로 나타나고 숫자가 올라간다
        string[] names = { "도달 웨이브", "처치한 적", "보유 골드", "파괴한 게이트" };
        string[] tags = { "WAVE", "KILLS", "GOLD", "GATES" };
        int[] values = { stats.wave, stats.kills, stats.gold, stats.gates };
        string[] tails = { " / " + stats.totalWaves, "", "", " / 2" };
        float sy = H * 0.12f + 232 * s;
        float w = 560 * s, rowH = 58 * s;
        for (int i = 0; i < names.Length; i++)
        {
            float k = Mathf.Clamp01((t - 0.5f - i * 0.15f) / 0.5f);
            float ek = 1f - (1f - k) * (1f - k);
            float prev = MenuKit.A;
            MenuKit.A = prev * k;

            float ry = sy + i * rowH;
            MenuKit.Text(new Rect(x + 4 * s, ry, 160 * s, rowH - 6 * s), MenuKit.Spaced(tags[i]), 16, MenuKit.Dim, MenuKit.Medium);
            MenuKit.Text(new Rect(x + 130 * s, ry, 260 * s, rowH - 6 * s), names[i], 20, MenuKit.Dim);
            MenuKit.Text(new Rect(x, ry, w - 6 * s, rowH - 6 * s), Mathf.RoundToInt(values[i] * ek) + tails[i], 38, MenuKit.Paper, MenuKit.Bold, TextAnchor.MiddleRight);
            MenuKit.HLine(x, ry + rowH - 4 * s, w, MenuKit.Line);

            MenuKit.A = prev;
        }

        Activate(menu.Draw(new Rect(x, sy + names.Length * rowH + 40 * s, w, 74 * s * 2), 74 * s, !fader.Busy && t > 1.0f));

        MenuKit.Text(new Rect(x, H - 92 * s, 900 * s, 30 * s), MenuKit.Spaced("W / S  MOVE     ENTER  SELECT     ESC  MENU"), 16, MenuKit.Dim, MenuKit.Medium);

        MenuKit.Wash(new Color(0f, 0f, 0f, Mathf.Max(black, fader.Alpha)));
    }
}
