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
        Vector3 move = forward * v * (v >= 0 ? moveSpeed : reverseSpeed);

        if (cc.isGrounded) verticalVelocity = -2.0f;
        else verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        cc.Move(move * Time.deltaTime);
    }
}
