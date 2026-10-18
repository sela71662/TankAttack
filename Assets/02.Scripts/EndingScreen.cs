using UnityEngine;

// 엔딩 씬 화면. GameFlow.LastEnd의 결과에 따라 승리 / 코어 파괴 / 목숨 소진 화면을 보여 준다.
// 배경은 MapBackdrop이 불러온 메인 씬이다. 코어 파괴는 그 위에 폭발과 연기를 새로 재생한다
public class EndingScreen : MonoBehaviour
{
    float openedAt;
    MapBackdrop backdrop;
    GameFlow.EndStats stats;

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
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) GameFlow.Restart();
        if (Input.GetKeyDown(KeyCode.Escape)) GameFlow.ToIntro();
    }

    void OnGUI()
    {
        float s = UiKit.S;
        float cx = Screen.width * 0.5f;
        bool win = stats.result == GameFlow.EndResult.Victory;
        bool core = stats.result == GameFlow.EndResult.CoreDestroyed;

        // 맵을 불러오기 전에는 가린 채, 불러온 뒤 결과 색의 막으로 서서히 바뀐다
        Color cover = win ? new Color(0.02f, 0.04f, 0.08f, 0.30f)
                    : core ? new Color(0.25f, 0.02f, 0.02f, 0.45f)
                    : new Color(0f, 0f, 0f, 0.78f);
        float k = backdrop.Ready ? Mathf.Clamp01((Time.unscaledTime - backdrop.ReadyAt) / 1.5f) : 0f;
        UiKit.Round(new Rect(0, 0, Screen.width, Screen.height),
                    new Color(Mathf.Lerp(0f, cover.r, k), Mathf.Lerp(0f, cover.g, k), Mathf.Lerp(0f, cover.b, k), Mathf.Lerp(1f, cover.a, k)), 0);

        float a = Mathf.Clamp01((Time.unscaledTime - openedAt) / 1.0f);
        string title = win ? "VICTORY" : core ? "CORE DESTROYED" : "YOU DIED";
        string sub = win ? "모든 게이트를 파괴하고 코어를 지켜냈습니다"
                   : core ? "코어가 파괴되었습니다"
                   : "플레이어가 모든 목숨을 잃었습니다";
        Color accent = win ? UiKit.Gold : UiKit.Danger;

        float y = Screen.height * 0.10f;
        UiKit.Label(new Rect(0, y, Screen.width, 110 * s), title, 88, new Color(accent.r, accent.g, accent.b, a));
        UiKit.Label(new Rect(0, y + 110 * s, Screen.width, 44 * s), sub, 26, new Color(1, 1, 1, a));
        y += 190 * s;

        DrawStats(new Rect(cx - 220 * s, y, 440 * s, 230 * s), a);
        y += 270 * s;

        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, a);
        if (UiKit.Button(new Rect(cx - 250 * s, y, 230 * s, 64 * s), stats.result == GameFlow.EndResult.PlayerDead ? "RESTART" : "RETRY", 28)) GameFlow.Restart();
        if (UiKit.Button(new Rect(cx + 20 * s, y, 230 * s, 64 * s), "MAIN MENU", 28)) GameFlow.ToIntro();
        GUI.color = old;
    }

    void DrawStats(Rect r, float a)
    {
        float s = UiKit.S;
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, a);
        UiKit.Panel(r, 16);
        GUI.color = old;

        string[] names = { "도달 웨이브", "처치한 적", "보유 골드", "파괴한 게이트" };
        string[] values = { $"{stats.wave} / {stats.totalWaves}", $"{stats.kills}", $"{stats.gold}", $"{stats.gates} / 2" };
        for (int i = 0; i < names.Length; i++)
        {
            var row = new Rect(r.x + 28 * s, r.y + 18 * s + i * 50 * s, r.width - 56 * s, 44 * s);
            UiKit.Label(row, names[i], 22, new Color(UiKit.Dim.r, UiKit.Dim.g, UiKit.Dim.b, a), TextAnchor.MiddleLeft);
            UiKit.Label(row, values[i], 26, new Color(1, 1, 1, a), TextAnchor.MiddleRight);
        }
    }
}
