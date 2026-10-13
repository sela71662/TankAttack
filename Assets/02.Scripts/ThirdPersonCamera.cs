using UnityEngine;

// 3인칭 카메라. 마우스로 탱크 주위를 돌고, 화면 중앙 조준점이 가리키는 지점(AimPoint)을 계산한다.
// 메인 카메라에 붙인다. 커서는 잠기며 Esc로 풀고, 화면을 클릭하면 다시 잠긴다
public class ThirdPersonCamera : MonoBehaviour
{
    public static Vector3 AimPoint { get; private set; }

    public float distance = 9.0f;
    public float pivotHeight = 3.2f;   // 탱크 위쪽에서 바라볼 높이
    public float sensitivityX = 3.0f;
    public float sensitivityY = 2.0f;
    public float minPitch = -8.0f;
    public float maxPitch = 55.0f;
    public float followSmooth = 14.0f;
    public float aimRange = 300.0f;

    Transform target;
    float yaw;
    float pitch = 18.0f;

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

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivityX;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivityY, minPitch, maxPitch);
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

        transform.position = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(followSmooth * Time.deltaTime));
        transform.rotation = rot;

        // 화면 중앙이 가리키는 지점
        var cam = GetComponent<Camera>();
        Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) : new Ray(transform.position, transform.forward);
        AimPoint = RaycastIgnoringPlayer(ray.origin, ray.direction, aimRange, out var aimHit)
            ? aimHit.point
            : ray.origin + ray.direction * aimRange;
    }

    // 화면 중앙 조준점 표시
    void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(cx - 8, cy - 1, 16, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 1, cy - 8, 2, 16), Texture2D.whiteTexture);
    }
}
