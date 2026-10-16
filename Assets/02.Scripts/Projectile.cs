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
    public float expEffectScale = 1.0f; // 폭발 이펙트 파티클 크기 배율
    public GameObject bounceEffect;     // Ball: 튕길 때마다 터지는 작은 불꽃
    public bool addTrail = true;        // 종류별 색 꼬리
    public Color trailColor = new Color(0, 0, 0, 0); // 알파가 0보다 크면 종류별 기본색 대신 이 색을 쓴다

    public bool tintByTeam = false;     // 켜면 포탄 본체와 꼬리가 팀색(적 빨강, 아군 노랑)이 된다

    // 날고 있는 모든 포탄. UFO가 날아오는 포탄을 피할 때 쓴다
    public static readonly System.Collections.Generic.List<Projectile> All = new System.Collections.Generic.List<Projectile>();
    public Team OwnerTeam => team;
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    Team team;
    float damageMul = 1.0f;
    Rigidbody rb;
    int bounces;
    Transform target;
    float nextSearchTime;
    Vector3 lastPos;
    float sweepRadius;
    bool exploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 발사한 탱크가 Instantiate 직후 호출한다
    public void Init(Team ownerTeam, float extraDamage = 1.0f)
    {
        team = ownerTeam;
        damageMul = TeamConfig.DamageMultiplier(ownerTeam) * extraDamage;
        if (tintByTeam) TintBody();
    }

    Color TeamColor()
    {
        return TeamConfig.IsAlly(team) ? new Color(1.0f, 0.85f, 0.1f) : new Color(1.0f, 0.1f, 0.05f);
    }

    // 프리팹 머티리얼을 건드리지 않도록 인스턴스(renderer.material)에 팀색을 입힌다
    void TintBody()
    {
        var c = TeamColor();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var m = r.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.5f);
            }
        }
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        lastPos = transform.position;
        var sc = GetComponent<SphereCollider>();
        sweepRadius = sc != null ? sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) : 0.2f;
        Destroy(gameObject, lifetime);
        if (addTrail) AddTrail();
    }

    static Material trailMat;

    // 종류마다 색과 굵기가 다른 꼬리를 붙여서 날아가는 포탄을 눈으로 구분하게 한다
    void AddTrail()
    {
        Color c; float width, time;
        switch (kind)
        {
            case ProjectileKind.Shell:  c = new Color(1.0f, 0.55f, 0.1f); width = 0.5f;  time = 0.45f; break;
            case ProjectileKind.Ball:   c = new Color(0.6f, 0.8f, 1.0f);  width = 0.6f;  time = 0.35f; break;
            case ProjectileKind.Plasma: c = new Color(0.4f, 1.0f, 0.9f);  width = 0.35f; time = 0.6f;  break;
            default:                    c = new Color(1.0f, 0.95f, 0.5f); width = 0.12f; time = 0.12f; break;
        }
        if (tintByTeam) c = TeamColor();
        if (trailColor.a > 0f) c = trailColor;
        if (trailMat == null) trailMat = new Material(Shader.Find("Sprites/Default"));

        var tr = gameObject.AddComponent<TrailRenderer>();
        tr.sharedMaterial = trailMat;
        tr.time = time;
        // 부모 스케일에 곱해지지 않도록 월드 크기로 맞춘다
        float s = Mathf.Max(transform.lossyScale.x, 0.01f);
        tr.widthMultiplier = width / s;
        tr.startColor = c;
        tr.endColor = new Color(c.r, c.g, c.b, 0f);
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        if (kind == ProjectileKind.Plasma)
        {
            var l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = 8f;
            l.intensity = 1.5f;
        }
    }

    void FixedUpdate()
    {
        if (kind != ProjectileKind.Ball) SweepHit();
        if (kind != ProjectileKind.Plasma) return;

        if (target == null && Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + 0.3f;
            target = FindTarget();
        }
        if (target == null) return;

        // 구조물(게이트는 아치)은 콜라이더 위쪽을 노린다. 가운데는 빈 곳이라 통과해 버린다
        Vector3 aim = target.position + Vector3.up * 1.2f;
        var tm = target.GetComponent<TeamMember>();
        var col = tm != null && tm.isStructure ? target.GetComponentInChildren<Collider>() : null;
        if (col != null && col.enabled) aim = col.bounds.center + Vector3.up * (col.bounds.extents.y * 0.5f);
        Vector3 toTarget = aim - transform.position;
        Vector3 dir = Vector3.RotateTowards(rb.linearVelocity.normalized, toTarget.normalized,
                                            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0);
        rb.linearVelocity = dir * speed;
        transform.forward = dir;
    }

    Transform FindTarget()
    {
        Transform bestTank = null, bestStruct = null;
        float tankDist = float.MaxValue, structDist = float.MaxValue;
        foreach (var t in TeamMember.All) // 활성 TeamMember만 (파괴된 게이트는 빠져 있다)
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

    // 빠른 포탄은 한 물리 프레임에 1m 넘게 움직여서, 게이트처럼 속이 빈 얇은 메시 콜라이더는 트리거에 안 걸리고 통과한다.
    // 그래서 지난 위치에서 지금 위치까지 구를 쓸어 보고 맞은 것이 있으면 트리거와 똑같이 처리한다
    void SweepHit()
    {
        Vector3 now = transform.position;
        Vector3 move = now - lastPos;
        float dist = move.magnitude;
        if (dist > 0.001f && !exploded)
        {
            float best = float.MaxValue;
            RaycastHit hit = default;
            Collider hitCol = null;
            foreach (var h in Physics.SphereCastAll(lastPos, sweepRadius, move / dist, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || !CanHit(h.collider)) continue;
                best = h.distance; hit = h; hitCol = h.collider;
            }
            if (hitCol != null)
            {
                Detonate(hit.distance > 0 ? hit.point : now, hitCol.GetComponentInParent<TeamMember>());
                return;
            }
        }
        lastPos = now;
    }

    bool CanHit(Collider other)
    {
        if (other.isTrigger || other.GetComponentInParent<Projectile>() != null) return false;
        var tm = other.GetComponentInParent<TeamMember>();
        return tm == null || tm.team != team; // 아군은 통과
    }

    // 트리거 충돌: Armor, Shell, Plasma
    void OnTriggerEnter(Collider other)
    {
        if (kind == ProjectileKind.Ball || exploded || !CanHit(other)) return;
        Detonate(SafeClosestPoint(other), other.GetComponentInParent<TeamMember>());
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

        if (bounceEffect != null && c.contactCount > 0)
            Destroy(Instantiate(bounceEffect, c.GetContact(0).point, Quaternion.identity), 1.0f);

        if (++bounces > maxBounces)
            Explode(transform.position);
    }

    void Detonate(Vector3 point, TeamMember hit)
    {
        if (exploded) return;
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
        if (exploded) return; // 같은 프레임에 트리거와 쓸기 검사가 둘 다 맞아도 한 번만 터진다
        exploded = true;
        if (expEffect != null)
        {
            var fx = Instantiate(expEffect, point + Vector3.up * 0.3f, Quaternion.identity); // 땅에 파묻히지 않게 살짝 띄운다
            PlayEffect(fx, expEffectScale);
            Destroy(fx, 3.0f); // 폭발 이펙트(파티클 약 2초)가 끝까지 나오게 한다
        }
        Destroy(gameObject);
    }

    // Tanks 샘플의 폭발 프리팹은 Play On Awake가 꺼져 있어서 직접 재생해야 한다.
    // scale이 1이 아니면 파티클 크기와 속도를 키운다 (Transform 크기는 파티클 크기에 영향을 주지 않는다)
    static void PlayEffect(GameObject fx, float scale)
    {
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!Mathf.Approximately(scale, 1.0f))
            {
                var m = ps.main;
                m.startSizeMultiplier *= scale;
                m.startSpeedMultiplier *= scale;
            }
            ps.Play(false);
        }
        foreach (var a in fx.GetComponentsInChildren<AudioSource>(true))
            if (a.clip != null && !a.isPlaying) a.Play();
    }
}
