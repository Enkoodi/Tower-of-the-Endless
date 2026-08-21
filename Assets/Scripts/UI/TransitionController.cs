using System.Collections;
using UnityEngine;

/// <summary>
/// 开场过场动画控制器（挂载在 TransitionCanvas 上）
///
/// 完整动画流程：
/// 1. 黑屏（CanvasGroup.alpha = 1，画面完全不透明），标题初始宽度为 0，先停顿 initialHoldDuration 秒
/// 2. 揭开标题（englishReveal 的 width 从 0 展开到 endWidth = 570）
/// 3. 到达 570 后停顿片刻
/// 4. 瞬间显示全部标题（width = fullWidth = 700）
/// 5. 标题产生一次强烈的冲击 + 回弹 + 二次震动
/// 6. 黑屏整体淡出（alpha 1 → 0），露出背后的菜单/游戏画面
///
/// 注意：标题显示期间始终保持 alpha = 1，不改变 TransitionCanvas 的透明度，
/// 避免黑屏中途穿帮。
/// </summary>
public class TransitionController : MonoBehaviour
{
    [Header("References")]

    /// <summary>
    /// 控制整个过渡画面透明度的 CanvasGroup。
    /// 标题显示期间保持 alpha = 1。
    /// </summary>
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>
    /// 标题条（带 RectMask2D 的 RectTransform）。
    /// 通过修改 sizeDelta.x 实现从左向右揭开标题。
    /// </summary>
    [SerializeField] private RectTransform englishReveal;


    [Header("Title Settings")]

    /// <summary>
    /// 标题展开后的完整宽度。
    /// </summary>
    [SerializeField] private float fullWidth = 700f;

    /// <summary>
    /// 标题先展开到的中间宽度。
    /// </summary>
    [SerializeField] private float endWidth = 570f;


    [Header("Animation Settings")]

    /// <summary>
    /// 标题开始显示前，黑屏的初始等待时长。
    /// </summary>
    [SerializeField] private float initialHoldDuration = 1f;

    /// <summary>
    /// 标题从左往右展开到 endWidth 的时长。
    /// </summary>
    [SerializeField] private float titleRevealDuration = 2f;

    /// <summary>
    /// 标题停在 endWidth 时的停顿时长。
    /// </summary>
    [SerializeField] private float endPauseDuration = 1f;


    [Header("Impact / Screen Shake")]

    /// <summary>
    /// 整个冲击动画的总时长。
    /// </summary>
    [SerializeField] private float shakeDuration = 0.1f;

    /// <summary>
    /// 第一次冲击向下移动的最大距离。
    /// 建议 15 ~ 25。
    /// </summary>
    [SerializeField] private float shakeMagnitude = 6f;

    /// <summary>
    /// 第二次小幅震动的距离。
    /// 建议 3 ~ 6。
    /// </summary>
    [SerializeField] private float secondaryShakeMagnitude = 3f;

    /// <summary>
    /// 标题冲击时的最大缩放倍率。
    /// 1.03 表示放大 3%。
    /// </summary>
    [SerializeField] private float impactScale = 1f;

    /// <summary>
    /// 第二次震动开始的时间比例。
    /// 0.55 表示第一段冲击完成约 55% 后开始。
    /// </summary>
    [SerializeField, Range(0f, 1f)]
    private float secondaryShakeStart = 0f;


    [Header("Fade Out Settings")]

    /// <summary>
    /// 动画结束后黑屏淡出时长。
    /// </summary>
    [SerializeField] private float fadeDuration = 4f;

    /// <summary>
    /// 淡出完成后是否自动停用本物体。
    /// </summary>
    [SerializeField] private bool deactivateOnComplete = false;


    /// <summary>
    /// 对象激活时初始化并自动播放过渡动画。
    /// </summary>
    private void Awake()
    {
        // 初始化：黑屏
        canvasGroup.alpha = 1f;

        // 标题隐藏
        englishReveal.sizeDelta =
            new Vector2(0f, englishReveal.sizeDelta.y);

        // 自动播放
        StartCoroutine(TransitionRoutine());
    }


    /// <summary>
    /// 外部调用入口：重新播放过渡动画。
    /// </summary>
    public void PlayTransition()
    {
        // 停止之前的动画
        StopAllCoroutines();

        // 恢复黑屏
        canvasGroup.alpha = 1f;

        // 恢复标题宽度
        englishReveal.sizeDelta =
            new Vector2(0f, englishReveal.sizeDelta.y);

        // 恢复射线
        canvasGroup.blocksRaycasts = true;

        // 播放
        StartCoroutine(TransitionRoutine());
    }


    /// <summary>
    /// 过渡动画主流程：
    ///
    /// 黑屏
    /// ↓
    /// 标题展开到 570
    /// ↓
    /// 停顿
    /// ↓
    /// 瞬间展开到 700
    /// ↓
    /// 强烈冲击 + 回弹 + 二次震动 + 缩放
    /// ↓
    /// 黑屏淡出
    /// </summary>
    private IEnumerator TransitionRoutine()
    {
        // 保持黑屏
        canvasGroup.alpha = 1f;

        // 标题隐藏
        englishReveal.sizeDelta =
            new Vector2(0f, englishReveal.sizeDelta.y);


        // =========================
        // 0. 初始黑屏等待
        // =========================

        yield return new WaitForSeconds(initialHoldDuration);


        // =========================
        // 1. 标题展开
        // =========================

        yield return StartCoroutine(RevealTitle());


        // =========================
        // 2. 停顿
        // =========================

        yield return new WaitForSeconds(endPauseDuration);


        // =========================
        // 3. 瞬间显示完整标题
        // =========================

        englishReveal.sizeDelta =
            new Vector2(fullWidth, englishReveal.sizeDelta.y);


        // =========================
        // 4. 震撼冲击
        // =========================

        yield return StartCoroutine(ShakeOnce());


        // =========================
        // 5. 黑屏淡出
        // =========================

        yield return StartCoroutine(FadeOutCanvas());


        // =========================
        // 6. 完成
        // =========================

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }
    }


    /// <summary>
    /// 标题展开动画：
    /// 宽度从 0 线性展开到 endWidth。
    /// </summary>
    private IEnumerator RevealTitle()
    {
        float time = 0f;

        while (time < titleRevealDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(time / titleRevealDuration);

            float width =
                Mathf.Lerp(0f, endWidth, t);

            englishReveal.sizeDelta =
                new Vector2(width, englishReveal.sizeDelta.y);

            yield return null;
        }


        // 确保最终精确到 endWidth
        englishReveal.sizeDelta =
            new Vector2(endWidth, englishReveal.sizeDelta.y);
    }


    /// <summary>
    /// 标题震撼出现效果。
    ///
    /// 效果流程：
    ///
    /// ① 标题已经瞬间展开到 fullWidth
    ///
    /// ② 标题瞬间向下冲
    ///
    /// ③ 快速回弹并超过原始位置
    ///
    /// ④ 回到原位
    ///
    /// ⑤ 中间加入一次小幅二次震动
    ///
    /// ⑥ 同时产生轻微 Scale 冲击
    ///
    /// 最终完全恢复到原来的位置和缩放。
    /// </summary>
    private IEnumerator ShakeOnce()
    {
        // =========================
        // 保存初始状态
        // =========================

        Vector3 originalPosition =
            englishReveal.localPosition;

        Vector3 originalScale =
            englishReveal.localScale;


        // =========================
        // 第一阶段：强烈下砸
        // =========================

        Vector3 impactPosition =
            originalPosition +
            new Vector3(0f, -shakeMagnitude, 0f);


        // =========================
        // 第二阶段：超过原位的回弹
        // =========================

        Vector3 reboundPosition =
            originalPosition +
            new Vector3(
                0f,
                shakeMagnitude * 0.35f,
                0f
            );


        // =========================
        // 直接瞬间移动到冲击位置
        // =========================

        englishReveal.localPosition =
            impactPosition;


        // 同时放大一点
        englishReveal.localScale =
            originalScale * impactScale;


        // =========================
        // 第一段：冲击 → 回弹
        // =========================

        float elapsed = 0f;

        float impactDuration =
            shakeDuration * 0.55f;


        while (elapsed < impactDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(elapsed / impactDuration);


            // EaseOut
            // 开始非常快，之后逐渐减速
            float easedT =
                1f - Mathf.Pow(1f - t, 3f);


            // 位置：冲击点 → 回弹点
            englishReveal.localPosition =
                Vector3.Lerp(
                    impactPosition,
                    reboundPosition,
                    easedT
                );


            // Scale：
            // 1.03 → 1.0
            float scaleT =
                Mathf.SmoothStep(0f, 1f, t);

            englishReveal.localScale =
                Vector3.Lerp(
                    originalScale * impactScale,
                    originalScale,
                    scaleT
                );


            yield return null;
        }


        // =========================
        // 第二段：回弹 → 原位
        // =========================

        elapsed = 0f;

        float reboundDuration =
            shakeDuration * 0.25f;


        while (elapsed < reboundDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(elapsed / reboundDuration);


            float easedT =
                1f - Mathf.Pow(1f - t, 3f);


            englishReveal.localPosition =
                Vector3.Lerp(
                    reboundPosition,
                    originalPosition,
                    easedT
                );


            yield return null;
        }


        // =========================
        // 第三段：轻微二次震动
        // =========================

        elapsed = 0f;

        float secondaryDuration =
            shakeDuration * 0.20f;


        Vector3 secondaryDown =
            originalPosition +
            new Vector3(
                0f,
                -secondaryShakeMagnitude,
                0f
            );


        Vector3 secondaryUp =
            originalPosition +
            new Vector3(
                0f,
                secondaryShakeMagnitude * 0.35f,
                0f
            );


        while (elapsed < secondaryDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / secondaryDuration
                );


            // 使用正弦曲线制造小幅震动
            float shakeT =
                Mathf.Sin(t * Mathf.PI * 2f);


            float amount =
                Mathf.Abs(shakeT);


            if (shakeT > 0f)
            {
                englishReveal.localPosition =
                    Vector3.Lerp(
                        originalPosition,
                        secondaryUp,
                        amount
                    );
            }
            else
            {
                englishReveal.localPosition =
                    Vector3.Lerp(
                        originalPosition,
                        secondaryDown,
                        amount
                    );
            }


            yield return null;
        }


        // =========================
        // 最终完全恢复
        // =========================

        englishReveal.localPosition =
            originalPosition;

        englishReveal.localScale =
            originalScale;
    }


    /// <summary>
    /// 黑屏淡出：
    /// CanvasGroup alpha 从 1 → 0。
    ///
    /// 使用三次函数：
    /// t³
    ///
    /// 效果：
    /// 开始非常慢
    /// ↓
    /// 中间逐渐加速
    /// ↓
    /// 最后快速淡出
    /// </summary>
    private IEnumerator FadeOutCanvas()
    {
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            // 归一化进度：0 → 1
            float t =
                Mathf.Clamp01(time / fadeDuration);

            // 三次函数
            // 0 → 0.001 → 0.125 → 1
            //
            // 前期变化很慢
            // 后期变化越来越快
            float easedT = t * t * t;

            // Alpha：1 → 0
            canvasGroup.alpha =
                Mathf.Lerp(1f, 0f, easedT);

            yield return null;
        }

        // 确保最终完全透明
        canvasGroup.alpha = 0f;

        // 不再拦截点击
        canvasGroup.blocksRaycasts = false;
    }
}