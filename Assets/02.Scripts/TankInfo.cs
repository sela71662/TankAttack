using UnityEngine;

public enum TankKind
{
    Alternative, // 플레이어가 직접 조종하는 탱크
    Original,
    Light,
    Heavy,
    MegaBall,
    UFO
}

// 탱크 종류와 이동 관련 기본 수치. 스폰 시 EnemyMover/FlyingMover가 읽는다
public class TankInfo : MonoBehaviour
{
    public TankKind kind;
    public bool flying = false;         // true면 지형을 무시하고 날아서 이동 (UFO)
    public float moveSpeed = 6.0f;      // 기본 이동 속도(m/s)
    public float stopDistance = 20.0f;  // 코어와 이 거리 안으로 들어오면 멈춘다
    public float flyHeight = 6.0f;      // 비행형: 코어보다 이만큼 높은 고도로 난다
}
