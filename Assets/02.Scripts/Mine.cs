using UnityEngine;

// 지뢰. 적 팀 탱크가 밟았을 때만 터진다
[RequireComponent(typeof(Rigidbody))]
public class Mine : MonoBehaviour
{
    public float damage = 50.0f;
    public float radius = 4.0f;
    public float lifetime = 60.0f;
    public GameObject expEffect;

    Team team;
    float damageMul = 1.0f;

    public void Init(Team ownerTeam)
    {
        team = ownerTeam;
        damageMul = TeamConfig.DamageMultiplier(ownerTeam);

        // 설치한 팀의 색: 적은 빨강, 아군은 노랑 (renderer.material은 이 지뢰만의 인스턴스)
        var c = TeamConfig.IsAlly(ownerTeam) ? new Color(1.0f, 0.85f, 0.1f) : new Color(1.0f, 0.1f, 0.05f);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var m = r.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.8f);
            }
        }
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        var tm = other.GetComponentInParent<TeamMember>();
        // 아군과 구조물은 지뢰를 밟아도 터지지 않는다
        if (tm == null || tm.team == team || tm.isStructure) return;

        var hit = new System.Collections.Generic.HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(transform.position, radius))
        {
            var h = col.GetComponentInParent<Health>();
            var t = col.GetComponentInParent<TeamMember>();
            if (h != null && t != null && t.team != team && hit.Add(h))
                h.TakeDamage(damage * damageMul);
        }

        if (expEffect != null)
            Destroy(Instantiate(expEffect, transform.position, Quaternion.identity), 1.0f);
        Destroy(gameObject);
    }
}
