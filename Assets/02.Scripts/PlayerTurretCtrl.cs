using UnityEngine;

// 플레이어 터렛 조준과 발사. 화면 중앙 조준점(ThirdPersonCamera.AimPoint)을 향해 터렛이 돌고, 좌클릭으로 발사한다
[RequireComponent(typeof(TurretAimer), typeof(TankShooter))]
public class PlayerTurretCtrl : MonoBehaviour
{
    TurretAimer aimer;
    TankShooter shooter;

    void Awake()
    {
        aimer = GetComponent<TurretAimer>();
        shooter = GetComponent<TankShooter>();
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return; // 커서가 풀려 있으면(메뉴 상태) 조작하지 않는다

        aimer.AimAt(ThirdPersonCamera.AimPoint);

        // 누르고 있으면 재발사 대기 시간마다 계속 발사한다
        if (Input.GetMouseButton(0))
            shooter.TryFire();
    }
}
