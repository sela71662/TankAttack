using System.Collections;
using UnityEngine;

// 플레이어 탱크를 스폰하고, 파괴되면 일정 시간 뒤 다시 스폰한다
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform spawnPoint;
    public float respawnDelay = 5.0f;

    public GameObject Current { get; private set; }

    void Start()
    {
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
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        Spawn();
    }
}
