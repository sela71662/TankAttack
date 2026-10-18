using System.Collections.Generic;
using UnityEngine;

// IMGUI HUD가 함께 쓰는 그리기 도우미: 둥근 패널, 그림자, 그라데이션 막대, 그림자 글자
public static class UiKit
{
    public static float S => Screen.height / 1080f;

    public static readonly Color PanelBg = new Color(0.04f, 0.06f, 0.10f, 0.80f);
    public static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color Accent = new Color(0.35f, 0.80f, 1.0f);
    public static readonly Color Gold = new Color(1.0f, 0.82f, 0.2f);
    public static readonly Color Danger = new Color(0.95f, 0.25f, 0.2f);
    public static readonly Color Dim = new Color(0.72f, 0.78f, 0.86f);

    // 채운 둥근 사각형
    public static void Round(Rect r, Color c, float radius)
    {
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
    }

    // 테두리만 그리는 둥근 사각형
    public static void Outline(Rect r, Color c, float width, float radius)
    {
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
    }

    // 그림자 + 배경 + 가는 테두리
    public static void Panel(Rect r, float radius = 10f, Color? bg = null)
    {
        float s = S;
        Round(new Rect(r.x + 2f * s, r.y + 4f * s, r.width, r.height), new Color(0, 0, 0, 0.35f), radius * s + 2f);
        Round(r, bg ?? PanelBg, radius * s);
        Outline(r, PanelEdge, Mathf.Max(1f, 1.2f * s), radius * s);
    }

    // 둥근 막대. ratio만큼 채우고 위쪽을 밝게 해 입체감을 준다. trail은 줄어들기 전 값(밝은 잔상)
    public static void Bar(Rect r, float ratio, Color fill, float trail = -1f)
    {
        float rad = r.height * 0.5f;
        Round(r, new Color(0, 0, 0, 0.55f), rad);
        float inset = Mathf.Max(1f, 1.5f * S);
        var inner = new Rect(r.x + inset, r.y + inset, r.width - inset * 2, r.height - inset * 2);
        float irad = inner.height * 0.5f;

        if (trail > ratio)
            DrawFill(inner, trail, new Color(1f, 1f, 1f, 0.35f), irad, false);
        DrawFill(inner, ratio, fill, irad, true);
    }

    static void DrawFill(Rect inner, float ratio, Color fill, float irad, bool shine)
    {
        ratio = Mathf.Clamp01(ratio);
        if (ratio <= 0.001f) return;
        float w = Mathf.Max(inner.height, inner.width * ratio); // 너무 짧아도 둥근 모양을 유지한다
        var fr = new Rect(inner.x, inner.y, w, inner.height);
        Round(fr, fill, irad);
        if (shine)
        {
            Round(new Rect(fr.x, fr.y, fr.width, fr.height * 0.5f), new Color(1, 1, 1, 0.20f), irad);
            Round(new Rect(fr.x, fr.y + fr.height * 0.72f, fr.width, fr.height * 0.28f), new Color(0, 0, 0, 0.18f), irad * 0.6f);
        }
    }

    // 하트: 위쪽 두 원과 45도 돌린 정사각형. center는 하트 중심, size는 가로 폭
    public static void Heart(Vector2 center, float size, Color c)
    {
        float r = size * 0.27f;
        var old = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(center.x, center.y + size * 0.1f));
        Round(new Rect(center.x - size * 0.27f, center.y - size * 0.17f, size * 0.54f, size * 0.54f), c, size * 0.06f);
        GUI.matrix = old;
        Round(new Rect(center.x - size * 0.5f + 0.5f, center.y - size * 0.36f, r * 2, r * 2), c, r);
        Round(new Rect(center.x + size * 0.5f - r * 2 - 0.5f, center.y - size * 0.36f, r * 2, r * 2), c, r);
    }

    // 둥근 알약(캡슐) 라벨 배경
    public static void Pill(Rect r, Color c) { Round(r, c, r.height * 0.5f); }

    static readonly Dictionary<long, GUIStyle> styles = new Dictionary<long, GUIStyle>();

    public static GUIStyle Style(int size, TextAnchor anchor, FontStyle fs = FontStyle.Bold)
    {
        int px = Mathf.Max(10, Mathf.RoundToInt(size * S));
        long key = ((long)px << 16) | ((long)(int)anchor << 4) | (long)(int)fs;
        if (!styles.TryGetValue(key, out var st) || st == null)
        {
            st = new GUIStyle(GUI.skin.label)
            {
                fontSize = px,
                alignment = anchor,
                fontStyle = fs,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            styles[key] = st;
        }
        return st;
    }

    // 그림자를 깔아 어떤 배경에서도 읽히게 한다
    public static void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle fs = FontStyle.Bold)
    {
        var style = Style(size, anchor, fs);
        var old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.85f * color.a);
        GUI.Label(new Rect(r.x + 1.5f * S, r.y + 1.5f * S, r.width, r.height), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    public static float TextWidth(string text, int size, FontStyle fs = FontStyle.Bold)
    {
        return Style(size, TextAnchor.MiddleLeft, fs).CalcSize(new GUIContent(text)).x;
    }

    // 둥근 버튼. 마우스를 올리면 밝아진다. 눌리면 true
    public static bool Button(Rect r, string text, int size)
    {
        float s = S;
        bool hover = r.Contains(Event.current.mousePosition);
        Round(new Rect(r.x + 2 * s, r.y + 4 * s, r.width, r.height), new Color(0, 0, 0, 0.4f), 16 * s);
        Round(r, hover ? new Color(0.30f, 0.62f, 0.85f, 0.95f) : new Color(0.10f, 0.20f, 0.32f, 0.92f), 16 * s);
        Outline(r, hover ? Color.white : PanelEdge, Mathf.Max(1.5f, 2f * s), 16 * s);
        Label(r, text, size, Color.white);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // 조준선: 가운데 호박색 점 + 네 방향 눈금 + 네 모서리 괄호. 각진 밀리터리 스타일(MenuKit과 같은 색),
    // 어두운 외곽선을 먼저 깔아 어떤 배경에서도 보인다. 점이 (cx, cy) 정중앙이라 조준 위치는 그대로다
    public static void Crosshair(float cx, float cy)
    {
        float s = S;
        float th = Mathf.Max(2f, 2f * s);        // 선 두께
        float tick = 9f * s, gap = 6f * s;       // 눈금 길이, 중심에서 눈금까지 빈 거리
        float box = 20f * s, arm = 7f * s;       // 모서리 괄호: 중심에서 모서리까지, 팔 길이
        var shadow = new Color(0.035f, 0.045f, 0.04f, 0.85f);
        var paper = new Color(0.90f, 0.92f, 0.86f, 0.95f);
        var amber = new Color(1.00f, 0.70f, 0.14f, 1f);

        for (int pass = 0; pass < 2; pass++)
        {
            Color line = pass == 0 ? shadow : paper;
            Color dot = pass == 0 ? shadow : amber;
            float o = pass == 0 ? th * 0.7f : 0f;   // 그림자 패스는 조금 더 두껍게

            // 중심 점
            float d = 3f * s;
            Round(new Rect(cx - d * 0.5f - o, cy - d * 0.5f - o, d + o * 2, d + o * 2), dot, 0f);

            // 네 방향 눈금
            Round(new Rect(cx - th * 0.5f - o, cy - gap - tick - o, th + o * 2, tick + o * 2), line, 0f);
            Round(new Rect(cx - th * 0.5f - o, cy + gap - o, th + o * 2, tick + o * 2), line, 0f);
            Round(new Rect(cx - gap - tick - o, cy - th * 0.5f - o, tick + o * 2, th + o * 2), line, 0f);
            Round(new Rect(cx + gap - o, cy - th * 0.5f - o, tick + o * 2, th + o * 2), line, 0f);

            // 네 모서리 괄호 (ㄱ자)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    float px = cx + sx * box, py = cy + sy * box;
                    // 가로 팔: 모서리에서 중심 쪽으로, 세로 팔: 모서리에서 중심 쪽으로
                    Round(new Rect(sx > 0 ? px - arm - o : px - th * 0.5f - o, py - th * 0.5f - o, arm + th * 0.5f + o * 2, th + o * 2), line, 0f);
                    Round(new Rect(px - th * 0.5f - o, sy > 0 ? py - arm - o : py - th * 0.5f - o, th + o * 2, arm + th * 0.5f + o * 2), line, 0f);
                }
            }
        }
    }
}
