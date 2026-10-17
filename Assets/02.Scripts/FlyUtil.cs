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

    // 같은 팀의 다른 UFO와 radius 안에서 가까워질수록 강하게 밀어내는 수평 방향 (크기 0~1). 같은 목표를 쫓는 UFO끼리 겹치지 않게 한다
    public static Vector3 Separation(Transform self, Team team, float radius)
    {
        Vector3 push = Vector3.zero;
        foreach (var t in TeamMember.All)
        {
            if (t == null || t.transform == self || t.team != team || t.isStructure) continue;
            var ti = t.GetComponent<TankInfo>();
            if (ti == null || !ti.flying) continue;
            Vector3 d = self.position - t.transform.position;
            d.y = 0;
            float m = d.magnitude;
            if (m >= radius) continue;
            // 거의 같은 자리면 서로 반대쪽으로 갈라지도록 이름 순서로 방향을 정한다
            Vector3 dir = m > 0.05f ? d / m : (string.CompareOrdinal(self.name, t.name) >= 0 ? self.right : -self.right);
            push += dir * (1.0f - m / radius);
        }
        return Vector3.ClampMagnitude(push, 1.0f);
    }

    // 지면(지형, 호수, 소품) 위 clearance 미만으로 내려가지 않도록 높이를 끌어올린다 (UFO가 맵 아래로 파고드는 것 방지)
    public static Vector3 ClampAboveGround(Vector3 p, float clearance)
    {
        float ground = float.MinValue;
        Vector3 origin = new Vector3(p.x, p.y + 100.0f, p.z);
        foreach (var h in Physics.RaycastAll(origin, Vector3.down, 400.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<TeamMember>() is TeamMember tm && !tm.isStructure) continue;
            if (h.collider.GetComponentInParent<CoreAnimator>() != null) continue;
            if (h.point.y > ground) ground = h.point.y;
        }
        if (ground > float.MinValue && p.y < ground + clearance) p.y = ground + clearance;
        return p;
    }
}
