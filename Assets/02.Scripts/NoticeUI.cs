using UnityEngine;

// 화면 위쪽 가운데에 잠깐 뜨는 큰 알림 (게이트 파괴 등). 씬에 따로 놓지 않아도 처음 Show 할 때 스스로 생긴다
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
        instance.text = text;
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

        float tw = Mathf.Max(UiKit.TextWidth(text, 44), string.IsNullOrEmpty(sub) ? 0 : UiKit.TextWidth(sub, 24));
        float w = tw + 120 * s, h = string.IsNullOrEmpty(sub) ? 84 * s : 124 * s;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.16f + slide, w, h);

        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        UiKit.Panel(r, 18, new Color(0.04f, 0.06f, 0.10f, 0.88f));
        UiKit.Round(new Rect(r.x + 18 * s, r.y + 8 * s, r.width - 36 * s, 4 * s), new Color(color.r, color.g, color.b, alpha), 2 * s);
        GUI.color = old;

        UiKit.Label(new Rect(r.x, r.y + 14 * s, r.width, 62 * s), text, 44, new Color(color.r, color.g, color.b, alpha));
        if (!string.IsNullOrEmpty(sub))
            UiKit.Label(new Rect(r.x, r.y + 74 * s, r.width, 36 * s), sub, 24, new Color(1f, 1f, 1f, alpha * 0.9f));
    }
}
