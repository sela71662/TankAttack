using UnityEngine;

// 화면 위쪽 가운데에 잠깐 뜨는 큰 알림 (게이트 파괴 등). 씬에 따로 놓지 않아도 처음 Show 할 때 스스로 생긴다.
// MenuKit의 밀리터리 HUD 스타일: 각진 패널, 위쪽 색 띠, 굵은 영문 제목
public class NoticeUI : MonoBehaviour
{
    static NoticeUI instance;

    string text;
    string sub;
    Color color;
    float startTime;
    const float Duration = 3.0f;

    public static void Show(string text, string sub, Color color)
    {
        if (instance == null)
        {
            var go = new GameObject("NoticeUI");
            instance = go.AddComponent<NoticeUI>();
        }
        instance.text = text.ToUpperInvariant();
        instance.sub = sub;
        instance.color = color;
        instance.startTime = Time.unscaledTime;
    }

    void OnGUI()
    {
        float t = Time.unscaledTime - startTime;
        if (string.IsNullOrEmpty(text) || t > Duration || MapView.IsOpen) return;

        // 0.25초 동안 위에서 내려오며 나타나고 마지막 0.6초 동안 사라진다
        float alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((Duration - t) / 0.6f);
        float s = UiKit.S;
        float slide = (1f - Mathf.Clamp01(t / 0.25f)) * -24f * s;

        float tw = Mathf.Max(MenuKit.TextWidth(text, 48, MenuKit.Bold), string.IsNullOrEmpty(sub) ? 0 : MenuKit.TextWidth(sub, 30, MenuKit.Bold));
        float w = tw + 140 * s, h = string.IsNullOrEmpty(sub) ? 86 * s : 130 * s;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.16f + slide, w, h);

        MenuKit.A = alpha;
        MenuKit.HudPanel(r, color);
        MenuKit.Fill(new Rect(r.x, r.y, r.width, 4 * s), color); // 위쪽 색 띠
        MenuKit.Fill(new Rect(r.x - 28 * s, r.y + h * 0.5f, 22 * s, 3 * s), color); // 양옆 짧은 선
        MenuKit.Fill(new Rect(r.xMax + 6 * s, r.y + h * 0.5f, 22 * s, 3 * s), color);

        MenuKit.Text(new Rect(r.x, r.y + 12 * s, r.width, 62 * s), text, 48, color, MenuKit.Bold, TextAnchor.MiddleCenter);
        if (!string.IsNullOrEmpty(sub))
            MenuKit.Text(new Rect(r.x, r.y + 76 * s, r.width, 40 * s), sub, 30, MenuKit.Paper, MenuKit.Bold, TextAnchor.MiddleCenter);
        MenuKit.A = 1f;
    }
}
