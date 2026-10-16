using UnityEngine;

// 오브젝트 위에 떠서 카메라를 향하는 체력바. Health가 있는 곳에 붙인다
[RequireComponent(typeof(Health))]
public class HealthBar : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 5.0f, 0); // 대상 위치에서 떨어진 거리
    public float width = 10.0f;
    public float height = 0.9f;
    public bool lockPosition = false; // true면 시작 위치에 고정 (코어처럼 위아래로 흔들리는 대상용)
    public bool scaleWithObject = false; // true면 대상 크기에 맞춰 바 크기와 높이도 키운다 (거대화한 탱크용)
    public bool showText = true;
    public bool textInside = false; // true면 글자를 막대 안쪽 가운데에 넣는다 (막대 높이에 맞춰 글자 크기를 정한다)
    public Color fullColor = new Color(0.2f, 0.9f, 0.3f);
    public Color emptyColor = new Color(0.95f, 0.15f, 0.1f);

    Health health;
    Transform root;
    Transform fill;
    TextMesh text;
    Vector3 lockedPos;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    void Start()
    {
        lockedPos = transform.position + offset;
        Build();
    }

    // 대상이 회전하거나 크기가 변해도 영향받지 않도록 별도 루트로 만든다
    void Build()
    {
        root = new GameObject(name + "_HealthBar").transform;

        var bg = MakeQuad("Back", new Color(0, 0, 0, 0.7f), 0);
        bg.localScale = new Vector3(width + 0.3f, height + 0.3f, 1);

        fill = MakeQuad("Fill", fullColor, 1);

        if (showText)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(root, false);
            text = go.AddComponent<TextMesh>();
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            if (textInside)
            {
                go.transform.localPosition = new Vector3(0, 0, -0.02f);
                text.anchor = TextAnchor.MiddleCenter;
                text.characterSize = height * 0.8f / 4.8f; // 글자 높이가 막대 높이 안에 들어오게 한다
            }
            else
            {
                go.transform.localPosition = new Vector3(0, height * 1.1f, 0);
                text.anchor = TextAnchor.LowerCenter;
                text.characterSize = 0.35f;
            }
            text.color = Color.white;
            go.GetComponent<MeshRenderer>().sortingOrder = 2;
        }
    }

    Transform MakeQuad(string quadName, Color color, int order)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = quadName;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        var r = q.GetComponent<MeshRenderer>();
        r.material = new Material(Shader.Find("Sprites/Default")) { color = color };
        r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q.transform;
    }

    // 컴포넌트를 꺼도 별도 루트의 막대는 남으므로 같이 숨긴다 (파괴된 게이트의 막대가 남던 문제)
    void OnEnable()
    {
        if (root != null) root.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    void LateUpdate()
    {
        if (root == null || health == null) return;

        float k = scaleWithObject ? transform.lossyScale.y : 1.0f;
        root.localScale = Vector3.one * k;
        root.position = lockPosition ? lockedPos : transform.position + offset * k;

        var cam = Camera.main;
        if (cam != null)
            root.rotation = cam.transform.rotation;

        float ratio = health.maxHp > 0 ? Mathf.Clamp01(health.hp / health.maxHp) : 0;

        // 왼쪽을 기준으로 줄어들도록 크기와 위치를 함께 조정한다
        fill.localScale = new Vector3(Mathf.Max(width * ratio, 0.0001f), height, 1);
        fill.localPosition = new Vector3(-(width - width * ratio) * 0.5f, 0, -0.01f);
        fill.GetComponent<MeshRenderer>().material.color = Color.Lerp(emptyColor, fullColor, ratio);

        if (text != null)
            text.text = $"{Mathf.CeilToInt(health.hp)} / {Mathf.CeilToInt(health.maxHp)}";
    }
}
