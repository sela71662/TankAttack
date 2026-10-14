using UnityEngine;

// 나무, 돌, 건물 같은 소품 위로는 탱크가 올라가지 못하게 하는 판정.
// 플레이어는 이동 전에 이 판정으로 막고, AI는 소품 위로 이어지는 NavMeshLink를 지워서(ClimbLinks) 오르지 못하게 했다
public static class PropGuard
{
    // 지형(Cliff), 도로, 다리, 계단은 소품이 아니다. 이름이 이 접두사로 시작하는 콜라이더가 소품이다
    static readonly string[] prefixes =
    {
        "Tree", "Rock", "House", "Tent", "Warehouse", "Doghouse", "Mill", "Watch Tower", "building", "Car"
    };

    static readonly RaycastHit[] hits = new RaycastHit[16];

    public static bool IsProp(Collider c)
    {
        if (c == null || c.isTrigger) return false;
        string n = c.name.Trim();
        foreach (var p in prefixes)
            if (n.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // p 바로 아래의 지면이 소품인가. self 아래의 콜라이더(자기 자신)는 무시한다
    public static bool PropBelow(Vector3 p, Transform self = null, float up = 2.0f, float length = 6.0f)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * up, Vector3.down, hits, length, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider top = null;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (self != null && c.transform.IsChildOf(self)) continue;
            if (hits[i].distance < best) { best = hits[i].distance; top = c; }
        }
        return IsProp(top);
    }
}
