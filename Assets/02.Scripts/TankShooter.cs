using UnityEngine;

// 포탄 발사 담당. 마우스 조준과 AI 조준이 함께 사용한다
public class TankShooter : MonoBehaviour
{
    public Transform firePos;
    public GameObject projectilePrefab; // 비어 있으면 기존 Resources/Cannon을 쓴다
    public float cooldown = 1.5f;       // 재발사 대기 시간(초)

    GameObject cannon;
    AudioClip fireSfx;
    TeamMember me;
    float nextFireTime;

    void Awake()
    {
        cannon = (GameObject)Resources.Load("Cannon");
        fireSfx = Resources.Load<AudioClip>("CannonFire");
        me = GetComponent<TeamMember>();
    }

    public bool TryFire()
    {
        var prefab = projectilePrefab != null ? projectilePrefab : cannon;
        if (Time.time < nextFireTime || firePos == null || prefab == null)
            return false;

        nextFireTime = Time.time + cooldown;
        if (fireSfx != null)
            AudioSource.PlayClipAtPoint(fireSfx, firePos.position, 1.0f);

        var go = Instantiate(prefab, firePos.position, firePos.rotation);
        var p = go.GetComponent<Projectile>();
        if (p != null && me != null) p.Init(me.team);
        return true;
    }
}
