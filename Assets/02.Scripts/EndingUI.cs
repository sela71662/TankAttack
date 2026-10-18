using UnityEngine;

// 메인 씬의 엔딩 트리거. 승리/패배가 정해지면 연출을 잠깐 보여 준 뒤 통계를 저장하고 엔딩 씬으로 넘어간다.
// 목숨 소진: 잠시 뒤 바로 전환. 코어 파괴: 코어가 폭발하고 슬로모션. 승리: 승리 배너(GameHUD)를 보여 준 뒤 전환.
// WaveManager.Start에서 자동으로 붙는다
public class EndingUI : MonoBehaviour
{
    public static bool Active { get; set; } // 엔딩 연출 중인가 (카메라가 클릭으로 커서를 다시 잠그지 않게 한다)

    public WaveManager waves;
    public float playerDeadDelay = 1.5f;    // 마지막 사망 후 전환까지 (실제 시간)
    public float coreBlastDelay = 2.5f;     // 코어 폭발 슬로모션 시간
    public float victoryDelay = 4.0f;       // 승리 배너를 보여 주는 시간
    public float slowTimeScale = 0.3f;

    bool triggered;
    bool leaving;
    float triggerAt;
    float delay;
    GameFlow.EndResult result;

    void Awake()
    {
        Active = false;
    }

    void Start()
    {
        if (waves == null) waves = GetComponent<WaveManager>();
        if (waves == null) return;
        waves.onDefeat += OnDefeat;
        waves.onVictory += OnVictory;
    }

    void OnDestroy()
    {
        if (waves == null) return;
        waves.onDefeat -= OnDefeat;
        waves.onVictory -= OnVictory;
    }

    void OnDefeat()
    {
        if (waves.defeatReason == WaveManager.DefeatReason.CoreDestroyed)
        {
            Trigger(GameFlow.EndResult.CoreDestroyed, coreBlastDelay);
            Time.timeScale = slowTimeScale;
            PlayCoreBlast();
        }
        else
        {
            Trigger(GameFlow.EndResult.PlayerDead, playerDeadDelay);
        }
    }

    void OnVictory()
    {
        Trigger(GameFlow.EndResult.Victory, victoryDelay);
    }

    void Trigger(GameFlow.EndResult r, float d)
    {
        triggered = true;
        triggerAt = Time.unscaledTime;
        result = r;
        delay = d;
        Active = true;
    }

    // 코어 위치에서 게이트 폭발 이펙트를 재사용한다
    void PlayCoreBlast()
    {
        if (waves.coreHealth == null) return;
        var gate = waves.gateA != null ? waves.gateA : waves.gateB;
        if (gate == null || gate.destroyEffect == null) return;
        var blast = Instantiate(gate.destroyEffect, waves.coreHealth.transform.position, Quaternion.identity);
        SpawnGate.ScaleEffect(blast, gate.blastScale);
        Destroy(blast, 6.0f);
    }

    void Update()
    {
        if (!triggered || leaving) return;
        if (Time.unscaledTime - triggerAt < delay) return;

        leaving = true;
        GameFlow.ToEnding(Capture());
    }

    GameFlow.EndStats Capture()
    {
        int gates = (waves.gateA != null && waves.gateA.IsDestroyed ? 1 : 0) + (waves.gateB != null && waves.gateB.IsDestroyed ? 1 : 0);
        return new GameFlow.EndStats
        {
            result = result,
            wave = waves.currentWave,
            totalWaves = waves.waves.Count,
            kills = AllyUpgrades.TotalKills,
            gold = GoldWallet.Gold,
            gates = gates,
        };
    }

    // 전환 직전에 화면이 어두워져 엔딩 씬의 검은 시작 화면과 이어진다
    void OnGUI()
    {
        if (!triggered) return;
        GUI.depth = -100; // HUD 위에 그린다

        float t = Time.unscaledTime - triggerAt;
        float fade = result == GameFlow.EndResult.Victory
            ? Mathf.Clamp01((t - (delay - 1.0f)) / 1.0f)   // 승리: 배너를 본 뒤 마지막 1초에만 어두워진다
            : Mathf.Clamp01(t / delay);
        Color tint = result == GameFlow.EndResult.CoreDestroyed ? new Color(0.30f, 0.02f, 0.02f) : Color.black;
        UiKit.Round(new Rect(0, 0, Screen.width, Screen.height), new Color(tint.r, tint.g, tint.b, fade), 0);
    }
}
