using UnityEngine;

public class CoreAnimator : MonoBehaviour
{
    public float rotationSpeed = 45.0f; // Y축 회전 속도 (도/초)
    public float hoverHeight = 0.5f;    // 부유 폭
    public float hoverSpeed = 2.0f;     // 부유 속도

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 제자리 Y축 회전
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);

        // Sin 파형으로 위아래 부유
        var pos = startPos;
        pos.y += Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        transform.position = pos;
    }
}
