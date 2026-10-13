using UnityEngine;

public enum Team
{
    Red,
    Yellow
}

// 탱크의 팀 소속과 팀 색상을 관리한다. 프리팹 하나로 두 팀을 구분하기 위한 컴포넌트
public class TeamMember : MonoBehaviour
{
    public Team team = Team.Red;
    public bool isStructure = false; // 코어, 스폰 게이트 같은 구조물
    public Color redColor = new Color(0.774f, 0.133f, 0.128f, 1f);
    public Color yellowColor = new Color(0.95f, 0.78f, 0.1f, 1f);

    // 씬에 활성화된 모든 TeamMember. 매 프레임 FindObjectsByType을 쓰지 않고 이웃 탐색에 쓴다
    public static readonly System.Collections.Generic.List<TeamMember> All = new System.Collections.Generic.List<TeamMember>();

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Awake()
    {
        SetTeam(team);
    }

    // 스폰 직후 팀을 바꿀 때 호출한다
    public void SetTeam(Team newTeam)
    {
        team = newTeam;

        // 팀에 따라 체력 배율이 달라진다
        var health = GetComponent<Health>();
        if (health != null) health.Refresh();

        // 에디터 모드에서 renderer.materials를 부르면 머티리얼이 누수되므로 플레이 중에만 색을 바꾼다
        if (!Application.isPlaying) return;

        var color = team == Team.Red ? redColor : yellowColor;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // renderer.materials는 인스턴스를 만들므로 다른 탱크의 색에 영향이 없다
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                // 차체/포탑 색 머티리얼만 변경 (TankColor, TankColor 1 ...)
                if (mats[i].name.Contains("TankColor"))
                    mats[i].color = color;
            }
        }
    }

    public bool IsEnemy(TeamMember other)
    {
        return other != null && other.team != team;
    }
}
