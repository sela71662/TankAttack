using UnityEngine;

// 플레이어 탱크 차체 조종. W/S 전진 후진, A/D 제자리 회전 (탱크 방식)
[RequireComponent(typeof(CharacterController))]
public class PlayerTankController : MonoBehaviour
{
    public static PlayerTankController Instance { get; private set; }

    public float moveSpeed = 9.0f;
    public float reverseSpeed = 5.0f;
    public float turnSpeed = 100.0f; // 도/초
    public float gravity = -25.0f;
    public float speedScale = 1.0f; // 아이템 효과로 바뀌는 이동 속도 배율

    CharacterController cc;
    float verticalVelocity;

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
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
        Vector3 move = forward * v * (v >= 0 ? moveSpeed : reverseSpeed) * speedScale;

        if (cc.isGrounded) verticalVelocity = -2.0f;
        else verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        Vector3 delta = move * Time.deltaTime;

        // 호수와 닿은 섬 가장자리(단차)로는 들어갈 수 없다. 막히면 벽을 따라 미끄러진다
        Vector3 pos = transform.position;
        if (ShoreBlocked(pos, new Vector3(delta.x, 0, delta.z)))
        {
            if (!ShoreBlocked(pos, new Vector3(delta.x, 0, 0))) delta.z = 0;
            else if (!ShoreBlocked(pos, new Vector3(0, 0, delta.z))) delta.x = 0;
            else { delta.x = 0; delta.z = 0; }
        }
        // 나무, 돌, 건물 위로는 올라갈 수 없다. 이미 올라가 있으면 내려오는 이동은 막지 않는다
        if (!PropGuard.PropBelow(pos, transform))
        {
            Vector3 flat = new Vector3(delta.x, 0, delta.z);
            Vector3 lead = pos + flat + flat.normalized * 1.2f;
            if (PropGuard.PropBelow(pos + flat, transform) || PropGuard.PropBelow(lead, transform))
            {
                delta.x = 0; delta.z = 0;
            }
        }
        cc.Move(delta);
    }

    // 차체 중심과 진행 방향 앞쪽 끝이 물가 구역 안으로 더 들어가는가
    static bool ShoreBlocked(Vector3 pos, Vector3 flat)
    {
        if (flat.sqrMagnitude < 1e-8f) return false;
        Vector3 lead = pos + flat + flat.normalized * 1.5f;
        return ShoreZone.Blocks(pos, pos + flat) || ShoreZone.Blocks(pos, lead);
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
