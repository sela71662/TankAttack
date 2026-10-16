using UnityEngine;

// 플레이어 터렛 조준과 발사. 마우스가 화면 위의 조준선(커서)을 자유롭게 움직이고, 터렛과 포신이 그 조준선이 가리키는 지점으로 돌아간다.
// 좌클릭으로 발사한다. 터렛은 차체 정면 기준 좌우 yawLimit 안에서만 돌아가고, 위아래는 TurretAimer의 피치 제한을 따른다
[RequireComponent(typeof(TurretAimer), typeof(TankShooter))]
public class PlayerTurretCtrl : MonoBehaviour
{
    public float cursorSpeed = 60.0f;   // 마우스 축 1당 조준선이 움직이는 픽셀 (화면 높이 1080 기준)
    public float aimRange = 300.0f;
    public float yawLimit = 60.0f;      // 차체 정면 기준 좌우 몇 도까지 조준하고 쏠 수 있는가

    TurretAimer aimer;
    TankShooter shooter;
    Camera cam;
    Vector2 cursor;                     // 조준선의 화면 위치 (픽셀, 왼쪽 아래가 원점)
    bool cursorInit;

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
        aimer.limitYaw = true;
        aimer.minYaw = -yawLimit;
        aimer.maxYaw = yawLimit;
    }

    void OnDisable()
    {
        ThirdPersonCamera.UseCursor = false;
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return; // 커서가 풀려 있으면(메뉴 상태) 조작하지 않는다
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (!cursorInit)
        {
            cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            cursorInit = true;
        }

        // 마우스로 조준선을 화면 안에서 움직인다
        float scale = cursorSpeed * Screen.height / 1080.0f;
        cursor.x = Mathf.Clamp(cursor.x + Input.GetAxis("Mouse X") * scale, 0, Screen.width);
        cursor.y = Mathf.Clamp(cursor.y + Input.GetAxis("Mouse Y") * scale, 0, Screen.height);
        ThirdPersonCamera.CursorPos = cursor;
        ThirdPersonCamera.UseCursor = true;

        // 조준선이 가리키는 월드 지점. 탱크 뒤쪽(카메라와 탱크 사이)의 물체에 가로막히지 않게 탱크 깊이부터 쏜다
        Vector3 hub = aimer.HubWorld;
        Ray ray = cam.ScreenPointToRay(cursor);
        Vector3 origin = ray.origin + ray.direction * Mathf.Max(0.0f, Vector3.Dot(hub - ray.origin, ray.direction));
        Vector3 point = ThirdPersonCamera.RaycastIgnoringPlayer(origin, ray.direction, aimRange, out var hit)
            ? hit.point
            : origin + ray.direction * aimRange;
        ThirdPersonCamera.AimPoint = point;

        // 터렛 회전 중심에서 그 지점으로 향하는 각도로 바로 돌린다 (각도 제한은 TurretAimer가 처리한다)
        Vector3 d = aimer.transform.InverseTransformDirection(point - hub);
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        aimer.SnapTo(yaw, pitch);

        // 누르고 있으면 재발사 대기 시간마다 계속 발사한다
        if (Input.GetMouseButton(0))
            shooter.TryFire();
    }
}
