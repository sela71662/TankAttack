using UnityEngine;
using UnityEngine.AI;

// NavMesh 관련 보조 함수
public static class NavUtil
{
    // 가장 가까운 NavMesh 지점을 찾는다. 반경을 작은 값부터 단계적으로 넓힌다.
    // 큰 반경으로 한 번에 찾으면 같은 위치 바로 옆(0.5m)에 고원 NavMesh가 있어도 5m 아래 낮은 층을 돌려주는 경우가 있어서,
    // 위아래 층이 겹치는 섬 지형에서는 가까운 층을 먼저 잡아야 한다
    // 결과가 나무, 돌, 건물 위면 주변의 소품 아닌 NavMesh 지점을 대신 돌려준다 (목표가 지붕 위로 잡히는 것을 막는다)
    public static bool SampleNearest(Vector3 pos, float maxRadius, out NavMeshHit hit)
    {
        if (!SampleRaw(pos, maxRadius, out hit)) return false;
        if (!PropGuard.PropBelow(hit.position, null, 1.5f, 4.0f)) return true;

        NavMeshHit first = hit;
        foreach (float r in new[] { 3.0f, 6.0f, 10.0f })
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 p = hit.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                if (NavMesh.SamplePosition(p, out NavMeshHit h, 2.0f, NavMesh.AllAreas) && !PropGuard.PropBelow(h.position, null, 1.5f, 4.0f))
                {
                    hit = h;
                    return true;
                }
            }
        }
        hit = first;
        return true;
    }

    static bool SampleRaw(Vector3 pos, float maxRadius, out NavMeshHit hit)
    {
        float[] radii = { 1.0f, 2.5f, 6.0f, 15.0f, 40.0f };
        foreach (float r in radii)
        {
            if (r > maxRadius) break;
            if (NavMesh.SamplePosition(pos, out hit, r, NavMesh.AllAreas)) return true;
        }
        return NavMesh.SamplePosition(pos, out hit, maxRadius, NavMesh.AllAreas);
    }
}
