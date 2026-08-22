using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全屏淡入淡出过场控制器（单例，跨场景常驻）。
/// 首次调用 FadeToScene 时自动创建自身与全屏遮罩，无需在场景中手动放置。
/// 所有场景跳转统一走 ScreenFader.FadeToScene("场景名")：
/// 先淡出（画面变黑）→ 切换场景 → 再淡入（画面恢复）。
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Tooltip("淡出/淡入的时长（秒，使用未缩放时间，不受 Time.timeScale 影响）")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("切换场景后停留在黑屏的时长（秒），给新场景初始化留出时间")]
    [SerializeField] private float blackHoldDuration = 1f;

    [Tooltip("遮罩颜色")]
    [SerializeField] private Color fadeColor = Color.black;

    /// <summary>全局单例</summary>
    public static ScreenFader Instance { get; private set; }

    private Image overlay;
    private Coroutine fadeRoutine;

    // ======================== 静态入口 ========================

    /// <summary>带淡入淡出地跳转场景：先淡出，加载场景，再淡入。</summary>
    public static void FadeToScene(string sceneName)
    {
        GetOrCreate().FadeTo(sceneName);
    }

    /// <summary>带淡入淡出地跳转场景，可自定义淡入淡出时长（秒，传 0 使用默认值）。</summary>
    public static void FadeToScene(string sceneName, float duration)
    {
        GetOrCreate().FadeTo(sceneName, duration);
    }

    private static ScreenFader GetOrCreate()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("ScreenFader");
            go.AddComponent<ScreenFader>();
        }
        return Instance;
    }

    // ======================== 实例逻辑 ========================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
    }

    /// <summary>创建屏幕空间覆盖的全屏遮罩（始终置顶，不拦截点击）。</summary>
    private void CreateOverlay()
    {
        // 挂到自身下面，随 DontDestroyOnLoad 一起跨场景常驻，
        // 否则切场景时遮罩 Canvas 会被当作普通场景对象销毁。
        GameObject canvasGo = new GameObject("FadeCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        GameObject rectGo = new GameObject("FadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rectGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = rectGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlay = rectGo.GetComponent<Image>();
        overlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        overlay.raycastTarget = false;
    }

    public void FadeTo(string sceneName)
    {
        FadeTo(sceneName, 0f);
    }

    /// <summary>淡入淡出跳转场景；duration 传 0 表示使用 fadeDuration。</summary>
    public void FadeTo(string sceneName, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeToRoutine(sceneName, duration));
    }

    private IEnumerator FadeToRoutine(string sceneName, float duration)
    {
        float fadeDur = duration > 0f ? duration : fadeDuration;
        yield return FadeRoutine(1f, fadeDur); // 淡出至全黑

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"[ScreenFader] 未在 Build Settings 中找到场景 '{sceneName}'，请添加后重试");
        }

        yield return null;            // 等待新场景完成加载
        yield return new WaitForSecondsRealtime(blackHoldDuration); // 黑屏停顿，等新场景初始化完毕
        yield return FadeRoutine(0f, fadeDur); // 淡入恢复画面
        fadeRoutine = null;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float fadeDur)
    {
        float from = overlay.color.a;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / fadeDur;
            SetAlpha(Mathf.Lerp(from, targetAlpha, Mathf.Clamp01(t)));
            yield return null;
        }
        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        Color c = overlay.color;
        c.a = alpha;
        overlay.color = c;
    }
}
