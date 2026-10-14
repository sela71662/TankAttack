using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 다리 위치/각도/높이를 바꾼 뒤 적 이동 경로를 다시 만드는 도구. 메뉴: TankAttack/Rebuild Gate Routes
// 1) 다리 데크 틈을 메우는 Bridge Deck Fill 콜라이더를 데크 높이에 맞춘다
// 2) Gate A/B 경로: 직선 도로 -> 곡선 도로(원호) -> 다리 입구 곡선 -> 다리 데크(2m 간격, 데크 높이) -> 섬 안쪽 5m
// 3) HeavyPost_A/B를 다리 섬쪽 출구 오른쪽 옆으로 옮기고, ShoreZone을 다시 굽는다
// 도로 조각(Road 1: 폭 10m 직선, Road 2: 중심선 반지름 25m 곡선)의 배치가 바뀌면 아래 상수를 고친다
public static class GateRouteBuilder
{
    // Gate A: x=-50.7 북쪽 직선 -> 곡선(중심 (-75.7, 43.9)) -> z=68.9 서쪽 직선 -> 다리 A(북쪽으로)
    static readonly Vector2 gateAStart = new Vector2(-50.7f, 10.0f);
    static readonly Vector2 gateACurveCenter = new Vector2(-75.7f, 43.9f);
    const float roadCurveRadius = 25.0f;
    const float gateARoadZ = 68.9f;
    // Gate B: x=-220.7 남쪽 직선 -> 다리 B(동쪽으로)
    static readonly Vector2 gateBStart = new Vector2(-220.7f, 190.0f);
    const float turnRadius = 6.0f;   // 도로에서 다리로 꺾는 곡선 반지름

    [MenuItem("TankAttack/Rebuild Gate Routes")]
    public static void Rebuild()
    {
        var log = new System.Text.StringBuilder();
        Transform bA = FindByTrim("Bridge"), bB = FindByTrim("Bridge  (2)");
        SpawnGate gA = null, gB = null;
        foreach (var g in Object.FindObjectsByType<SpawnGate>())
        {
            if (g.name.Trim() == "Gate A") gA = g;
            if (g.name.Trim() == "Gate B") gB = g;
        }
        if (bA == null || bB == null || gA == null || gB == null) { Debug.LogError("GateRouteBuilder: 다리나 게이트를 찾지 못했다"); return; }

        FitDeckFill(bA, log);
        FitDeckFill(bB, log);
        Physics.SyncTransforms();

        // ---- Gate A
        BridgeEnds(bA, out Vector3 a0, out Vector3 a1);
        Vector3 aRoad = a0.z < a1.z ? a0 : a1, aIsland = a0.z < a1.z ? a1 : a0;   // 도로는 남쪽
        var A = new List<Vector2> { gateAStart };
        Line(A, new Vector2(gateAStart.x, gateACurveCenter.y), 8f);
        Arc(A, gateACurveCenter, roadCurveRadius, 0f, 90f, 3f);
        Line(A, new Vector2(aRoad.x + turnRadius, gateARoadZ), 8f);
        Arc(A, new Vector2(aRoad.x + turnRadius, gateARoadZ + turnRadius), turnRadius, -90f, -180f, 1.5f); // 서쪽 -> 북쪽
        var aPts = ToWorld(A);
        AddBridge(aPts, aRoad, aIsland);
        Apply(gA, aPts, log);

        // ---- Gate B
        BridgeEnds(bB, out Vector3 b0, out Vector3 b1);
        Vector3 bRoad = b0.x < b1.x ? b0 : b1, bIsland = b0.x < b1.x ? b1 : b0;   // 도로는 서쪽
        var B = new List<Vector2> { gateBStart };
        Line(B, new Vector2(gateBStart.x, bRoad.z + turnRadius), 8f);
        Arc(B, new Vector2(gateBStart.x + turnRadius, bRoad.z + turnRadius), turnRadius, 180f, 270f, 1.5f); // 남쪽 -> 동쪽
        var bPts = ToWorld(B);
        AddBridge(bPts, bRoad, bIsland);
        Apply(gB, bPts, log);

        // ---- Heavy 대기 지점
        MovePost("HeavyPost_A", aRoad, aIsland, log);
        MovePost("HeavyPost_B", bRoad, bIsland, log);

        var sz = Object.FindAnyObjectByType<ShoreZone>();
        if (sz != null) sz.Bake();

        var scene = gA.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("GateRouteBuilder:\n" + log);
    }

    static Transform FindByTrim(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>())
            if (t.name.Trim() == name && t.GetComponent<MeshFilter>() != null) return t;
        return null;
    }

    // 다리 메시 길이 방향(로컬 x) 양 끝의 월드 위치
    static void BridgeEnds(Transform b, out Vector3 e0, out Vector3 e1)
    {
        var mb = b.GetComponent<MeshFilter>().sharedMesh.bounds;
        e0 = b.TransformPoint(mb.center - new Vector3(mb.extents.x, 0, 0));
        e1 = b.TransformPoint(mb.center + new Vector3(mb.extents.x, 0, 0));
    }

    // 다리 자체 콜라이더의 데크 윗면(로컬 높이 중앙값)에 맞춰 얇은 박스 콜라이더를 깐다 (메시 콜라이더 틈으로 떨어지지 않게)
    static void FitDeckFill(Transform t, System.Text.StringBuilder log)
    {
        var mb = t.GetComponent<MeshFilter>().sharedMesh.bounds;
        var own = t.GetComponent<Collider>();
        var ys = new List<float>();
        for (int k = 0; k <= 10; k++)
        {
            Vector3 w = t.TransformPoint(new Vector3(Mathf.Lerp(mb.min.x, mb.max.x, k / 10f) * 0.9f, mb.max.y + 1f, mb.center.z));
            foreach (var h in Physics.RaycastAll(w + Vector3.up * 20f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider == own) ys.Add(t.InverseTransformPoint(h.point).y);
        }
        ys.Sort();
        float top = ys.Count > 0 ? ys[ys.Count / 2] : mb.max.y;

        var fill = t.Find("Bridge Deck Fill");
        if (fill == null)
        {
            fill = new GameObject("Bridge Deck Fill").transform;
            fill.SetParent(t, false);
        }
        fill.localPosition = Vector3.zero; fill.localRotation = Quaternion.identity; fill.localScale = Vector3.one;
        var box = fill.GetComponent<BoxCollider>();
        if (box == null) box = fill.gameObject.AddComponent<BoxCollider>();
        const float thick = 0.3f;
        box.center = new Vector3(mb.center.x, top - thick * 0.5f, mb.center.z);
        box.size = new Vector3(mb.size.x, thick, mb.size.z * 0.85f);
        log.AppendLine($"{t.name.Trim()}: deck local top {top:F2} ({ys.Count} samples)");
    }

    static void Arc(List<Vector2> pts, Vector2 c, float r, float a0, float a1, float step)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * Mathf.Deg2Rad * r / step));
        for (int i = 1; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }

    static void Line(List<Vector2> pts, Vector2 to, float step)
    {
        Vector2 from = pts[pts.Count - 1];
        int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / step));
        for (int i = 1; i <= n; i++) pts.Add(Vector2.Lerp(from, to, i / (float)n));
    }

    static List<Vector3> ToWorld(List<Vector2> pts)
    {
        var w = new List<Vector3>();
        foreach (var p in pts) w.Add(new Vector3(p.x, Surface(p), p.y));
        return w;
    }

    // 도로/다리/지형 중 가장 높은 표면. 없으면 도로 높이
    static float Surface(Vector2 p)
    {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.y), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = h.collider.name.Trim();
            if (!(n.StartsWith("Road") || n.StartsWith("Bridge") || n.StartsWith("Cliff") || n.StartsWith("Stairs"))) continue;
            if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
        }
        return float.IsNaN(best) ? -5.55f : best;
    }

    static float Deck(Vector3 p)
    {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name.Trim().StartsWith("Bridge") && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
        return best;
    }

    // 다리 입구 -> 데크(2m 간격) -> 섬 안쪽 5m
    static void AddBridge(List<Vector3> pts, Vector3 roadEnd, Vector3 islandEnd)
    {
        Vector3 dir = islandEnd - roadEnd; dir.y = 0; dir.Normalize();
        Vector3 start = roadEnd - dir * 0.5f, end = islandEnd + dir * 0.5f;
        float len = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        int n = Mathf.Max(2, Mathf.CeilToInt(len / 2f));
        var deck = new List<Vector3>();
        for (int k = 0; k <= n; k++)
        {
            Vector3 p = Vector3.Lerp(start, end, k / (float)n);
            float y = Deck(p);
            p.y = float.IsNaN(y) ? Surface(new Vector2(p.x, p.z)) : y;
            deck.Add(p);
        }
        pts.AddRange(deck);
        Vector2 inside = new Vector2(islandEnd.x, islandEnd.z) + new Vector2(dir.x, dir.z) * 5f;
        pts.Add(new Vector3(inside.x, Surface(inside), inside.y));
    }

    static void Apply(SpawnGate gate, List<Vector3> pts, System.Text.StringBuilder log)
    {
        var root = gate.transform.Find("Route");
        for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        var list = new List<Transform>();
        for (int i = 0; i < pts.Count; i++)
        {
            var go = new GameObject("WP_" + i);
            Undo.RegisterCreatedObjectUndo(go, "route");
            go.transform.SetParent(root, false);
            go.transform.position = pts[i];
            list.Add(go.transform);
        }
        Undo.RecordObject(gate, "route");
        gate.route = list.ToArray();
        EditorUtility.SetDirty(gate);
        log.AppendLine($"{gate.name.Trim()}: {list.Count} waypoints, last {pts[pts.Count - 1]}");
    }

    // Heavy 대기 지점: 다리 섬쪽 출구에서 4m 들어가 진행 방향 오른쪽으로 비켜선다 (적의 길을 막지 않게).
    // 오른쪽이 물가이거나 땅이 아니면 왼쪽에 둔다
    static void MovePost(string name, Vector3 roadEnd, Vector3 islandEnd, System.Text.StringBuilder log)
    {
        Transform post = null;
        foreach (var t in Object.FindObjectsByType<Transform>()) if (t.name.Trim() == name) post = t;
        if (post == null) return;
        Vector3 dir = islandEnd - roadEnd; dir.y = 0; dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector2 baseP = new Vector2(islandEnd.x, islandEnd.z) + new Vector2(dir.x, dir.z) * 4f;
        // 오른쪽 7m부터 시작해 물가가 아니고 주변에 나무/돌/건물이 없는(포구가 걸리지 않는) 자리를 찾는다
        Vector2 p = baseP + new Vector2(right.x, right.z) * 7f;
        bool found = false;
        foreach (float side in new[] { 1f, -1f })
        {
            foreach (float lateral in new[] { 7f, 9f, 11f, 5f })
            {
                foreach (float fwd in new[] { 0f, 3f, 6f })
                {
                    Vector2 q = baseP + new Vector2(right.x, right.z) * (lateral * side) + new Vector2(dir.x, dir.z) * fwd;
                    if (IsSolidGround(q) && NoPropsAround(q, 4.5f)) { p = q; found = true; break; }
                }
                if (found) break;
            }
            if (found) break;
        }
        if (!found) log.AppendLine($"{name}: 소품 없는 자리를 못 찾아 기본 위치에 둔다");
        Undo.RecordObject(post, "post");
        post.position = new Vector3(p.x, Surface(p), p.y);
        log.AppendLine($"{name} -> {post.position}");
    }

    // 포구 높이에서 반경 r 안에 소품 콜라이더가 없는가
    static bool NoPropsAround(Vector2 p, float r)
    {
        Vector3 c = new Vector3(p.x, Surface(p) + 2.2f, p.y);
        foreach (var col in Physics.OverlapSphere(c, r, ~0, QueryTriggerInteraction.Ignore))
            if (PropGuard.IsProp(col)) return false;
        return true;
    }

    // 지형(Cliff)이나 도로 위이고, 반경 3m 안에 물이 보이지 않는 곳
    static bool IsSolidGround(Vector2 p)
    {
        for (int k = 0; k < 9; k++)
        {
            Vector2 q = k == 0 ? p : p + new Vector2(Mathf.Cos(k * Mathf.PI / 4), Mathf.Sin(k * Mathf.PI / 4)) * 3f;
            string top = ""; float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(q.x, 60f, q.y), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
                if (h.point.y > best && !PropGuard.IsProp(h.collider)) { best = h.point.y; top = h.collider.name.Trim(); }
            if (!(top.StartsWith("Cliff") || top.StartsWith("Road"))) return false;
        }
        return true;
    }
}
