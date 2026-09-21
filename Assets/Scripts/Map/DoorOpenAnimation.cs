using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 开门动画播放器 —— DoorController / BattleDoorController 共用（静态工具，不需要挂节点）。
///
/// 门的 Animator 平时是关着的（见 <see cref="Prepare"/>）：门上的动画控制器只有一个默认状态，
/// 只要 Animator 一启用就会自己把开门动画播下去。
/// 真正开门时调用 <see cref="PlayOpen"/>，从头播一次，播完执行 onFinished（把门的碰撞体和画面收掉）。
///
/// 门上没有 Animator、或控制器为空时，onFinished 立刻执行 —— 等价于原来的「开门即消失」，
/// 所以没做动画的门（移涌之门、心灵之门等）行为完全不变。
///
/// ⚠️ 配套前提：这 4 段开门动画的 **Loop Time 必须是关的**（`.anim` 里 m_LoopTime: 0）。
/// 如果 clip 还在循环，播到末尾会绕回首帧（= 关门图），收画面之前就会闪一下关门。
/// </summary>
public static class DoorOpenAnimation
{
    /// <summary>取不到动画长度时的兜底时长（秒）。</summary>
    private const float FallbackDuration = 0.6f;

    /// <summary>等待动画的最长时间（秒）。兜底，防止异常状态下门永远不消失。</summary>
    private const float MaxWait = 5f;

    /// <summary>
    /// 门生成时调用：关掉门上的 Animator，让门停在静止那一帧，等开门时再播。
    /// </summary>
    public static void Prepare(Animator animator)
    {
        if (animator != null)
            animator.enabled = false;
    }

    /// <summary>
    /// 开门时调用：从头播一次开门动画，播完执行 onFinished。
    /// </summary>
    /// <param name="host">用来跑协程的节点（传门自己）。</param>
    /// <param name="animator">门的 Animator，可以为 null。</param>
    /// <param name="onFinished">动画播完后的收尾动作，可以为 null。</param>
    public static void PlayOpen(MonoBehaviour host, Animator animator, Action onFinished)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            if (onFinished != null) onFinished();
            return;
        }

        animator.enabled = true;   // 重新启用：KeepAnimatorStateOnDisable=0，状态机复位
        animator.Play(0, 0, 0f);   // 0 = 控制器里的默认状态；normalizedTime 0 = 从第 0 帧开始

        if (host != null && host.isActiveAndEnabled)
            host.StartCoroutine(WaitThenFinish(animator, onFinished));
        else if (onFinished != null)
            onFinished();
    }

    private static IEnumerator WaitThenFinish(Animator animator, Action onFinished)
    {
        yield return null;   // 等一帧，让状态机真正切进默认状态

        float duration = FallbackDuration;
        if (animator != null)
        {
            float length = animator.GetCurrentAnimatorStateInfo(0).length;
            if (length > 0.01f) duration = length;
        }

        float elapsed = 0f;
        while (elapsed < duration && elapsed < MaxWait)
        {
            // 以「动画自己播到头了」为准来收尾，而不是死等 duration 秒：
            // clip 不循环时 normalizedTime 到 1 就停在末帧，这一帧收掉画面刚好。
            // 死等 duration 会因为 deltaTime 过冲多跑一帧 —— 那一帧正好是
            // （若还循环）绕回末帧之后的首帧，也就是闪出来的关门图。
            if (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (onFinished != null) onFinished();
    }
}
