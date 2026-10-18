using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 인트로/엔딩 씬의 배경. 메인 씬을 함께(additive) 불러오고, 씬의 카메라가 코어 주위를 돈다.
// 불러오는 동안 GameFlow.BackgroundOnly가 켜져 있어 웨이브, 플레이어, 게임 카메라는 동작하지 않는다
public class MapBackdrop : MonoBehaviour
{
    public float orbitRadius = 110f;   // 코어에서 카메라까지 수평 거리
    public float orbitHeight = 35f;    // 코어보다 높은 정도
    public float orbitSpeed = 5f;      // 초당 도는 각도

    public bool Ready { get; private set; }     // 맵을 다 불러왔는가
    public float ReadyAt { get; private set; }  // 준비된 시각(unscaled)
    public Transform Core { get; private set; }
    public event System.Action onReady;

    float angle;
    Camera cam;

    // 이미 붙어 있으면 그대로 쓰고, 없으면 붙인다
    public static MapBackdrop Ensure(GameObject go)
    {
        var b = go.GetComponent<MapBackdrop>();
        return b != null ? b : go.AddComponent<MapBackdrop>();
    }

    void Start()
    {
        cam = Camera.main;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

        GameFlow.BackgroundOnly = true; // 메인 씬의 Awake가 이 값을 보고 게임 동작을 끈다
        StartCoroutine(Load());
    }

    IEnumerator Load()
    {
        var op = SceneManager.LoadSceneAsync(GameFlow.MainScene, LoadSceneMode.Additive);
        if (op == null) yield break;
        while (!op.isDone) yield return null;
        yield return null; // 불러온 오브젝트의 Start가 한 번 돈 뒤에 정리한다

        var map = SceneManager.GetSceneByName(GameFlow.MainScene);
        if (!map.IsValid()) yield break;
        SceneManager.SetActiveScene(map); // 하늘, 안개 같은 조명 설정은 활성 씬의 것을 쓴다

        // 메인 씬의 카메라와 오디오 리스너는 이 씬의 카메라와 겹치지 않게 끈다. 월드 체력바도 숨긴다
        foreach (var root in map.GetRootGameObjects())
        {
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            foreach (var h in root.GetComponentsInChildren<HealthBar>(true)) h.enabled = false;
        }

        var core = FindFirstObjectByType<CoreAnimator>();
        if (core == null) { Debug.LogWarning("MapBackdrop: CoreAnimator를 찾지 못했다"); yield break; }
        Core = core.transform;

        Ready = true;
        ReadyAt = Time.unscaledTime;
        onReady?.Invoke();
    }

    void LateUpdate()
    {
        if (!Ready || cam == null || Core == null) return;
        angle += orbitSpeed * Time.deltaTime;
        Vector3 c = Core.position;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 pos = c + new Vector3(Mathf.Sin(rad) * orbitRadius, orbitHeight, Mathf.Cos(rad) * orbitRadius);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(c - pos, Vector3.up);
    }
}
