using UnityEngine;

// 지면 경사에 맞춰 차체를 기울인다. 오르막과 내리막에서 탱크가 지면에 박히거나 뜨지 않게 한다.
// 방향(좌우)은 다른 이동 스크립트가 정한 값을 그대로 쓰고, 기울기만 덧붙인다
public class GroundAlign : MonoBehaviour
{
    public float smooth = 8.0f;     // 기울기가 따라가는 속도
    public float maxTilt = 45.0f;   // 이 각도 이상은 기울이지 않는다
    public float rayStartHeight = 0.8f;
    public float rayLength = 4.0f;

    // 켜면 차체 바로 앞과 뒤 지면만 재서 앞뒤 기울기만 맞춘다 (옆 경사나 주변 경사에 끌려 기울지 않는다). 플레이어용
    public bool frontBackOnly = false;
    public float probeDistance = 1.5f;  // 앞뒤로 얼마나 떨어진 곳을 잴 것인가
    [System.NonSerialized] public float forwardSlope; // frontBackOnly일 때 앞쪽이 높으면 +인 경사 (높이차 / 거리)

    Vector3 normal = Vector3.up;
    static readonly RaycastHit[] probeHits = new RaycastHit[8];

    // p 위쪽에서 아래로 쏴서 자기 자신을 뺀 가장 가까운 지면 높이
    bool GroundY(Vector3 p, out float y)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 2.0f, Vector3.down, probeHits, 6.0f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        y = 0;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (probeHits[i].collider.transform.IsChildOf(transform)) continue;
            if (probeHits[i].distance < best) { best = probeHits[i].distance; y = probeHits[i].point.y; found = true; }
        }
        return found;
    }

    void LateUpdate()
    {
        // 광선이 자기 몸통 콜라이더 안에서 시작하므로 자기 자신에는 맞지 않는다
        Vector3 target = Vector3.up;
        if (frontBackOnly)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude > 0.0001f)
            {
                fwd.Normalize();
                Vector3 p = transform.position;
                bool hasF = GroundY(p + fwd * probeDistance, out float yF);
                bool hasB = GroundY(p - fwd * probeDistance, out float yB);
                forwardSlope = hasF && hasB ? (yF - yB) / (2.0f * probeDistance) : 0.0f;
                if (hasF && hasB)
                {
                    // 앞뒤 높이차로 앞뒤 기울기만 구한다. 좌우(옆) 기울기는 없다
                    Vector3 slopeDir = (fwd * (2.0f * probeDistance) + Vector3.up * (yF - yB)).normalized;
                    target = Vector3.Cross(slopeDir, Vector3.Cross(Vector3.up, fwd)).normalized;
                }
            }
        }
        else if (Physics.Raycast(transform.position + Vector3.up * rayStartHeight, Vector3.down,
                            out RaycastHit hit, rayLength, ~0, QueryTriggerInteraction.Ignore))
            target = hit.normal;

        if (Vector3.Angle(Vector3.up, target) > maxTilt)
            target = Vector3.Slerp(Vector3.up, target, maxTilt / Vector3.Angle(Vector3.up, target));

        normal = Vector3.Slerp(normal, target, Mathf.Clamp01(smooth * Time.deltaTime)).normalized;

        Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f) return;

        Vector3 forward = Vector3.ProjectOnPlane(heading.normalized, normal);
        if (forward.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(forward, normal);
    }
}
