using UnityEngine;

// 지면 경사에 맞춰 차체를 기울인다. 오르막과 내리막에서 탱크가 지면에 박히거나 뜨지 않게 한다.
// 방향(좌우)은 다른 이동 스크립트가 정한 값을 그대로 쓰고, 기울기만 덧붙인다
public class GroundAlign : MonoBehaviour
{
    public float smooth = 8.0f;     // 기울기가 따라가는 속도
    public float maxTilt = 45.0f;   // 이 각도 이상은 기울이지 않는다
    public float rayStartHeight = 0.8f;
    public float rayLength = 4.0f;

    Vector3 normal = Vector3.up;

    void LateUpdate()
    {
        // 광선이 자기 몸통 콜라이더 안에서 시작하므로 자기 자신에는 맞지 않는다
        Vector3 target = Vector3.up;
        if (Physics.Raycast(transform.position + Vector3.up * rayStartHeight, Vector3.down,
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
