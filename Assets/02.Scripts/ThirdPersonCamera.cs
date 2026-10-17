using UnityEngine;

// 3인칭 카메라. 차체 뒤에서 차체가 향한 방향을 보고(마우스 상하로 높이 조절), 화면 중앙 조준점이 가리키는 지점(AimPoint)을 계산한다.
// followHull을 끄면 예전처럼 마우스로 탱크 주위를 돈다.
// 메인 카메라에 붙인다. 커서는 잠기며 Esc로 풀고, 화면을 클릭하면 다시 잠긴다
public class ThirdPersonCamera : MonoBehaviour
{
    public static Vector3 AimPoint { get; set; }
    public static bool UseCursor { get; set; }       // true면 조준선을 CursorPos(화면 픽셀, 왼쪽 아래가 원점)에 그린다
    public static Vector2 CursorPos { get; set; }

    public float distance = 20.0f;
    public float pivotHeight = 5.5f;   // 탱크 위쪽에서 바라볼 높이
    public float sensitivityX = 3.0f;
    public float sensitivityY = 2.0f;
    public float minPitch = -8.0f;
    public float maxPitch = 55.0f;
    public float aimRange = 300.0f;
    public bool followHull = true;     // true면 카메라가 항상 차체 뒤에서 차체가 향한 방향을 본다 (마우스 좌우는 쓰지 않는다)

    Transform target;
    float yaw;
    float pitch = 18.0f;
    bool placed;      // 처음 위치를 잡았는가
    bool started;     // 플레이어가 움직이기 시작해서 카메라가 따라가는 중인가
    Vector3 startPos; // 처음 위치를 잡았을 때의 플레이어 위치

    void OnEnable()
    {
        LockCursor(true);
    }

    void OnDisable()
    {
        LockCursor(false);
    }

    static void LockCursor(bool on)
    {
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }

    // 플레이어 몸통에 가리지 않도록, 플레이어 콜라이더를 제외한 가장 가까운 충돌을 찾는다
    public static bool RaycastIgnoringPlayer(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;
        foreach (var h in Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<PlayerTankController>() != null) continue;
            // 날아가는 포탄과 지뢰는 조준점을 가로채지 않는다 (쏜 포탄을 따라 조준선이 튄다)
            if (h.collider.GetComponentInParent<Projectile>() != null || h.collider.GetComponentInParent<Mine>() != null) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var p = PlayerTankController.Instance;
            if (p == null) return;
            target = p.transform;
            yaw = target.eulerAngles.y;
        }

        if (followHull)
        {
            // 지면에 맞춰 기울어진 차체라도 수평 방향만 쓴다
            Vector3 f = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (f.sqrMagnitude > 1e-4f)
                yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; // 차체 방향을 지연 없이 그대로 따른다
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // followHull일 때 마우스는 터렛(PlayerTurretCtrl)만 움직이고, 카메라는 차체 방향과 고정 각도를 유지한다
            if (!followHull)
            {
                yaw += Input.GetAxis("Mouse X") * sensitivityX;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivityY, minPitch, maxPitch);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            LockCursor(true);
        }

        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 desired = pivot - rot * Vector3.forward * distance;

        // 벽이나 지형에 가려지면 카메라를 앞으로 당긴다
        Vector3 toCam = desired - pivot;
        if (RaycastIgnoringPlayer(pivot, toCam.normalized, toCam.magnitude, out var hit))
            desired = hit.point + hit.normal * 0.3f;

        // 시작할 때 카메라가 저절로 움직이지 않도록, 처음에 플레이어 뒤로 한 번에 놓고 플레이어가 움직이기 시작할 때부터 따라간다
        if (!started)
        {
            if (!placed)
            {
                transform.position = desired;
                transform.rotation = rot;
                placed = true;
                startPos = target.position;
            }
            else if (!followHull || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f
                     || (target.position - startPos).sqrMagnitude > 9.0f)
            {
                started = true; // 키 입력이 있거나 밀려나서 3m 넘게 움직였다
            }
        }
        if (started)
        {
            transform.position = desired; // 플레이어 움직임에 딱 맞게 지연 없이 따라간다
            transform.rotation = rot;
        }

        // followHull이면 조준점은 PlayerTurretCtrl이 터렛이 향한 방향으로 정한다. 아니면 화면 중앙이 가리키는 지점
        if (!followHull)
        {
            var cam = GetComponent<Camera>();
            Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) : new Ray(transform.position, transform.forward);
            AimPoint = RaycastIgnoringPlayer(ray.origin, ray.direction, aimRange, out var aimHit)
                ? aimHit.point
                : ray.origin + ray.direction * aimRange;
        }
    }

    // 조준점 표시. 터렛 조준이면 터렛이 향한 지점이 화면 어디인지에 그린다
    void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked || MapView.IsOpen) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        var cam = GetComponent<Camera>();
        if (UseCursor)
        {
            // 플레이어가 마우스로 움직이는 조준선
            cx = CursorPos.x; cy = Screen.height - CursorPos.y;
        }
        else if (followHull && cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(AimPoint);
            if (sp.z <= 0) return; // 조준점이 카메라 뒤면 그리지 않는다 (화면 중앙으로 튀지 않게)
            cx = sp.x; cy = Screen.height - sp.y;
        }
        UiKit.Crosshair(cx, cy);
    }
}
