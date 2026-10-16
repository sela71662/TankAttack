using UnityEngine;

// 비행형(UFO)이 고도를 정할 때 쓰는 도우미
public static class FlyUtil
{
    public static bool IsAir(Transform t)
    {
        var ti = t != null ? t.GetComponentInParent<TankInfo>() : null;
        return ti != null && ti.flying;
    }

    // at 바로 아래의 지면 높이. 코어처럼 공중에 뜬 목표는 아래 건물이나 땅을 기준으로 잡는다.
    // 아무것도 없으면 fallback을 돌려준다
    public static float GroundY(Vector3 at, float fallback)
    {
        Vector3 origin = new Vector3(at.x, fallback + 1.0f, at.z);
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(origin, Vector3.down, 200.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            // 목표 자신(코어)과 탱크는 지면이 아니다
            if (h.collider.GetComponentInParent<TeamMember>() is TeamMember tm && !tm.isStructure) continue;
            if (h.collider.GetComponentInParent<CoreAnimator>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? best : fallback;
    }
}
