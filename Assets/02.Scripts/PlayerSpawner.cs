using System.Collections;
using UnityEngine;

// 플레이어 탱크를 스폰하고, 파괴되면 일정 시간 뒤 다시 스폰한다
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform spawnPoint;
    public float respawnDelay = 5.0f;
    public int maxLives = 3;           // 목숨 수. 다 쓰면 패배한다

    public static int Lives { get; private set; }
    public static int MaxLives { get; private set; }
    WaveManager waves;

    public GameObject Current { get; private set; }

    void Start()
    {
        Lives = MaxLives = maxLives;
        waves = FindFirstObjectByType<WaveManager>();
        Spawn();
    }

    public void Spawn()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        Current = Instantiate(playerPrefab, pos, rot);

        Current.GetComponent<TeamMember>().SetTeam(TeamConfig.playerTeam);
        var health = Current.GetComponent<Health>();
        health.Refresh();
        health.onDeath += OnPlayerDeath;
    }

    void OnPlayerDeath(Health h)
    {
        Lives = Mathf.Max(0, Lives - 1);
        if (Lives <= 0)
        {
            // 목숨을 다 썼다: 부활하지 않고 패배
            GameHUD.Toast("목숨을 모두 잃었습니다");
            if (waves != null) waves.Defeat();
            return;
        }
        GameHUD.Toast($"목숨 {Lives}개 남음");
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        Spawn();
    }
}
