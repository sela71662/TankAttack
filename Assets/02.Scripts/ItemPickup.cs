using UnityEngine;

public enum ItemType
{
    Damage,   // 공격력 증가
    FireRate, // 연사 속도 증가
    Heal,     // 체력 회복
    Speed     // 이동 속도 증가
}

// 적을 처치하면 확률로 떨어지는 아이템. 플레이어 탱크가 가까이 가면 먹는다
public class ItemPickup : MonoBehaviour
{
    public ItemType type;
    public float lifetime = 20.0f;      // 이 시간이 지나면 사라진다
    public float pickupRadius = 3.5f;

    float baseY;

    static readonly Color[] colors =
    {
        new Color(1.0f, 0.25f, 0.2f),  // Damage: 빨강
        new Color(1.0f, 0.85f, 0.1f),  // FireRate: 노랑
        new Color(0.2f, 1.0f, 0.35f),  // Heal: 초록
        new Color(0.2f, 0.75f, 1.0f)   // Speed: 하늘
    };

    public static Color ColorOf(ItemType t) { return colors[(int)t]; }

    // chance 확률로 pos 근처에 무작위 아이템을 떨어뜨린다
    public static void TryDrop(Vector3 pos, float chance)
    {
        if (Random.value > chance) return;
        Create(pos, (ItemType)Random.Range(0, 4));
    }

    public static ItemPickup Create(Vector3 pos, ItemType type)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Item_" + type;
        Destroy(go.GetComponent<Collider>()); // 획득은 거리로 판정한다
        go.transform.localScale = Vector3.one * 1.4f;
        go.transform.position = pos + Vector3.up * 1.5f;

        var mat = new Material(Shader.Find("Standard"));
        mat.color = colors[(int)type];
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colors[(int)type] * 0.8f);
        go.GetComponent<Renderer>().material = mat;

        var item = go.AddComponent<ItemPickup>();
        item.type = type;
        return item;
    }

    void Start()
    {
        baseY = transform.position.y;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3.0f) * 0.3f;
        transform.position = p;
        transform.Rotate(Vector3.up, 120.0f * Time.deltaTime, Space.World);

        var player = PlayerTankController.Instance;
        if (player == null) return;

        Vector3 d = player.transform.position - transform.position;
        d.y = 0;
        if (d.magnitude > pickupRadius) return;

        var buffs = player.GetComponent<PlayerBuffs>();
        if (buffs == null) buffs = player.gameObject.AddComponent<PlayerBuffs>();
        buffs.Apply(type);
        Destroy(gameObject);
    }
}
