using System.Collections.Generic;
using UnityEngine;

// 인트로/엔딩 화면용 그리기 도우미. 각진 패널, 얇은 선, 모서리 괄호, 스텐실 제목 폰트, 키보드/마우스 메뉴 목록.
// 둥근 카드를 쓰는 인게임 HUD(UiKit)와 달리 밀리터리 HUD 느낌으로 통일한다
public static class MenuKit
{
    public static readonly Color Ink = new Color(0.035f, 0.045f, 0.04f);
    public static readonly Color Paper = new Color(0.90f, 0.92f, 0.86f);
    public static readonly Color Dim = new Color(0.56f, 0.60f, 0.54f);
    public static readonly Color Amber = new Color(1.00f, 0.70f, 0.14f);
    public static readonly Color Alert = new Color(0.92f, 0.24f, 0.18f);
    public static readonly Color Line = new Color(0.90f, 0.92f, 0.86f, 0.22f);

    public static float S => UiKit.S;
    public static float A = 1f; // 화면 전체에 곱해지는 투명도. OnGUI 맨 앞에서 정한다

    // ---------- 폰트 ----------
    static Font titleFont, boldFont, mediumFont;
    public static Font TitleFont => titleFont != null ? titleFont : (titleFont = Resources.Load<Font>("Fonts/BlackOpsOne"));
    public static Font Bold => boldFont != null ? boldFont : (boldFont = Resources.Load<Font>("Fonts/Rajdhani-Bold"));
    public static Font Medium => mediumFont != null ? mediumFont : (mediumFont = Resources.Load<Font>("Fonts/Rajdhani-Medium"));

    static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();

    static GUIStyle Style(Font f, int size, TextAnchor anchor)
    {
        int px = Mathf.Max(10, Mathf.RoundToInt(size * S));
        string key = (f != null ? f.name : "default") + "|" + px + "|" + (int)anchor;
        if (!styles.TryGetValue(key, out var st) || st == null)
        {
            st = new GUIStyle(GUI.skin.label)
            {
                font = f,
                fontSize = px,
                fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, // 영문 폰트는 굵기가 폰트 파일에 들어 있다
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            styles[key] = st;
        }
        return st;
    }

    // f가 null이면 기본 폰트(한글). 영문 제목/라벨은 TitleFont, Bold, Medium을 넘긴다
    public static void Text(Rect r, string text, int size, Color c, Font f = null, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var old = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * A);
        GUI.Label(r, text, Style(f, size, anchor));
        GUI.color = old;
    }

    public static float TextWidth(string text, int size, Font f = null)
    {
        return Style(f, size, TextAnchor.MiddleLeft).CalcSize(new GUIContent(text)).x;
    }

    // 칸 너비에 들어갈 때까지 글자 크기를 줄여서 그린다 (칸 크기는 그대로 두고 긴 이름이 잘리지 않게 한다)
    public static void TextFit(Rect r, string text, int maxSize, int minSize, Color c, Font f = null, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        int size = maxSize;
        while (size > minSize && TextWidth(text, size, f) > r.width) size--;
        Text(r, text, size, c, f, anchor);
    }

    // 막대 위처럼 밝은 면 위에 올리는 글자. 어두운 외곽선을 둘러 어떤 배경에서도 읽힌다
    public static void TextOutlined(Rect r, string text, int size, Color c, Font f = null, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        float o = Mathf.Max(1.5f, 1.8f * S);
        var dark = new Color(Ink.r, Ink.g, Ink.b, 0.95f);
        Text(new Rect(r.x - o, r.y, r.width, r.height), text, size, dark, f, anchor);
        Text(new Rect(r.x + o, r.y, r.width, r.height), text, size, dark, f, anchor);
        Text(new Rect(r.x, r.y - o, r.width, r.height), text, size, dark, f, anchor);
        Text(new Rect(r.x, r.y + o, r.width, r.height), text, size, dark, f, anchor);
        Text(r, text, size, c, f, anchor);
    }

    // 글자 사이를 띄운다 (IMGUI는 자간을 지원하지 않는다)
    public static string Spaced(string s)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1) sb.Append(s[i] == ' ' ? "  " : " ");
        }
        return sb.ToString();
    }

    // ---------- 도형 ----------
    public static void Fill(Rect r, Color c)
    {
        UiKit.Round(r, new Color(c.r, c.g, c.b, c.a * A), 0f);
    }

    public static void HLine(float x, float y, float w, Color c) { Fill(new Rect(x, y, w, Mathf.Max(1f, S)), c); }
    public static void VLine(float x, float y, float h, Color c) { Fill(new Rect(x, y, Mathf.Max(1f, S), h), c); }

    // 네 모서리에만 ㄱ자 괄호를 그린다
    public static void Brackets(Rect r, Color c, float len, float th)
    {
        len *= S; th = Mathf.Max(1f, th * S);
        Fill(new Rect(r.x, r.y, len, th), c); Fill(new Rect(r.x, r.y, th, len), c);
        Fill(new Rect(r.xMax - len, r.y, len, th), c); Fill(new Rect(r.xMax - th, r.y, th, len), c);
        Fill(new Rect(r.x, r.yMax - th, len, th), c); Fill(new Rect(r.x, r.yMax - len, th, len), c);
        Fill(new Rect(r.xMax - len, r.yMax - th, len, th), c); Fill(new Rect(r.xMax - th, r.yMax - len, th, len), c);
    }

    // 키보드 키 모양 (테두리 상자 + 글자)
    public static void KeyCap(Rect r, string key)
    {
        Fill(r, new Color(1, 1, 1, 0.05f));
        float t = Mathf.Max(1f, S);
        Fill(new Rect(r.x, r.y, r.width, t), Line); Fill(new Rect(r.x, r.yMax - t, r.width, t), Line);
        Fill(new Rect(r.x, r.y, t, r.height), Line); Fill(new Rect(r.xMax - t, r.y, t, r.height), Line);
        Text(r, key, 22, Paper, Bold, TextAnchor.MiddleCenter);
    }

    // 인게임 HUD 패널: 반투명 어두운 면 + 가는 테두리 + 왼쪽 위/오른쪽 아래 강조 괄호
    public static void HudPanel(Rect r, Color accent)
    {
        float s = S, t = Mathf.Max(1f, s);
        Fill(new Rect(r.x + 2 * s, r.y + 3 * s, r.width, r.height), new Color(0, 0, 0, 0.30f));
        Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.74f));
        Fill(new Rect(r.x, r.y, r.width, t), Line); Fill(new Rect(r.x, r.yMax - t, r.width, t), Line);
        Fill(new Rect(r.x, r.y, t, r.height), Line); Fill(new Rect(r.xMax - t, r.y, t, r.height), Line);

        float len = 12 * s, th = Mathf.Max(2f, 2f * s);
        Fill(new Rect(r.x, r.y, len, th), accent); Fill(new Rect(r.x, r.y, th, len), accent);
        Fill(new Rect(r.xMax - len, r.yMax - th, len, th), accent); Fill(new Rect(r.xMax - th, r.yMax - len, th, len), accent);
    }

    // 칸으로 나뉜 막대. ratio만큼 칸이 차고, trail은 줄어들기 전 값을 흐린 칸으로 남긴다
    public static void BlockBar(Rect r, float ratio, Color fill, float trail = -1f, float cell = 12f)
    {
        ratio = Mathf.Clamp01(ratio);
        float s = S, t = Mathf.Max(1f, s);
        Fill(r, new Color(0, 0, 0, 0.55f));
        Fill(new Rect(r.x, r.y, r.width, t), Line); Fill(new Rect(r.x, r.yMax - t, r.width, t), Line);
        Fill(new Rect(r.x, r.y, t, r.height), Line); Fill(new Rect(r.xMax - t, r.y, t, r.height), Line);

        float pad = Mathf.Max(2f, 3f * s);
        var inner = new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2);
        int n = Mathf.Max(4, Mathf.RoundToInt(inner.width / (cell * s)));
        float gap = Mathf.Max(1f, 2f * s);
        float cw = (inner.width - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            float x = inner.x + i * (cw + gap);
            float lo = (float)i / n, hi = (float)(i + 1) / n;
            if (trail > ratio && lo < trail) Fill(new Rect(x, inner.y, cw, inner.height), new Color(1, 1, 1, 0.28f));
            if (ratio > lo)
            {
                float f = Mathf.Clamp01((ratio - lo) / (hi - lo));
                Fill(new Rect(x, inner.y, cw * f, inner.height), fill);
            }
        }
    }

    // 테두리 상자 안의 짧은 글자 (레벨 표시 등)
    public static void Chip(Rect r, string text, Color c, int size = 15)
    {
        float t = Mathf.Max(1f, S);
        Fill(r, new Color(c.r, c.g, c.b, 0.14f));
        Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        Text(r, text, size, c, Bold, TextAnchor.MiddleCenter);
    }

    // 작은 제목표시 (앞의 짧은 막대 + 띄운 글자)
    public static void Tag(float x, float y, string text, Color accent)
    {
        Fill(new Rect(x, y + 4 * S, 34 * S, 3 * S), accent);
        Text(new Rect(x + 48 * S, y - 10 * S, 900 * S, 30 * S), Spaced(text), 18, Dim, Medium);
    }

    // ---------- 배경 연출 ----------
    static Texture2D hGrad, vGrad, scan;

    static void EnsureTextures()
    {
        if (hGrad == null)
        {
            hGrad = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 128; x++)
            {
                float t = 1f - x / 127f;
                hGrad.SetPixel(x, 0, new Color(1, 1, 1, t * t)); // 부드럽게 사라지는 곡선
            }
            hGrad.Apply();
        }
        if (vGrad == null)
        {
            vGrad = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 128; y++)
            {
                float t = y / 127f; // 아래(0)에서 위(1)로: 위쪽이 진하다
                vGrad.SetPixel(0, y, new Color(1, 1, 1, t * t));
            }
            vGrad.Apply();
        }
        if (scan == null)
        {
            scan = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++) scan.SetPixel(0, y, new Color(1, 1, 1, y == 0 ? 1f : 0f));
            scan.Apply();
        }
    }

    // 왼쪽을 어둡게 눌러 글자가 읽히게 하고, 위아래 가장자리와 주사선을 깐다. 오른쪽은 맵이 비친다
    public static void Shade(float left = 0.92f, float edge = 0.55f)
    {
        EnsureTextures();
        float W = Screen.width, H = Screen.height;
        var old = GUI.color;

        GUI.color = new Color(Ink.r, Ink.g, Ink.b, left * A);
        GUI.DrawTexture(new Rect(0, 0, W * 0.78f, H), hGrad, ScaleMode.StretchToFill);

        GUI.color = new Color(Ink.r, Ink.g, Ink.b, edge * A);
        GUI.DrawTexture(new Rect(0, 0, W, H * 0.22f), vGrad, ScaleMode.StretchToFill);
        GUI.DrawTextureWithTexCoords(new Rect(0, H * 0.78f, W, H * 0.22f), vGrad, new Rect(0, 1, 1, -1)); // 위아래를 뒤집어 아래쪽을 진하게

        GUI.color = new Color(0, 0, 0, 0.10f * A);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), scan, new Rect(0, 0, 1, H / 4f));
        GUI.color = old;
    }

    // 화면 전체를 한 색으로 덮는다 (전환 페이드, 결과별 색조)
    public static void Wash(Color c)
    {
        var old = GUI.color;
        GUI.color = Color.white;
        UiKit.Round(new Rect(0, 0, Screen.width, Screen.height), c, 0f);
        GUI.color = old;
    }

    // 화면 가장자리 안쪽의 모서리 괄호
    public static void ScreenFrame()
    {
        float m = 36f * S;
        Brackets(new Rect(m, m, Screen.width - m * 2, Screen.height - m * 2), Line, 44, 2);
    }
}

// 세로 메뉴 목록. 마우스를 올리거나 W/S, 위/아래 화살표로 고르고 Enter/Space 또는 클릭으로 실행한다
public class MenuList
{
    readonly string[] labels;
    readonly string[] subs;
    readonly float[] hover;
    Vector3 lastMouse;
    bool mouseMoved;

    public int Sel { get; private set; }

    public MenuList(string[] labels, string[] subs)
    {
        this.labels = labels;
        this.subs = subs;
        hover = new float[labels.Length];
        lastMouse = Input.mousePosition;
    }

    // Update에서 호출. 키보드로 실행한 항목 번호를 돌려준다 (없으면 -1)
    public int HandleKeys()
    {
        mouseMoved = (Input.mousePosition - lastMouse).sqrMagnitude > 1f;
        lastMouse = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Sel = (Sel + 1) % labels.Length;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Sel = (Sel - 1 + labels.Length) % labels.Length;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) return Sel;
        return -1;
    }

    // OnGUI에서 호출. 클릭한 항목 번호를 돌려준다 (없으면 -1)
    public int Draw(Rect area, float rowH, bool interactive = true)
    {
        float s = MenuKit.S;
        int clicked = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            var row = new Rect(area.x, area.y + i * rowH, area.width, rowH - 6 * s);
            if (interactive && mouseMoved && row.Contains(Event.current.mousePosition)) Sel = i;

            if (Event.current.type == EventType.Repaint)
                hover[i] = Mathf.MoveTowards(hover[i], i == Sel && interactive ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float h = hover[i];

            MenuKit.Fill(row, new Color(MenuKit.Amber.r, MenuKit.Amber.g, MenuKit.Amber.b, 0.10f * h));
            MenuKit.Fill(new Rect(row.x, row.y, 4 * s, row.height), new Color(MenuKit.Amber.r, MenuKit.Amber.g, MenuKit.Amber.b, h));
            MenuKit.HLine(row.x, row.yMax, row.width, MenuKit.Line);

            Color lc = Color.Lerp(MenuKit.Paper, MenuKit.Amber, h);
            float shift = 12 * s * h;
            MenuKit.Text(new Rect(row.x + 22 * s + shift, row.y, 60 * s, row.height), (i + 1).ToString("00"), 18, Color.Lerp(MenuKit.Dim, MenuKit.Amber, h), MenuKit.Medium);
            MenuKit.Text(new Rect(row.x + 76 * s + shift, row.y - 2 * s, row.width, row.height), labels[i], 40, lc, MenuKit.Bold);
            if (subs != null && i < subs.Length)
                MenuKit.Text(new Rect(row.x, row.y, row.width - 22 * s, row.height), subs[i], 20, Color.Lerp(MenuKit.Dim, MenuKit.Paper, h), null, TextAnchor.MiddleRight);

            if (interactive && GUI.Button(row, GUIContent.none, GUIStyle.none)) clicked = i;
        }
        return clicked;
    }
}

// 화면 전환 페이드. 눌린 동작은 화면이 어두워진 뒤에 실행한다
public class ScreenFader
{
    const float OutDuration = 0.45f;
    System.Action pending;
    float startedAt;
    bool fired;

    public bool Busy => pending != null || fired;

    public void Go(System.Action action)
    {
        if (Busy) return;
        pending = action;
        startedAt = Time.unscaledTime;
    }

    // Update에서 호출
    public void Tick()
    {
        if (pending == null || Time.unscaledTime - startedAt < OutDuration) return;
        var a = pending;
        pending = null;
        fired = true;
        a();
    }

    public float Alpha => fired ? 1f : pending != null ? Mathf.Clamp01((Time.unscaledTime - startedAt) / OutDuration) : 0f;
}
