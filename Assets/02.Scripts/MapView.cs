using UnityEngine;

// M키로 섬 전체를 위에서 내려다보는 전체 맵을 켜고 끈다. 씬 설정 없이 플레이 시작 때 스스로 만들어진다
public class MapView : MonoBehaviour
{
    public static bool IsOpen;

    public Vector3 center = new Vector3(-135.0f, 150.0f, 90.0f); // 카메라 위치 (맵 중심 위쪽)
    public float orthoSize = 140.0f;                             // 세로 절반 길이 (m)

    Camera mapCam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("MapView");
        go.AddComponent<MapView>();
        DontDestroyOnLoad(go); // 인트로 -> 메인, 재시작처럼 씬이 바뀌어도 지도가 남는다
    }

    void Start()
    {
        IsOpen = false;
        var go = new GameObject("Map Camera");
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f); // 아래를 보고 화면 위쪽이 +Z
        mapCam = go.AddComponent<Camera>();
        mapCam.orthographic = true;
        mapCam.orthographicSize = orthoSize;
        mapCam.nearClipPlane = 1.0f;
        mapCam.farClipPlane = 500.0f;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.04f, 0.06f, 0.05f); // MenuKit.Ink 계열의 어두운 색
        mapCam.depth = 100.0f; // 메인 카메라 위에 그린다
        mapCam.enabled = false;
        var listener = go.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    void Update()
    {
        if (GameFlow.InMenuScene) return; // 인트로에서는 지도를 열지 않는다
        if (Input.GetKeyDown(KeyCode.M))
        {
            IsOpen = !IsOpen;
            if (mapCam != null) mapCam.enabled = IsOpen;
        }
    }

    // 팀 마커: 테두리 있는 사각형. size는 1080p 기준 픽셀
    static void Marker(Vector2 p, float size, Color fill, Color border)
    {
        float s = size * UiKit.S;
        float b = Mathf.Max(1.5f, s * 0.18f);
        MenuKit.Fill(new Rect(p.x - s * 0.5f - b, p.y - s * 0.5f - b, s + b * 2, s + b * 2), border);
        MenuKit.Fill(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), fill);
    }

    void OnGUI()
    {
        if (!IsOpen || mapCam == null) return;
        if (Event.current.type != EventType.Repaint) return;

        MenuKit.A = 1f;
        GUI.color = Color.white;
        float S = UiKit.S;
        Color allyColor = MenuKit.Amber, enemyColor = MenuKit.Alert;

        var player = PlayerTankController.Instance;
        foreach (var t in TeamMember.All)
        {
            if (t == null) continue;
            Vector3 sp = mapCam.WorldToScreenPoint(t.transform.position);
            if (sp.z <= 0.0f) continue;
            var p = new Vector2(sp.x, Screen.height - sp.y);
            Color c = TeamConfig.IsAlly(t.team) ? allyColor : enemyColor;

            if (t.isStructure)
            {
                Marker(p, 20.0f, c, MenuKit.Ink);
                float h = 17.0f * S; // 건물은 모서리 괄호로 감싼다
                MenuKit.Brackets(new Rect(p.x - h, p.y - h, h * 2, h * 2), c, 8, 2);
            }
            else if (player != null && t.gameObject == player.gameObject)
            {
                Marker(p, 16.0f, c, MenuKit.Paper);
                float h = 14.0f * S;
                MenuKit.Brackets(new Rect(p.x - h, p.y - h, h * 2, h * 2), MenuKit.Paper, 6, 2);
            }
            else Marker(p, 11.0f, c, MenuKit.Ink);
        }

        // 위쪽 왼쪽: 제목과 범례, 아래: 닫기 안내. 지도 위에서 읽히도록 어두운 판 위에 둔다
        float x = 60 * S, y = 54 * S;
        var legend = new Rect(x - 18 * S, y - 22 * S, 330 * S, 190 * S);
        MenuKit.HudPanel(legend, MenuKit.Amber);
        MenuKit.Tag(x, y, "TACTICAL MAP", MenuKit.Amber);
        MenuKit.Text(new Rect(x, y + 22 * S, 280 * S, 28 * S), "전체 맵", 20, MenuKit.Paper);
        LegendRow(x, y + 62 * S, allyColor, MenuKit.Ink, "ALLY", "아군");
        LegendRow(x, y + 94 * S, enemyColor, MenuKit.Ink, "ENEMY", "적");
        LegendRow(x, y + 126 * S, allyColor, MenuKit.Paper, "YOU", "플레이어");

        float hy = Screen.height - 90 * S;
        MenuKit.KeyCap(new Rect(60 * S, hy, 44 * S, 36 * S), "M");
        MenuKit.Text(new Rect(116 * S, hy, 200 * S, 36 * S), "닫기", 20, MenuKit.Dim);
        MenuKit.ScreenFrame();
    }

    static void LegendRow(float x, float y, Color fill, Color border, string label, string korean)
    {
        float S = UiKit.S;
        Marker(new Vector2(x + 10 * S, y + 14 * S), 14.0f, fill, border);
        MenuKit.Text(new Rect(x + 34 * S, y, 120 * S, 28 * S), MenuKit.Spaced(label), 16, MenuKit.Paper, MenuKit.Medium);
        MenuKit.Text(new Rect(x + 150 * S, y, 150 * S, 28 * S), korean, 16, MenuKit.Dim);
    }
}
