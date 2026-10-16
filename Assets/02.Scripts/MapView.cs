using UnityEngine;

// M키로 섬 전체를 위에서 내려다보는 전체 맵을 켜고 끈다. 씬 설정 없이 플레이 시작 때 스스로 만들어진다
public class MapView : MonoBehaviour
{
    public static bool IsOpen;

    public Vector3 center = new Vector3(-135.0f, 150.0f, 90.0f); // 카메라 위치 (맵 중심 위쪽)
    public float orthoSize = 140.0f;                             // 세로 절반 길이 (m)

    Camera mapCam;
    GUIStyle label;
    Texture2D white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("MapView");
        go.AddComponent<MapView>();
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
        mapCam.backgroundColor = new Color(0.05f, 0.08f, 0.12f);
        mapCam.depth = 100.0f; // 메인 카메라 위에 그린다
        mapCam.enabled = false;
        var listener = go.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);

        white = Texture2D.whiteTexture;
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            IsOpen = !IsOpen;
            if (mapCam != null) mapCam.enabled = IsOpen;
        }
    }

    void Box(Vector2 p, float size, Color fill, Color border)
    {
        float s = size * Screen.height / 1080.0f;
        float b = Mathf.Max(1.0f, s * 0.2f);
        GUI.color = border;
        GUI.DrawTexture(new Rect(p.x - s * 0.5f - b, p.y - s * 0.5f - b, s + b * 2, s + b * 2), white);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), white);
    }

    void OnGUI()
    {
        if (!IsOpen || mapCam == null) return;
        if (Event.current.type != EventType.Repaint) return;

        var player = PlayerTankController.Instance;
        foreach (var t in TeamMember.All)
        {
            if (t == null) continue;
            Vector3 sp = mapCam.WorldToScreenPoint(t.transform.position);
            if (sp.z <= 0.0f) continue;
            var p = new Vector2(sp.x, Screen.height - sp.y);
            bool ally = TeamConfig.IsAlly(t.team);
            Color c = ally ? new Color(1.0f, 0.85f, 0.1f) : new Color(1.0f, 0.15f, 0.1f);

            if (t.isStructure) Box(p, 22.0f, c, Color.black);
            else if (player != null && t.gameObject == player.gameObject) Box(p, 16.0f, c, Color.white);
            else Box(p, 11.0f, c, Color.black);
        }
        GUI.color = Color.white;

        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label);
            label.alignment = TextAnchor.UpperCenter;
            label.fontStyle = FontStyle.Bold;
            label.normal.textColor = Color.white;
        }
        label.fontSize = Mathf.RoundToInt(28.0f * Screen.height / 1080.0f);
        GUI.Label(new Rect(0, 10.0f * Screen.height / 1080.0f, Screen.width, 50.0f * Screen.height / 1080.0f), "전체 맵   (M: 닫기)", label);
    }
}
