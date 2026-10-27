using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开场过场（挂在 TransitionCanvas 上）。
/// 黑屏里依次横向揭开 TOWER → OF THE → END，停顿后震屏瞬间补上 LESS，
/// 随后分割线从中间长出去、黑底自标题中心开一个圆洞并整体淡掉，两者同时收尾。
///
/// 标题：拿到过埃翁钥匙 或 神圣火花 才显示（都是 0 时整块隐藏）。
/// 过场：一辈子只播两次 —— 首次通关故事（从 Credits 回来、钥匙 1、还没火花）一次，
///       首次拿到神圣火花（从 Ending 回来）一次；正常启动进游戏不播，直接完整菜单。
/// </summary>
public class TransitionController : MonoBehaviour
{
    /// <summary>这次进 Opening 是从哪个场景回来的。静态常驻，重启后自然是 None。</summary>
    public static IntroSource PendingSource { get; set; }

    public enum IntroSource
    {
        None,
        Credits,
        Ending
    }

    /// <summary>锁定 60 帧，避免无锁帧时揭字节奏抖动。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LockFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    [Header("References")]

    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>标题根节点：七块分块都挂在它下面，震屏抖的也是它。</summary>
    [SerializeField] private RectTransform titleRoot;

    [SerializeField] private RectTransform towerMask;
    [SerializeField] private RectTransform ofTheMask;
    [SerializeField] private RectTransform lineLeftMask;
    [SerializeField] private RectTransform lineRightMask;
    [SerializeField] private RectTransform endMask;
    [SerializeField] private RectTransform lessMask;

    /// <summary>底部整条分割线：中心固定，向两边长。</summary>
    [SerializeField] private RectTransform lineBottomMask;

    /// <summary>全屏黑底。材质按 revealShader 在运行时生成，圆心/半径/软边/整体透明度都靠它算。</summary>
    [SerializeField] private Image holeCover;
    [SerializeField] private Shader revealShader;

    [Header("Title Reveal")]

    /// <summary>开场黑屏静置。要盖过 ScreenFader 进场景时的 1s 停留 + 1s 淡入。</summary>
    [SerializeField] private float initialHold = 2f;
    [SerializeField] private float towerDuration = 0.9f;
    [SerializeField] private float gapAfterTower = 0.25f;
    [SerializeField] private float ofTheDuration = 0.55f;
    [SerializeField] private float gapAfterOfThe = 0.25f;
    [SerializeField] private float endDuration = 0.7f;
    [SerializeField] private float gapBeforeLess = 0.45f;
    [SerializeField] private float gapAfterLess = 0.6f;

    [Header("Shake")]

    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeMagnitude = 3f;

    [Header("Background Reveal")]

    /// <summary>分割线与圆洞共用的时长 —— 两者必须同时收尾。</summary>
    [SerializeField] private float revealDuration = 1.2f;

    /// <summary>渐变带宽度（单侧），占屏幕短边的比例。整条 1→0 的跨度是它的两倍。</summary>
    [SerializeField, Range(0.005f, 0.6f)] private float holeSoftness = 0.06f;

    /// <summary>半径变大时渐变带跟着变宽的比例。不能为负。</summary>
    [SerializeField, Range(0f, 0.6f)] private float holeSoftnessGrow = 0.08f;

    /// <summary>整体退黑的起点，占 revealDuration 的时间比例（0.55 = 走到 55% 时长才开始淡）。</summary>
    [SerializeField, Range(0f, 1f)] private float holeFadeStart = 0.55f;

    private const int Tower = 0;
    private const int OfThe = 1;
    private const int LineLeft = 2;
    private const int LineRight = 3;
    private const int End = 4;
    private const int Less = 5;
    private const int LineBottom = 6;

    private RectTransform[] masks;
    private float[] maskFullWidths;

    private Material holeMaterial;
    private Vector4 holeCenterUv;
    private Vector2 holeSizePx;
    private float holeMaxRadius;
    private float holeBaseSoftness;

    private Vector3 titleHomePosition;
    private bool titleVisible;

    private void Awake()
    {
        masks = new[]
        {
            towerMask, ofTheMask, lineLeftMask, lineRightMask,
            endMask, lessMask, lineBottomMask
        };
        maskFullWidths = new float[masks.Length];
        for (int i = 0; i < masks.Length; i++)
        {
            maskFullWidths[i] = masks[i].sizeDelta.x;
        }

        // 材质必须在运行时实例化：直接改共享材质会把数值写进工程资产
        if (holeCover != null && revealShader != null)
        {
            holeMaterial = new Material(revealShader);
            holeCover.material = holeMaterial;
        }

        titleHomePosition = titleRoot.localPosition;

        GlobalSaveData global = SaveManager.LoadGlobalData();
        int aeonKeys = global != null ? global.aeonKeys : 0;
        int divineSpark = global != null ? global.divineSpark : 0;
        int stage = global != null ? global.openingIntroStage : 0;

        IntroSource source = PendingSource;
        PendingSource = IntroSource.None;

        // 标题算进度奖励：两样都没有就整块不显示
        titleVisible = aeonKeys > 0 || divineSpark > 0;
        titleRoot.gameObject.SetActive(titleVisible);

        // 一辈子只播两次：故事首次通关一次、首次拿到神圣火花一次
        bool playIntro =
            (source == IntroSource.Credits && stage == 0 && aeonKeys == 1 && divineSpark == 0) ||
            (source == IntroSource.Ending && stage < 2 && divineSpark >= 1);

        if (!playIntro)
        {
            SetFinalState();
            return;
        }

        if (global != null)
        {
            global.openingIntroStage = source == IntroSource.Credits ? 1 : 2;
            SaveManager.SaveGlobalData(global);
        }

        SetInitialState();
        StartCoroutine(PlayRoutine());
    }

    /// <summary>外部入口：从头重播一遍。运行中在 Inspector 上右键可用来调参。</summary>
    [ContextMenu("重播过场")]
    public void PlayTransition()
    {
        StopAllCoroutines();
        titleRoot.localPosition = titleHomePosition;
        SetInitialState();
        StartCoroutine(PlayRoutine());
    }

    /// <summary>完整状态：分块全宽、黑底关掉、不拦点击。</summary>
    private void SetFinalState()
    {
        for (int i = 0; i < masks.Length; i++)
        {
            SetMaskWidth(i, maskFullWidths[i]);
        }

        SetHoleActive(false);

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>过场初始：分块全收，黑底盖满整屏。</summary>
    private void SetInitialState()
    {
        for (int i = 0; i < masks.Length; i++)
        {
            SetMaskWidth(i, 0f);
        }

        SetHoleActive(true);
        CoverAll();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
    }

    private IEnumerator PlayRoutine()
    {
        yield return new WaitForSeconds(initialHold);

        // 没标题时这几拍全是空转，直接跳到揭背景
        if (titleVisible)
        {
            yield return RevealMask(Tower, towerDuration);
            yield return new WaitForSeconds(gapAfterTower);

            yield return RevealMask(OfThe, ofTheDuration);
            yield return new WaitForSeconds(gapAfterOfThe);

            yield return RevealMask(End, endDuration);
            yield return new WaitForSeconds(gapBeforeLess);

            // 震屏与 LESS 同一帧：字先到，屏幕才开始抖
            SetMaskWidth(Less, maskFullWidths[Less]);
            yield return ShakeTitle();
            yield return new WaitForSeconds(gapAfterLess);
        }

        yield return RevealLinesAndBackground();

        SetHoleActive(false);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = true;
    }

    private IEnumerator RevealMask(int index, float duration)
    {
        float width = maskFullWidths[index];
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            SetMaskWidth(index, width * Mathf.Clamp01(time / duration));
            yield return null;
        }

        SetMaskWidth(index, width);
    }

    /// <summary>分割线从中间往两边长，黑底同时从标题中心开圆洞，共用一个进度条。</summary>
    private IEnumerator RevealLinesAndBackground()
    {
        MeasureHole();

        float time = 0f;
        while (time < revealDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / revealDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            SetMaskWidth(LineLeft, maskFullWidths[LineLeft] * eased);
            SetMaskWidth(LineRight, maskFullWidths[LineRight] * eased);
            SetMaskWidth(LineBottom, maskFullWidths[LineBottom] * eased);

            // 圆洞吃线性进度：缓动在 ApplyHole 里单独做，否则两层缓动叠起来会提前开完
            ApplyHole(t);

            yield return null;
        }

        SetMaskWidth(LineLeft, maskFullWidths[LineLeft]);
        SetMaskWidth(LineRight, maskFullWidths[LineRight]);
        SetMaskWidth(LineBottom, maskFullWidths[LineBottom]);
        ApplyHole(1f);
    }

    /// <summary>衰减的正弦抖动，只抖标题根节点；此时画面里只有标题，等同震屏。</summary>
    private IEnumerator ShakeTitle()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            float remain = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            float y = Mathf.Sin(elapsed * 88f) * shakeMagnitude * remain;
            float x = Mathf.Cos(elapsed * 71f) * shakeMagnitude * 0.45f * remain;

            titleRoot.localPosition = titleHomePosition + new Vector3(x, y, 0f);
            yield return null;
        }

        titleRoot.localPosition = titleHomePosition;
    }

    private void SetMaskWidth(int index, float width)
    {
        RectTransform mask = masks[index];
        Vector2 size = mask.sizeDelta;
        size.x = Mathf.Max(0f, width);
        mask.sizeDelta = size;
    }

    /// <summary>量出标题中心在黑底上的 UV，以及圆洞要长到多大才盖住四角。</summary>
    private void MeasureHole()
    {
        if (holeMaterial == null)
        {
            return;
        }

        RectTransform cover = (RectTransform)holeCover.transform;
        Rect rect = cover.rect;
        holeSizePx = new Vector2(rect.width, rect.height);

        Vector3 local = cover.InverseTransformPoint(titleRoot.TransformPoint(titleRoot.rect.center));
        holeCenterUv = new Vector4(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height,
            0f, 0f);

        float far = 0f;
        far = Mathf.Max(far, Vector2.Distance(local, new Vector2(rect.xMin, rect.yMin)));
        far = Mathf.Max(far, Vector2.Distance(local, new Vector2(rect.xMax, rect.yMin)));
        far = Mathf.Max(far, Vector2.Distance(local, new Vector2(rect.xMin, rect.yMax)));
        far = Mathf.Max(far, Vector2.Distance(local, new Vector2(rect.xMax, rect.yMax)));

        holeMaxRadius = far * 1.05f;
        holeBaseSoftness = Mathf.Min(rect.width, rect.height) * Mathf.Max(0.0001f, holeSoftness);

        // 收尾时渐变带的内沿也要越出最远的角，否则四角会留一层灰
        float denom = Mathf.Max(0.25f, 1f - Mathf.Max(0f, holeSoftnessGrow));
        holeMaxRadius = Mathf.Max(holeMaxRadius, (far + holeBaseSoftness) / denom);
    }

    /// <summary>
    /// t = 0 全黑，t = 1 黑底完全消失，t 就是时间进度（0~1），不再叠第二层缓动。
    /// </summary>
    private void ApplyHole(float t)
    {
        if (holeMaterial == null)
        {
            return;
        }

        // 半径走 S 曲线：起步慢、中段匀、收尾再缓一点，但整段铺满 revealDuration
        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        float radius = Mathf.Lerp(-holeBaseSoftness, holeMaxRadius, grow);
        float softness = holeBaseSoftness + Mathf.Max(0f, radius) * holeSoftnessGrow;

        // 后段整体淡掉，角落不至于被圆形硬切
        float fadeStart = Mathf.Clamp01(holeFadeStart);
        float fade = 1f - Mathf.Clamp01((t - fadeStart) / Mathf.Max(0.01f, 1f - fadeStart));

        holeMaterial.SetVector("_Center", holeCenterUv);
        holeMaterial.SetVector("_Size", new Vector4(holeSizePx.x, holeSizePx.y, 0f, 0f));
        holeMaterial.SetFloat("_Radius", radius);
        holeMaterial.SetFloat("_Softness", softness);
        holeMaterial.SetFloat("_Fade", fade);
    }

    /// <summary>还没量过尺寸时的兜底：无论屏幕多大都是全黑。</summary>
    private void CoverAll()
    {
        if (holeMaterial == null)
        {
            return;
        }

        holeMaterial.SetVector("_Center", new Vector4(0.5f, 0.5f, 0f, 0f));
        holeMaterial.SetVector("_Size", Vector4.one);
        holeMaterial.SetFloat("_Radius", -1f);
        holeMaterial.SetFloat("_Softness", 0.5f);
        holeMaterial.SetFloat("_Fade", 1f);
    }

    private void SetHoleActive(bool active)
    {
        if (holeCover != null)
        {
            holeCover.gameObject.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (holeMaterial != null)
        {
            Destroy(holeMaterial);
        }
    }
}
