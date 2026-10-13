using UnityEngine;

public enum ProjectileKind
{
    Armor,  // 직선으로 빠르게 날아가는 철갑탄
    Shell,  // 느리고 닿으면 폭발해 범위 피해를 주는 포탄
    Ball,   // 터지지 않고 튕겨 다니는 쇠구슬
    Plasma  // 가장 가까운 적을 향해 휘어 날아가는 호밍 구체
}

// 모든 탱크 포탄의 공통 동작. 종류(kind)에 따라 충돌 처리와 비행 방식이 달라진다
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    public ProjectileKind kind = ProjectileKind.Armor;
    public float speed = 40.0f;
    public float damage = 20.0f;
    public float lifetime = 4.0f;
    public float splashRadius = 0.0f;   // Shell: 폭발 반경
    public int maxBounces = 8;          // Ball: 이 횟수만큼 튕기면 사라진다
    public float turnSpeed = 120.0f;    // Plasma: 초당 회전 각도
    public bool attackStructures = false; // Plasma: 적 탱크가 없을 때 게이트 같은 구조물을 노린다
    public GameObject expEffect;

    Team team;
    float damageMul = 1.0f;
    Rigidbody rb;
    int bounces;
    Transform target;
    float nextSearchTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 발사한 탱크가 Instantiate 직후 호출한다
    public void Init(Team ownerTeam)
    {
        team = ownerTeam;
        damageMul = TeamConfig.DamageMultiplier(ownerTeam);
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (kind != ProjectileKind.Plasma) return;

        if (target == null && Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + 0.3f;
            target = FindTarget();
        }
        if (target == null) return;

        Vector3 toTarget = (target.position + Vector3.up * 1.2f) - transform.position;
        Vector3 dir = Vector3.RotateTowards(rb.linearVelocity.normalized, toTarget.normalized,
                                            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0);
        rb.linearVelocity = dir * speed;
        transform.forward = dir;
    }

    Transform FindTarget()
    {
        Transform bestTank = null, bestStruct = null;
        float tankDist = float.MaxValue, structDist = float.MaxValue;
        foreach (var t in FindObjectsByType<TeamMember>())
        {
            if (t.team == team) continue;
            float d = (t.transform.position - transform.position).sqrMagnitude;
            if (t.isStructure)
            {
                if (d < structDist) { structDist = d; bestStruct = t.transform; }
            }
            else if (d < tankDist) { tankDist = d; bestTank = t.transform; }
        }
        if (bestTank != null) return bestTank;
        return attackStructures ? bestStruct : null;
    }

    // 트리거 충돌: Armor, Shell, Plasma
    void OnTriggerEnter(Collider other)
    {
        if (kind == ProjectileKind.Ball || other.isTrigger) return;
        if (other.GetComponentInParent<Projectile>() != null) return;

        var tm = other.GetComponentInParent<TeamMember>();
        if (tm != null && tm.team == team) return; // 아군은 통과

        Detonate(SafeClosestPoint(other), tm);
    }

    // ClosestPoint는 Box/Sphere/Capsule/볼록 Mesh 콜라이더에서만 쓸 수 있다 (지형 메시에서는 경고가 난다)
    Vector3 SafeClosestPoint(Collider c)
    {
        bool supported = c is BoxCollider || c is SphereCollider || c is CapsuleCollider
                         || (c is MeshCollider mc && mc.convex);
        return supported ? c.ClosestPoint(transform.position) : transform.position;
    }

    // 물리 충돌: Ball은 터지지 않고 튕긴다
    void OnCollisionEnter(Collision c)
    {
        if (kind != ProjectileKind.Ball) return;

        var tm = c.collider.GetComponentInParent<TeamMember>();
        if (tm != null && tm.team != team)
            DamageTarget(c.collider.GetComponentInParent<Health>());

        if (++bounces > maxBounces)
            Explode(transform.position);
    }

    void Detonate(Vector3 point, TeamMember hit)
    {
        if (splashRadius > 0)
        {
            foreach (var col in Physics.OverlapSphere(point, splashRadius))
            {
                var h = col.GetComponentInParent<Health>();
                var tm = col.GetComponentInParent<TeamMember>();
                if (h != null && tm != null && tm.team != team && !h.IsDead)
                    DamageOnce(h);
            }
        }
        else if (hit != null)
        {
            DamageTarget(hit.GetComponent<Health>());
        }
        Explode(point);
    }

    // 같은 탱크가 여러 콜라이더로 중복 피해를 받지 않게 한다
    readonly System.Collections.Generic.HashSet<Health> damaged = new System.Collections.Generic.HashSet<Health>();
    void DamageOnce(Health h)
    {
        if (damaged.Add(h)) DamageTarget(h);
    }

    void DamageTarget(Health h)
    {
        if (h != null) h.TakeDamage(damage * damageMul);
    }

    void Explode(Vector3 point)
    {
        if (expEffect != null)
            Destroy(Instantiate(expEffect, point, Quaternion.identity), 1.0f);
        Destroy(gameObject);
    }
}
