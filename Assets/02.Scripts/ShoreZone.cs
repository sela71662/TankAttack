using UnityEngine;

// 호수와 닿은 섬 가장자리(단차) 구역. 호수까지의 거리 지도를 미리 구워 두고(Bake), 플레이어 탱크가 물이나 물가 벽 쪽으로 들어가지 못하게 한다.
// 다리 데크와 다리 입구의 땅은 건너다녀야 하므로 제외하지만, 다리 옆의 물은 제외하지 않는다(다리에서 떨어지지 않게)
public class ShoreZone : MonoBehaviour
{
    public static ShoreZone Instance { get; private set; }

    public float blockDistance = 3.0f;   // 물에서 이 거리 안으로는 들어갈 수 없다
    public float bridgeMargin = 5.0f;    // 다리 데크에서 이 거리 안의 땅은 구역에서 제외한다 (다리 입구)

    [HideInInspector] public Vector2 origin; // 지도 왼쪽 아래(월드 x, z)
    [HideInInspector] public float cell = 1.0f;
    [HideInInspector] public int width;
    [HideInInspector] public int height;
    [HideInInspector] public byte[] dist;    // 칸마다 물까지의 거리(m). 0은 물, 255는 먼 곳 또는 제외 구역(다리)

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    bool Cell(Vector3 p, out int i)
    {
        int x = Mathf.FloorToInt((p.x - origin.x) / cell);
        int y = Mathf.FloorToInt((p.z - origin.y) / cell);
        i = y * width + x;
        return dist != null && x >= 0 && y >= 0 && x < width && y < height;
    }

    // 월드 위치에서 물까지의 거리(m). 지도 밖이면 아주 크다
    public static float Distance(Vector3 p)
    {
        var z = Instance;
        if (z == null || !z.Cell(p, out int i)) return 999.0f;
        return z.dist[i];
    }

    public static bool IsWater(Vector3 p)
    {
        return Distance(p) < 0.5f;
    }

    // from에서 to로 가는 것이 단차 구역 안쪽으로 더 들어가는 이동인가. 이미 안에 있어도 바깥쪽으로 나오는 이동은 허용한다
    public static bool Blocks(Vector3 from, Vector3 to)
    {
        var z = Instance;
        if (z == null) return false;
        float d2 = Distance(to);
        return d2 < z.blockDistance && d2 < Distance(from);
    }

    // p에서 가장 가까운, 물에서 충분히 떨어진 땅 칸의 중심(xz). 없으면 false
    public static bool FindSafe(Vector3 p, out Vector3 safe)
    {
        safe = p;
        var z = Instance;
        if (z == null || z.dist == null) return false;
        int cx = Mathf.FloorToInt((p.x - z.origin.x) / z.cell);
        int cy = Mathf.FloorToInt((p.z - z.origin.y) / z.cell);
        int need = Mathf.CeilToInt(z.blockDistance) + 2;
        for (int r = 1; r <= 60; r++)
        {
            float best = float.MaxValue;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // 고리 테두리만
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= z.width || y >= z.height) continue;
                    int d = z.dist[y * z.width + x];
                    if (d < need || d == 255) continue; // 물가와 다리는 피한다
                    float dd = dx * dx + dy * dy;
                    if (dd < best)
                    {
                        best = dd;
                        safe = new Vector3(z.origin.x + (x + 0.5f) * z.cell, p.y, z.origin.y + (y + 0.5f) * z.cell);
                    }
                }
            if (best < float.MaxValue) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    // 씬의 Lake* 렌더러와 지형 콜라이더로 거리 지도를 만든다. 물 평면보다 지면이 낮은 칸이 물이고,
    // 가장 위의 표면이 Bridge* 콜라이더인 칸이 다리 데크다
    [ContextMenu("Bake")]
    public void Bake()
    {
        var lakes = new System.Collections.Generic.List<Renderer>();
        foreach (var r in FindObjectsByType<Renderer>())
            if (r.name.Trim().StartsWith("Lake")) lakes.Add(r);
        if (lakes.Count == 0) { Debug.LogWarning("ShoreZone: Lake 렌더러가 없다"); return; }

        Bounds all = lakes[0].bounds;
        foreach (var l in lakes) all.Encapsulate(l.bounds);
        origin = new Vector2(all.min.x, all.min.z);
        width = Mathf.CeilToInt(all.size.x / cell);
        height = Mathf.CeilToInt(all.size.z / cell);

        const float inf = 1e6f;
        var d = new float[width * height];
        var deck = new bool[width * height];
        float startY = all.max.y + 80.0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 p = new Vector3(origin.x + (x + 0.5f) * cell, startY, origin.y + (y + 0.5f) * cell);
                int i = y * width + x;
                d[i] = inf;

                // 호수 콜라이더를 뺀 가장 높은 표면
                float ground = TopSurface(p, out string top);
                if (top.StartsWith("Bridge"))
                {
                    // 칸 전체가 데크 위일 때만 데크로 본다. 가장자리에 걸친 칸은 물로 두어 탱크 중심이 데크 끝을 넘지 못하게 한다
                    // 가장자리 칸은 데크가 아닌 모서리 중 가장 낮은 지면으로 물 여부를 판단한다 (다리 옆은 물, 다리 끝은 땅)
                    bool full = true;
                    float lowest = float.PositiveInfinity;
                    foreach (var o in new[] { new Vector3(-0.45f, 0, -0.45f), new Vector3(0.45f, 0, -0.45f), new Vector3(-0.45f, 0, 0.45f), new Vector3(0.45f, 0, 0.45f) })
                    {
                        float g2 = TopSurface(p + o * cell, out string t2);
                        if (t2.StartsWith("Bridge")) continue;
                        full = false;
                        lowest = Mathf.Min(lowest, g2);
                    }
                    if (full) { deck[i] = true; continue; }
                    ground = lowest;
                }

                // 이 칸을 덮는 호수 중 물 높이가 가장 높은 것
                float water = float.NegativeInfinity;
                foreach (var l in lakes)
                {
                    var b = l.bounds;
                    if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                    water = Mathf.Max(water, b.max.y);
                }
                if (float.IsNegativeInfinity(water)) continue;
                if (ground < water - 0.05f) d[i] = 0.0f; // 물
            }
        }

        // 거리 변환 (체이머퍼: 앞 방향, 뒤 방향 두 번 훑는다)
        Chamfer(d);

        // 다리 데크와, 데크 가까이의 물이 아닌 칸(다리 입구)은 제외한다
        var deckDist = new float[width * height];
        for (int i = 0; i < deckDist.Length; i++) deckDist[i] = deck[i] ? 0 : inf;
        Chamfer(deckDist);

        dist = new byte[width * height];
        for (int i = 0; i < dist.Length; i++)
        {
            bool exempt = deck[i] || (d[i] > 0 && deckDist[i] * cell <= bridgeMargin);
            dist[i] = exempt ? (byte)255 : (byte)Mathf.Min(254, Mathf.RoundToInt(d[i] * cell));
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }

    static float TopSurface(Vector3 from, out string top)
    {
        float ground = float.NegativeInfinity;
        top = "";
        foreach (var h in Physics.RaycastAll(from, Vector3.down, 400.0f))
        {
            if (h.collider.isTrigger || h.collider.name.Trim().StartsWith("Lake")) continue;
            if (h.point.y > ground) { ground = h.point.y; top = h.collider.name.Trim(); }
        }
        return ground;
    }

    void Chamfer(float[] d)
    {
        const float diag = 1.41421356f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + 1);
                if (y > 0)
                {
                    d[i] = Mathf.Min(d[i], d[i - width] + 1);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - width - 1] + diag);
                    if (x < width - 1) d[i] = Mathf.Min(d[i], d[i - width + 1] + diag);
                }
            }
        for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                if (x < width - 1) d[i] = Mathf.Min(d[i], d[i + 1] + 1);
                if (y < height - 1)
                {
                    d[i] = Mathf.Min(d[i], d[i + width] + 1);
                    if (x < width - 1) d[i] = Mathf.Min(d[i], d[i + width + 1] + diag);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i + width - 1] + diag);
                }
            }
    }
#endif
}
