using UnityEngine;

// 인트로 화면: 스텐실 제목, 메뉴 3개(START / CONTROLS / EXIT). Intro 씬의 아무 오브젝트에 붙인다.
// 배경은 MapBackdrop이 메인 씬을 함께 불러와 코어 주위를 도는 카메라로 보여 준다. 레이아웃은 MenuKit의 밀리터리 HUD 스타일
public class IntroUI : MonoBehaviour
{
    static readonly string[,] Controls =
    {
        { "W  S", "전진 / 후진" },
        { "A  D", "차체 회전" },
        { "MOUSE", "조준 (화면 속 조준선)" },
        { "L-CLICK", "발사" },
        { "M", "전체 지도" },
        { "1 - 5", "아군 소환" },
        { "R", "코어 수리" },
    };

    float openedAt;
    bool showControls; // 조작법 패널이 열려 있는가
    MapBackdrop backdrop;
    MenuList mainMenu;
    MenuList backMenu;
    readonly ScreenFader fader = new ScreenFader();

    void Awake()
    {
        GameFlow.InMenuScene = true;
        backdrop = MapBackdrop.Ensure(gameObject);
        mainMenu = new MenuList(new[] { "START", "CONTROLS", "EXIT" }, new[] { "시작", "조작법", "종료" });
        backMenu = new MenuList(new[] { "BACK" }, new[] { "돌아가기" });
    }

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        openedAt = Time.unscaledTime;
    }

    void Update()
    {
        fader.Tick();
        if (fader.Busy) return;

        if (showControls)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || backMenu.HandleKeys() == 0) showControls = false;
            return;
        }
        Activate(mainMenu.HandleKeys());
    }

    void Activate(int index)
    {
        if (index == 0) fader.Go(GameFlow.Restart);
        else if (index == 1) showControls = true;
        else if (index == 2) fader.Go(Quit);
    }

    void OnGUI()
    {
        float s = UiKit.S;
        float W = Screen.width, H = Screen.height;
        float a = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.8f);
        MenuKit.A = a;

        // 맵을 불러오기 전에는 검은 화면, 불러온 뒤에는 서서히 걷히는 막
        float black = backdrop.Ready ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - backdrop.ReadyAt) / 1.4f)) : 1f;

        MenuKit.Shade();
        MenuKit.ScreenFrame();

        float x = 130 * s;
        MenuKit.VLine(x - 34 * s, 78 * s, H - 156 * s, MenuKit.Line);

        // 제목 블록
        MenuKit.Tag(x, 84 * s, "ISLAND DEFENSE  //  5 WAVES", MenuKit.Amber);
        if (showControls)
        {
            // 조작법을 볼 때는 제목을 작게 줄여 자리를 비운다
            MenuKit.Text(new Rect(x - 3 * s, H * 0.11f, W, 80 * s), "TANK ATTACK", 66, MenuKit.Paper, MenuKit.TitleFont);
            DrawControls(x, H * 0.30f);
        }
        else
        {
            float ty = H * 0.14f;
            MenuKit.Text(new Rect(x - 6 * s, ty, W, 150 * s), "TANK", 138, MenuKit.Paper, MenuKit.TitleFont);
            MenuKit.Text(new Rect(x - 6 * s, ty + 128 * s, W, 150 * s), "ATTACK", 138, MenuKit.Amber, MenuKit.TitleFont);
            MenuKit.Fill(new Rect(x, ty + 288 * s, 96 * s, 4 * s), MenuKit.Amber);
            MenuKit.Text(new Rect(x, ty + 300 * s, 900 * s, 34 * s), MenuKit.Spaced("DEFEND THE ENERGY CORE"), 24, MenuKit.Paper, MenuKit.Medium);
            MenuKit.Text(new Rect(x, ty + 334 * s, 900 * s, 34 * s), "섬의 에너지 코어를 지켜라", 20, MenuKit.Dim);

            Activate(mainMenu.Draw(new Rect(x, H * 0.62f, 560 * s, 74 * s * 3), 74 * s, !fader.Busy));
        }

        // 하단 안내
        string hint = showControls ? "ESC  BACK" : "W / S  MOVE     ENTER  SELECT";
        MenuKit.Text(new Rect(x, H - 92 * s, 900 * s, 30 * s), MenuKit.Spaced(hint), 16, MenuKit.Dim, MenuKit.Medium);

        MenuKit.Wash(new Color(0f, 0f, 0f, Mathf.Max(black, fader.Alpha)));
    }

    // 조작법: 키 표시 + 설명, 아래에 BACK
    void DrawControls(float x, float y)
    {
        float s = UiKit.S;
        float rowH = 50 * s;
        float w = 560 * s;
        var panel = new Rect(x, y, w, 70 * s + Controls.GetLength(0) * rowH + 8 * s);

        MenuKit.Text(new Rect(x + 22 * s, y + 8 * s, w, 44 * s), "CONTROLS", 34, MenuKit.Amber, MenuKit.Bold);
        MenuKit.Text(new Rect(x, y + 8 * s, w - 22 * s, 44 * s), "조작법", 20, MenuKit.Dim, null, TextAnchor.MiddleRight);
        MenuKit.HLine(x, y + 58 * s, w, MenuKit.Line);

        for (int i = 0; i < Controls.GetLength(0); i++)
        {
            float ry = y + 70 * s + i * rowH;
            MenuKit.KeyCap(new Rect(x + 22 * s, ry + 4 * s, 130 * s, rowH - 10 * s), Controls[i, 0]);
            MenuKit.Text(new Rect(x + 176 * s, ry, w - 176 * s, rowH - 4 * s), Controls[i, 1], 22, MenuKit.Paper);
        }

        float by = panel.yMax + 14 * s;
        if (backMenu.Draw(new Rect(x, by, w, 74 * s), 74 * s, !fader.Busy) == 0) showControls = false;
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
