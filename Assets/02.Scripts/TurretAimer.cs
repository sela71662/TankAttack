using UnityEngine;

// 터렛(좌우)과 포신(상하)을 월드 좌표의 목표 지점으로 조준한다.
// 탱크 루트(차체 기준 좌표계)에 붙인다.
// 모델 프리팹의 계층을 바꿀 수 없어서, turretParts를 hub 지점 기준으로 직접 회전시킨다.
public class TurretAimer : MonoBehaviour
{
    public Transform[] turretParts; // 함께 회전할 파츠 (터렛 메시, firePos 포함)
    public Vector3 hub;             // 회전 중심 (차체 로컬 좌표)
    public Transform firePos;

    public float yawSpeed = 90.0f;   // 도/초
    public float pitchSpeed = 60.0f; // 도/초

    // 차체 정면 기준 각도 제한. 제한을 끄면 터렛이 360도 회전한다
    public bool limitYaw = true;
    public float minYaw = -90.0f;
    public float maxYaw = 90.0f;
    public float minPitch = -10.0f; // 아래쪽
    public float maxPitch = 30.0f;  // 위쪽

    Vector3[] basePos;       // 파츠의 초기 차체 로컬 위치
    Quaternion[] baseRot;    // 파츠의 초기 차체 로컬 회전
    float curYaw;
    float curPitch;
    float errYaw;
    float errPitch;

    public Vector3 HubWorld => transform.TransformPoint(hub);

    void Awake()
    {
        basePos = new Vector3[turretParts.Length];
        baseRot = new Quaternion[turretParts.Length];
        for (int i = 0; i < turretParts.Length; i++)
        {
            basePos[i] = transform.InverseTransformPoint(turretParts[i].position);
            baseRot[i] = Quaternion.Inverse(transform.rotation) * turretParts[i].rotation;
        }
    }

    // 현재 각도와 (제한 전) 원하는 각도의 차이가 tolerance(도) 이내인지
    public bool IsAimed(float tolerance)
    {
        return Mathf.Abs(errYaw) <= tolerance && Mathf.Abs(errPitch) <= tolerance;
    }

    public void AimAt(Vector3 worldPoint)
    {
        // 목표 방향을 차체 로컬 좌표로 변환
        Vector3 d = transform.InverseTransformDirection(worldPoint - HubWorld);

        float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float wantPitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

        float clampYaw = limitYaw ? Mathf.Clamp(wantYaw, minYaw, maxYaw) : wantYaw;
        float clampPitch = Mathf.Clamp(wantPitch, minPitch, maxPitch);

        curYaw = Mathf.MoveTowardsAngle(curYaw, clampYaw, yawSpeed * Time.deltaTime);
        curPitch = Mathf.MoveTowards(curPitch, clampPitch, pitchSpeed * Time.deltaTime);

        // 제한에 걸려 닿을 수 없는 목표는 "조준 안 됨"으로 처리한다
        errYaw = Mathf.DeltaAngle(curYaw, wantYaw);
        errPitch = wantPitch - curPitch;

        Apply();
    }

    void Apply()
    {
        // 좌우(Y축) 회전 후 상하(X축) 회전. 상하는 터렛이 향한 방향 기준으로 적용된다
        Quaternion rot = Quaternion.Euler(0, curYaw, 0) * Quaternion.Euler(-curPitch, 0, 0);
        for (int i = 0; i < turretParts.Length; i++)
        {
            Vector3 localPos = hub + rot * (basePos[i] - hub);
            turretParts[i].position = transform.TransformPoint(localPos);
            turretParts[i].rotation = transform.rotation * (rot * baseRot[i]);
        }
    }
}
