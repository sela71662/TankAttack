using UnityEngine;

// Light 탱크용. 이동하는 동안 일정 주기로 뒤쪽에 지뢰를 떨어뜨린다
[RequireComponent(typeof(TeamMember))]
public class MineLayer : MonoBehaviour
{
    public GameObject minePrefab;
    public float interval = 3.0f;      // 지뢰 설치 주기(초)
    public float backOffset = 2.5f;    // 탱크 뒤쪽으로 떨어진 거리
    public float minMoveDistance = 0.5f; // 주기 사이에 이만큼 움직이지 않았으면 설치하지 않는다

    TeamMember me;
    float nextTime;
    Vector3 lastPos;

    void Awake()
    {
        me = GetComponent<TeamMember>();
        lastPos = transform.position;
    }

    void Update()
    {
        if (minePrefab == null || Time.time < nextTime) return;

        nextTime = Time.time + interval;
        if ((transform.position - lastPos).sqrMagnitude < minMoveDistance * minMoveDistance)
            return;
        lastPos = transform.position;

        var pos = transform.position - transform.forward * backOffset;
        var mine = Instantiate(minePrefab, pos, Quaternion.identity);
        mine.GetComponent<Mine>().Init(me.team);
    }
}
