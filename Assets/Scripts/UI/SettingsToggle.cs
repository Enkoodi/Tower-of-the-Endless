using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC 键开关设置场景（跨场景常驻单例）：
/// - Opening / Game 场景按下 ESC → 进入 Setting；
/// - Setting 场景按下 ESC → 返回进入前的场景；
/// - 从 Game 进入时先临时存档，返回 Game 后读取，恢复按下时的游戏状态。
/// </summary>
public class SettingsToggle : MonoBehaviour
{
    public static SettingsToggle Instance { get; private set; }

    private string previousSceneName;
    private bool isSwitching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("SettingsToggle");
            go.AddComponent<SettingsToggle>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || isSwitching) return;

        // 战斗窗口打开时禁用 ESC 切换设置
        if (BattleManager.Instance != null && BattleManager.Instance.IsFighting) return;

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "Setting")
        {
            ExitSettings();
        }
        else if (currentScene == "Opening" || currentScene == "Game")
        {
            EnterSettings(currentScene);
        }
    }

    private void EnterSettings(string fromScene)
    {
        isSwitching = true;
        previousSceneName = fromScene;

        // 从 Game 进入时，先写入自动存档
        if (fromScene == "Game" && SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveAutoGame();
        }

        // 进入设置界面使用淡入淡出，时长减半（默认 1 秒 → 0.5 秒）
        ScreenFader.FadeToScene("Setting", 0.5f);
        StartCoroutine(WaitSceneLoaded("Setting"));
    }

    /// <summary>供 Setting 场景中的 Return 按钮调用，效果等同按 ESC 返回。</summary>
    public void ExitSettingsFromButton()
    {
        if (isSwitching) return;
        ExitSettings();
    }

    private void ExitSettings()
    {
        isSwitching = true;

        string target = string.IsNullOrEmpty(previousSceneName) ? "Opening" : previousSceneName;

        if (target == "Game")
        {
            ScreenFader.FadeToScene("Game");
            StartCoroutine(ReturnToGame());
        }
        else
        {
            ScreenFader.FadeToScene(target);
            StartCoroutine(WaitSceneLoaded(target));
        }
    }

    private IEnumerator WaitSceneLoaded(string sceneName)
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
        yield return null;
        isSwitching = false;
    }

    private IEnumerator ReturnToGame()
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Game");
        yield return null;
        // 自动存档由 Game 场景的 MapGenerator.Start 读取，无需在此手动读取
        isSwitching = false;
    }
}
