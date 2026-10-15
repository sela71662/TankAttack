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
        if (string.IsNullOrEmpty(text) || t > Duration) return;

        // 0.2초 동안 나타나고 마지막 0.6초 동안 사라진다
        float alpha = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((Duration - t) / 0.6f);
        float y = Screen.height * 0.18f;

        var big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var small = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        var old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.7f * alpha);
        GUI.Label(new Rect(2, y + 2, Screen.width, 60), text, big);   // 그림자
        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.Label(new Rect(0, y, Screen.width, 60), text, big);
        if (!string.IsNullOrEmpty(sub))
            GUI.Label(new Rect(0, y + 56, Screen.width, 32), sub, small);
        GUI.color = old;
    }
}
