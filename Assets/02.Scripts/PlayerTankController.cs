using UnityEngine;

// 플레이어 탱크 차체 조종. W/S 전진 후진, A/D 제자리 회전 (탱크 방식)
[RequireComponent(typeof(CharacterController))]
public class PlayerTankController : MonoBehaviour
{
    public static PlayerTankController Instance { get; private set; }

    public float moveSpeed = 13.0f;
    public float reverseSpeed = 7.0f;
    public float turnSpeed = 100.0f; // 도/초
    public float gravity = -25.0f;
    public float speedScale = 1.0f; // 아이템 효과로 바뀌는 이동 속도 배율

    CharacterController cc;
    GroundAlign align;
    Vector3 baseCenter; // 프리팹의 center (차체 정위치 기준)
    float verticalVelocity;

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        baseCenter = cc.center;
        align = GetComponent<GroundAlign>();
        if (align != null) align.frontBackOnly = true; // 플레이어는 바로 앞뒤 경사만 본다
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 지면에 맞춰 기울어진 차체여도 좌우 회전은 월드 위쪽 축 기준으로 한다
        transform.Rotate(Vector3.up, h * turnSpeed * Time.deltaTime, Space.World);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        KeepCapsuleUpright(forward);

        Vector3 flatVel = forward * v * (v >= 0 ? moveSpeed : reverseSpeed) * speedScale;

        // 바로 앞뒤 지면의 경사만 보고, 오르막/내리막에서도 지면에 붙어 움직이도록 상하 이동을 더한다.
        // 수평 이동 방향은 바꾸지 않는다 (옆 경사나 주변 경사로 끌려가지 않는다)
        bool grounded = cc.isGrounded;
        float slopeY = 0;
        if (grounded) verticalVelocity = -2.0f;
        else verticalVelocity += gravity * Time.deltaTime;
        if (grounded && align != null)
        {
            float maxSlope = Mathf.Tan(cc.slopeLimit * Mathf.Deg2Rad);
            slopeY = Vector3.Dot(flatVel, forward) * Mathf.Clamp(align.forwardSlope, -maxSlope, maxSlope);
        }

        Vector3 delta = new Vector3(flatVel.x, 0, flatVel.z) * Time.deltaTime;
        float flatLen0 = delta.magnitude;

        // 호수와 닿은 섬 가장자리(단차)로는 들어갈 수 없다. 막히면 벽을 따라 미끄러진다
        Vector3 pos = transform.position;
        if (ShoreBlocked(pos, delta))
        {
            if (!ShoreBlocked(pos, new Vector3(delta.x, 0, 0))) delta.z = 0;
            else if (!ShoreBlocked(pos, new Vector3(0, 0, delta.z))) delta.x = 0;
            else { delta.x = 0; delta.z = 0; }
        }
        // 나무, 돌, 건물 위로는 올라갈 수 없다. 이미 올라가 있으면 내려오는 이동은 막지 않는다
        if (!PropGuard.PropBelow(pos, transform))
        {
            Vector3 lead = pos + delta + delta.normalized * 1.2f;
            if (PropGuard.PropBelow(pos + delta, transform) || PropGuard.PropBelow(lead, transform))
            {
                delta.x = 0; delta.z = 0;
            }
        }

        // 막혀서 줄어든 만큼 경사 방향의 상하 이동도 줄인다
        float keep = flatLen0 > 1e-6f ? delta.magnitude / flatLen0 : 0;
        delta.y = (slopeY * keep + verticalVelocity) * Time.deltaTime;
        cc.Move(delta);
    }

    // CharacterController의 center는 차체 회전을 따라가서, 경사에 맞춰 차체가 기울면 충돌 캡슐이 옆으로 밀린다.
    // 기울어도 캡슐이 차체 위치 바로 위에 서 있도록 center를 매 프레임 보정한다
    void KeepCapsuleUpright(Vector3 flatForward)
    {
        if (flatForward.sqrMagnitude < 1e-4f) return;
        Quaternion yawOnly = Quaternion.LookRotation(flatForward, Vector3.up);
        cc.center = Quaternion.Inverse(transform.rotation) * (yawOnly * baseCenter);
    }

    // 차체 중심이 물가 구역 안으로 더 들어가는가. 다리 위와 입구에서는 앞쪽 검사를 하지 않는다
    // (좁은 다리에서 차체가 조금만 비스듬해도 앞쪽 지점이 다리 가장자리 칸에 걸려 이동이 전부 막힌다)
    static bool ShoreBlocked(Vector3 pos, Vector3 flat)
    {
        if (flat.sqrMagnitude < 1e-8f) return false;
        if (ShoreZone.Blocks(pos, pos + flat)) return true;
        if (ShoreZone.Distance(pos) >= 255.0f) return false;
        return ShoreZone.Blocks(pos, pos + flat + flat.normalized * 0.8f);
    }

    // 바로 아래 가장 가까운 표면이 호수인가 (다리 아래로 떨어진 경우처럼 지도상 물 칸이 아니어도 잡는다)
    bool OnWaterSurface(Vector3 pos)
    {
        float best = float.MaxValue;
        Collider top = null;
        foreach (var h in Physics.RaycastAll(pos + Vector3.up * 1.0f, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; top = h.collider; }
        }
        return top != null && top.name.Trim().StartsWith("Lake");
    }

    // 어떤 이유로든 물에 빠졌으면 가장 가까운 안전한 땅으로 옮긴다
    void LateUpdate()
    {
        Vector3 pos = transform.position;
        if (!(ShoreZone.IsWater(pos) || OnWaterSurface(pos)) || !ShoreZone.FindSafe(pos, out Vector3 safe)) return;

        // 안전한 칸의 지면 높이 (소품이 아닌 가장 높은 표면)
        float ground = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(safe + Vector3.up * 60.0f, Vector3.down, 200.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || PropGuard.IsProp(h.collider) || h.collider.name.Trim().StartsWith("Lake")) continue;
            ground = Mathf.Max(ground, h.point.y);
        }
        if (float.IsNegativeInfinity(ground)) return;
        cc.enabled = false;
        transform.position = new Vector3(safe.x, ground + 0.5f, safe.z);
        cc.enabled = true;
        verticalVelocity = 0;
    }
}
