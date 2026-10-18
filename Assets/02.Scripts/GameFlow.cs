using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환. 인트로 -> 메인 씬, 메인 씬 -> 엔딩 씬, 엔딩에서 재시작(인트로 제외) 또는 메인 메뉴로 돌아가기
public static class GameFlow
{
    public const string IntroScene = "Intro";
    public const string MainScene = "Main";
    public const string EndingScene = "Ending";

    public enum EndResult { Victory, PlayerDead, CoreDestroyed }

    // 엔딩 씬에 넘기는 결과와 통계
    public struct EndStats
    {
        public EndResult result;
        public int wave, totalWaves, kills, gold, gates;
    }

    public static EndStats LastEnd { get; private set; }

    // 인트로/엔딩 배경으로 메인 씬을 함께 불러온 상태. 켜져 있으면 웨이브, 플레이어, 카메라 조작, 지도 키가 동작하지 않는다
    public static bool BackgroundOnly { get; set; }
    public static bool InMenuScene { get; set; } // 인트로/엔딩 씬이 열려 있다 (M키 지도 등을 막는다)

    // 인트로를 건너뛰고 메인 씬만 처음부터 다시 시작한다
    public static void Restart()
    {
        CleanUp();
        SceneManager.LoadScene(MainScene);
    }

    public static void ToIntro()
    {
        CleanUp();
        SceneManager.LoadScene(IntroScene);
    }

    // 결과와 통계를 저장하고 엔딩 씬으로 넘어간다
    public static void ToEnding(EndStats stats)
    {
        LastEnd = stats;
        CleanUp();
        SceneManager.LoadScene(EndingScene);
    }

    // 씬을 불러오기 전에 정적 상태와 시간 배율을 되돌린다
    // (골드, 레벨, 목숨은 새 씬의 Awake/Start에서 다시 초기화된다)
    static void CleanUp()
    {
        Time.timeScale = 1f;
        BackgroundOnly = false;
        InMenuScene = false;
        EndingUI.Active = false;
        MapView.IsOpen = false;
        SpawnGate.ClearEffects();
        AllyMover.ResetStatics();
    }
}
