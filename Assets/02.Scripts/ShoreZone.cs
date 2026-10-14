using UnityEngine;

// 호수와 닿은 섬 가장자리(단차) 구역. 호수까지의 거리 지도를 미리 구워 두고(Bake), 플레이어 탱크가 이 구역으로 들어가지 못하게 한다.
// 다리 주변은 건너다녀야 하므로 제외한다. AI 탱크는 이 구역 근처의 NavMeshLink(ClimbLinks)를 지워서 오르지 못하게 했다
public class ShoreZone : MonoBehaviour
{
    public static ShoreZone Instance { get; private set; }

    public float blockDistance = 3.0f;   // 물에서 이 거리 안으로는 들어갈 수 없다
    public float bridgeMargin = 8.0f;    // 다리 주변 이 거리 안은 구역에서 제외한다

    [HideInInspector] public Vector2 origin; // 지도 왼쪽 아래(월드 x, z)
    [HideInInspector] public float cell = 1.0f;
    [HideInInspector] public int width;
    [HideInInspector] public int height;
    [HideInInspector] public byte[] dist;    // 칸마다 물까지의 거리(m). 255는 먼 곳 또는 제외 구역

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 월드 위치에서 물까지의 거리(m). 지도 밖이면 아주 크다
    public static float Distance(Vector3 p)
    {
        var z = Instance;
        if (z == null || z.dist == null) return 999.0f;
        int x = Mathf.FloorToInt((p.x - z.origin.x) / z.cell);
        int y = Mathf.FloorToInt((p.z - z.origin.y) / z.cell);
        if (x < 0 || y < 0 || x >= z.width || y >= z.height) return 999.0f;
        return z.dist[y * z.width + x];
    }

    // from에서 to로 가는 것이 단차 구역 안쪽으로 더 들어가는 이동인가. 이미 안에 있어도 바깥쪽으로 나오는 이동은 허용한다
    public static bool Blocks(Vector3 from, Vector3 to)
    {
        var z = Instance;
        if (z == null) return false;
        float d2 = Distance(to);
        return d2 < z.blockDistance && d2 < Distance(from);
    }

#if UNITY_EDITOR
    // 씬의 Lake* 렌더러와 지형 콜라이더로 거리 지도를 만든다. 물 평면보다 지면이 낮은 칸이 물이다
    [ContextMenu("Bake")]
    public void Bake()
    {
        var lakes = new System.Collections.Generic.List<Renderer>();
        var bridges = new System.Collections.Generic.List<Bounds>();
        foreach (var r in FindObjectsByType<Renderer>())
        {
            string n = r.name.Trim();
            if (n.StartsWith("Lake")) lakes.Add(r);
            else if (n.StartsWith("Bridge")) bridges.Add(r.bounds);
        }
        if (lakes.Count == 0) { Debug.LogWarning("ShoreZone: Lake 렌더러가 없다"); return; }

        Bounds all = lakes[0].bounds;
        foreach (var l in lakes) all.Encapsulate(l.bounds);
        origin = new Vector2(all.min.x, all.min.z);
        width = Mathf.CeilToInt(all.size.x / cell);
        height = Mathf.CeilToInt(all.size.z / cell);

        const float inf = 1e6f;
        var d = new float[width * height];
        var exempt = new bool[width * height];
        float startY = all.max.y + 80.0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 p = new Vector3(origin.x + (x + 0.5f) * cell, startY, origin.y + (y + 0.5f) * cell);
                int i = y * width + x;
                d[i] = inf;

                // 이 칸을 덮는 호수 중 물 높이가 가장 높은 것
                float water = float.NegativeInfinity;
                foreach (var l in lakes)
                {
                    var b = l.bounds;
                    if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                    water = Mathf.Max(water, b.max.y);
                }
                if (float.IsNegativeInfinity(water)) continue;

                // 호수 콜라이더를 뺀 가장 높은 지면
                float ground = float.NegativeInfinity;
                foreach (var h in Physics.RaycastAll(p, Vector3.down, 400.0f))
                {
                    if (h.collider.isTrigger || h.collider.name.Trim().StartsWith("Lake")) continue;
                    ground = Mathf.Max(ground, h.point.y);
                }
                if (ground < water - 0.05f) d[i] = 0.0f; // 물
            }
        }

        // 다리 주변은 단차 구역에서 제외
        foreach (var b in bridges)
        {
            b.Expand(new Vector3(bridgeMargin * 2, 0, bridgeMargin * 2));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - origin.x) / cell)), x1 = Mathf.Min(width - 1, Mathf.FloorToInt((b.max.x - origin.x) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - origin.y) / cell)), y1 = Mathf.Min(height - 1, Mathf.FloorToInt((b.max.z - origin.y) / cell));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) exempt[y * width + x] = true;
        }

        // 거리 변환 (체이머퍼: 앞 방향, 뒤 방향 두 번 훑는다)
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

        dist = new byte[width * height];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = exempt[i] ? (byte)255 : (byte)Mathf.Min(255, Mathf.RoundToInt(d[i] * cell));

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
