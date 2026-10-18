using UnityEngine;

// 인트로 화면: 제목, 한 줄 설정, 버튼 3개(START / 조작법 / EXIT). Intro 씬의 아무 오브젝트에 붙인다.
// 배경은 MapBackdrop이 메인 씬을 함께 불러와 코어 주위를 도는 카메라로 보여 준다
public class IntroUI : MonoBehaviour
{
    static readonly string[,] Controls =
    {
        { "W / S", "전진 / 후진" },
        { "A / D", "차체 회전" },
        { "마우스", "조준 (화면 속 조준선)" },
        { "좌클릭", "발사" },
        { "M", "전체 지도" },
        { "1 ~ 5", "아군 소환" },
        { "R", "코어 수리" },
    };

    float openedAt;
    bool showControls; // 조작법 패널이 열려 있는가
    MapBackdrop backdrop;

    void Awake()
    {
        GameFlow.InMenuScene = true;
        backdrop = MapBackdrop.Ensure(gameObject);
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
        if (Input.GetKeyDown(KeyCode.Escape) && showControls) { showControls = false; return; }
        if (showControls) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) GameFlow.Restart();
    }

    void OnGUI()
    {
        float s = UiKit.S;
        float cx = Screen.width * 0.5f;
        float a = Mathf.Clamp01((Time.unscaledTime - openedAt) / 1.0f);

        // 맵을 불러오기 전에는 검은 화면, 불러온 뒤에는 서서히 비쳐 보이는 어두운 막
        float cover = 1f;
        if (backdrop.Ready) cover = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((Time.unscaledTime - backdrop.ReadyAt) / 1.5f));
        UiKit.Round(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.04f, 0.08f, cover), 0);
        UiKit.Round(new Rect(0, 0, Screen.width, Screen.height * 0.5f), new Color(0.10f, 0.18f, 0.28f, 0.25f), 0);

        float y = Screen.height * 0.14f;
        UiKit.Label(new Rect(0, y, Screen.width, 120 * s), "TANK ATTACK", 104, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a));
        UiKit.Label(new Rect(0, y + 120 * s, Screen.width, 44 * s), "섬의 에너지 코어를 지켜라", 28, new Color(1, 1, 1, a));
        UiKit.Label(new Rect(0, y + 166 * s, Screen.width, 36 * s), "다섯 번의 웨이브를 막고, 두 게이트를 파괴하라", 20,
                    new Color(UiKit.Dim.r, UiKit.Dim.g, UiKit.Dim.b, a));

        if (showControls) DrawControls(a);
        else DrawMenu(cx, Screen.height * 0.46f, a);
    }

    // 메인 메뉴: START / 조작법 / EXIT
    void DrawMenu(float cx, float top, float a)
    {
        float s = UiKit.S;
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, a);
        float w = 320 * s, h = 72 * s, gap = 22 * s;
        if (UiKit.Button(new Rect(cx - w * 0.5f, top, w, h), "START", 34)) GameFlow.Restart();
        if (UiKit.Button(new Rect(cx - w * 0.5f, top + (h + gap), w, h), "조작법", 30)) showControls = true;
        if (UiKit.Button(new Rect(cx - w * 0.5f, top + (h + gap) * 2, w, h), "EXIT", 30)) Quit();
        GUI.color = old;
        UiKit.Label(new Rect(0, top + (h + gap) * 3, Screen.width, 30 * s), "Enter / Space : START", 16,
                    new Color(UiKit.Dim.r, UiKit.Dim.g, UiKit.Dim.b, 0.7f * a));
    }

    // 조작법 패널 + 돌아가기 버튼
    void DrawControls(float a)
    {
        float s = UiKit.S;
        float cx = Screen.width * 0.5f;
        float rowH = 40 * s;
        float panelW = 460 * s, panelH = (Controls.GetLength(0) * rowH) + 40 * s;
        var panel = new Rect(cx - panelW * 0.5f, Screen.height * 0.44f, panelW, panelH);
        UiKit.Panel(panel, 16);
        for (int i = 0; i < Controls.GetLength(0); i++)
        {
            var row = new Rect(panel.x + 30 * s, panel.y + 20 * s + i * rowH, panel.width - 60 * s, rowH);
            UiKit.Label(row, Controls[i, 0], 22, UiKit.Accent, TextAnchor.MiddleLeft);
            UiKit.Label(row, Controls[i, 1], 22, Color.white, TextAnchor.MiddleRight);
        }
        if (UiKit.Button(new Rect(cx - 130 * s, panel.yMax + 30 * s, 260 * s, 64 * s), "BACK", 28)) showControls = false;
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
