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

    // 조준선: 흰 고리 + 점 + 네 방향 눈금, 검은 외곽선으로 어떤 배경에서도 보인다
    public static void Crosshair(float cx, float cy)
    {
        float s = S;
        float ring = 11f * s, tick = 7f * s, gap = 5f * s, th = Mathf.Max(2f, 2f * s);
        var shadow = new Color(0, 0, 0, 0.7f);
        var white = new Color(1f, 1f, 1f, 0.95f);

        for (int pass = 0; pass < 2; pass++)
        {
            Color c = pass == 0 ? shadow : white;
            float o = pass == 0 ? th * 0.6f : 0f;   // 그림자 패스는 조금 더 두껍게
            Outline(new Rect(cx - ring - o, cy - ring - o, (ring + o) * 2, (ring + o) * 2), c, th + o * 2, ring + o);
            Round(new Rect(cx - 2f * s - o, cy - 2f * s - o, 4f * s + o * 2, 4f * s + o * 2), c, 4f * s);
            float a = ring + gap;
            Round(new Rect(cx - th * 0.5f - o, cy - a - tick - o, th + o * 2, tick + o * 2), c, 1f);
            Round(new Rect(cx - th * 0.5f - o, cy + a - o, th + o * 2, tick + o * 2), c, 1f);
            Round(new Rect(cx - a - tick - o, cy - th * 0.5f - o, tick + o * 2, th + o * 2), c, 1f);
            Round(new Rect(cx + a - o, cy - th * 0.5f - o, tick + o * 2, th + o * 2), c, 1f);
        }
    }
}
