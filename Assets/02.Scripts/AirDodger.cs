using UnityEngine;

// UFO끼리 싸울 때의 움직임. 상대를 정면으로 보면서 적당한 거리를 두고 상하좌우로 흔들리듯 움직이고,
// 날아오는 적 포탄이 자기를 맞출 것 같으면 포탄의 진행 방향에서 벗어나는 쪽으로 피한다
public class AirDodger
{
    public float minRange = 12.0f;      // 이보다 가까우면 물러난다 (두 UFO가 겹치지 않게)
    public float maxRange = 26.0f;      // 이보다 멀면 다가간다
    public float verticalLimit = 4.0f;  // 상대 높이에서 위아래로 벗어날 수 있는 한계
    public float dodgeLookahead = 1.6f; // 이 시간(초) 안에 맞을 포탄만 피한다
    public float dodgeMissDistance = 5.0f; // 포탄이 이 거리 안으로 지나가면 피해야 한다고 본다

    public float accel = 10.0f;         // 속도가 바뀌는 최대 가속도(m/s^2). 방향이 갑자기 꺾이지 않게 한다
    Vector3 vel;                        // 지금 실제 속도
    Vector3 strafe;                     // 지금 옆으로(좌우/상하) 움직이는 방향
    float nextStrafeTime;

    // 이번 프레임의 이동 속도 벡터 (크기 speed). 상대 높이 기준 위아래 한계를 지키려고 현재 높이도 본다
    public Vector3 Step(Vector3 self, Transform target, Team myTeam, float speed)
    {
        Vector3 to = target.position - self;
        Vector3 flatTo = new Vector3(to.x, 0, to.z);
        float d = flatTo.magnitude;
        Vector3 toward = d > 0.01f ? flatTo / d : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, toward);

        // 0.6~1.4초마다 새로 옆으로 움직일 방향을 고른다 (좌우 위주, 가끔 위아래)
        if (Time.time >= nextStrafeTime)
        {
            nextStrafeTime = Time.time + Random.Range(0.6f, 1.4f);
            float lr = Random.value < 0.5f ? -1.0f : 1.0f;
            float ud = Random.value < 0.5f ? 0.0f : (Random.value < 0.5f ? -1.0f : 1.0f);
            strafe = side * lr + Vector3.up * ud * 0.7f;
        }
        Vector3 move = strafe;

        // 거리 조절
        if (d > maxRange) move += toward * 1.5f;
        else if (d < minRange) move -= toward * 1.5f;

        // 높이 한계: 상대 높이에서 너무 벗어나면 되돌아온다
        float dy = self.y - target.position.y;
        if (dy > verticalLimit) move += Vector3.down * 1.5f;
        else if (dy < -verticalLimit) move += Vector3.up * 1.5f;

        // 날아오는 적 포탄 피하기
        Vector3 dodge = DodgeDirection(self, myTeam);
        if (dodge != Vector3.zero) move = dodge * 3.0f + move * 0.2f;

        Vector3 desired = move.sqrMagnitude < 0.0001f ? Vector3.zero : move.normalized * speed;
        vel = Vector3.MoveTowards(vel, desired, accel * Time.deltaTime);
        return vel;
    }

    // 자기를 맞출 것 같은 가장 급한 적 포탄을 찾아 그 궤적에서 벗어나는 방향을 돌려준다. 없으면 zero
    Vector3 DodgeDirection(Vector3 self, Team myTeam)
    {
        Vector3 best = Vector3.zero;
        float bestTime = dodgeLookahead;
        foreach (var p in Projectile.All)
        {
            if (p == null || p.OwnerTeam == myTeam) continue;
            Vector3 v = p.Velocity;
            float speed = v.magnitude;
            if (speed < 0.1f) continue;
            Vector3 vDir = v / speed;
            Vector3 rel = self - p.transform.position;
            float along = Vector3.Dot(rel, vDir);
            if (along <= 0) continue;                       // 이미 지나갔다
            float tHit = along / speed;
            if (tHit >= bestTime) continue;
            Vector3 off = rel - vDir * along;               // 궤적에서 자기까지의 수직 벡터
            if (off.magnitude > dodgeMissDistance) continue; // 어차피 빗나간다
            bestTime = tHit;
            if (off.sqrMagnitude < 0.01f) off = Vector3.Cross(vDir, Random.value < 0.5f ? Vector3.up : Vector3.right);
            best = off.normalized;
        }
        return best;
    }
}
