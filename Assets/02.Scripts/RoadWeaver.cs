using UnityEngine;

// MegaBall이 도로나 다리를 따라갈 때 경로 좌우로 흔들리듯 움직이게 한다 (일직선으로 줄지어 가는 다른 탱크 옆에서 자유롭게 움직인다).
// 좌우 여유(도로 폭)를 재서 호수, 소품, 큰 높이 차이가 있는 쪽으로는 가지 않는다
public class RoadWeaver
{
    public float amplitude = 5.0f;      // 경로에서 좌우로 벗어나는 최대 거리(m). 실제로는 잰 도로 폭 안에서만
    public float sideSpeed = 3.5f;      // 좌우 이동 속도(m/s)
    public float edgeMargin = 1.5f;     // 도로 가장자리에서 띄울 거리

    float lateral;                      // 경로 기준 현재 좌우 이동량 (오른쪽이 +)
    float seed;                         // 탱크마다 다른 흔들림 위상 (MonoBehaviour 생성자에서 Random을 못 쓰므로 처음 쓸 때 정한다)
    bool seeded;
    float clearLeft, clearRight, nextClearTime;

    public float Lateral => lateral;

    // 진행 방향(flatDir)이 정해진 프레임마다 호출한다. 위치를 옆으로 옮긴다
    public void Apply(Transform t, Vector3 flatDir)
    {
        if (flatDir.sqrMagnitude < 0.0001f) return;
        if (!seeded) { seed = Random.value * 100.0f; seeded = true; }
        flatDir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, flatDir);

        // 좌우 여유를 0.25초마다 잰다. 지금 위치 기준이므로 lateral을 빼서 경로 중심 기준으로 환산한다
        if (Time.time >= nextClearTime)
        {
            nextClearTime = Time.time + 0.25f;
            clearRight = Clearance(t.position, right, amplitude + edgeMargin) - lateral;
            clearLeft = Clearance(t.position, -right, amplitude + edgeMargin) + lateral;
        }
        float maxRight = Mathf.Clamp(clearRight - edgeMargin, 0.0f, amplitude);
        float maxLeft = Mathf.Clamp(clearLeft - edgeMargin, 0.0f, amplitude);

        // 두 개의 느린 사인파를 섞어 -1~1을 고루 오가게 한다 (펄린 노이즈는 0 근처에 몰린다)
        float time = Time.time + seed;
        float s = Mathf.Sin(time * 0.8f) * 0.6f + Mathf.Sin(time * 0.33f + 1.7f) * 0.4f;
        float want = s >= 0 ? s * maxRight : s * maxLeft;

        float next = Mathf.MoveTowards(lateral, want, sideSpeed * Time.deltaTime);
        t.position += right * (next - lateral);
        lateral = next;
    }

    // origin에서 dir 방향으로 1m씩 가며 갈 수 있는 거리. 호수/땅 없음/소품/큰 높이 차이를 만나면 거기서 멈춘다
    static float Clearance(Vector3 origin, Vector3 dir, float max)
    {
        float free = 0.0f;
        for (float d = 1.0f; d <= max; d += 1.0f)
        {
            Vector3 p = origin + dir * d;
            if (!Physics.Raycast(p + Vector3.up * 6.0f, Vector3.down, out RaycastHit h, 14.0f, ~0, QueryTriggerInteraction.Ignore)) break;
            if (h.collider.name.Trim().StartsWith("Lake") || PropGuard.IsProp(h.collider)) break;
            if (Mathf.Abs(h.point.y - origin.y) > 1.5f) break;
            free = d;
        }
        return free;
    }
}
