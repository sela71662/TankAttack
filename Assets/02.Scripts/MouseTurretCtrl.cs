using UnityEngine;

// 마우스로 터렛을 조준하고 좌클릭으로 발사한다 (Tank_Alternative_Model 전용)
[RequireComponent(typeof(TurretAimer), typeof(TankShooter))]
public class MouseTurretCtrl : MonoBehaviour
{
    public LayerMask groundMask = 1 << 8; // Terrain 레이어
    TurretAimer aimer;
    TankShooter shooter;

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 point;
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundMask))
        {
            point = hit.point;
        }
        else
        {
            // 지면 레이어가 없으면 터렛 높이의 수평면과 교차한 지점으로 조준
            var plane = new Plane(Vector3.up, aimer.HubWorld);
            if (!plane.Raycast(ray, out float enter)) return;
            point = ray.GetPoint(enter);
        }

        aimer.AimAt(point);

        if (Input.GetMouseButtonDown(0))
            shooter.TryFire();
    }
}
