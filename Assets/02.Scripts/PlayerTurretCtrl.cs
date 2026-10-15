using UnityEngine;

// 플레이어 터렛 조준과 발사. 마우스 좌우/상하로 터렛과 포신이 차체 기준으로 움직이고, 좌클릭으로 발사한다.
// 카메라는 차체 방향만 따라가므로 마우스는 터렛만 움직인다
[RequireComponent(typeof(TurretAimer), typeof(TankShooter))]
public class PlayerTurretCtrl : MonoBehaviour
{
    public float sensitivityX = 3.0f;
    public float sensitivityY = 2.0f;
    public float aimRange = 300.0f;

    TurretAimer aimer;
    TankShooter shooter;
    float aimYaw;   // 차체 정면 기준 좌우 각도
    float aimPitch; // 위쪽이 +

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return; // 커서가 풀려 있으면(메뉴 상태) 조작하지 않는다

        aimYaw += Input.GetAxis("Mouse X") * sensitivityX;
        aimPitch += Input.GetAxis("Mouse Y") * sensitivityY;
        aimYaw = aimer.limitYaw ? Mathf.Clamp(aimYaw, aimer.minYaw, aimer.maxYaw) : Mathf.Repeat(aimYaw + 180.0f, 360.0f) - 180.0f;
        aimPitch = Mathf.Clamp(aimPitch, aimer.minPitch, aimer.maxPitch);

        // 터렛 회전 중심에서 그 각도로 뻗은 선이 닿는 곳을 조준점으로 삼는다
        Vector3 origin = aimer.HubWorld;
        Vector3 dir = aimer.transform.TransformDirection(Quaternion.Euler(-aimPitch, aimYaw, 0) * Vector3.forward);
        ThirdPersonCamera.AimPoint = ThirdPersonCamera.RaycastIgnoringPlayer(origin, dir, aimRange, out var hit)
            ? hit.point
            : origin + dir * aimRange;

        aimer.AimAt(ThirdPersonCamera.AimPoint);

        // 누르고 있으면 재발사 대기 시간마다 계속 발사한다
        if (Input.GetMouseButton(0))
            shooter.TryFire();
    }
}
