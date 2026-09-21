using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 UI — 挂载在 BattleCanvas/BattlePanel/BattleWindow 上。
/// 按层级图绑定：敌人名、玩家/敌人图片与属性、战斗日志。
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("窗口根节点")]
    [SerializeField] private GameObject battleWindow;

    [Header("顶部敌人名称")]
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI turnText;

    [Header("左侧玩家")]
    [SerializeField] private Image playerImage;
    [SerializeField] private TextMeshProUGUI playerStatsText;

    private Animator playerAvatarAnimator;   // 玩家头像动画（与 playerImage 同对象，懒解析）

    // PlayerUI.controller 的动画参数（均为 Trigger 型，触发一次即被消费）与待机状态名
    private static readonly int IsAttackHash  = Animator.StringToHash("isAttack");
    private static readonly int IsHurtHash    = Animator.StringToHash("isHurt");
    private const string PlayerIdleStateName  = "PlayerIdle";

    [Header("中间战斗日志")]
    [SerializeField] private TextMeshProUGUI battleLogText;

    [Header("右侧敌人")]
    [SerializeField] private Image enemyImage;
    [SerializeField] private TextMeshProUGUI enemyStatsText;

    [Header("敌人受击特效（只显示在战斗界面内）")]
    [Tooltip("斩击特效用的精灵；留空则只抖不显示特效")]
    [SerializeField] private Sprite hitEffectSprite;
    [Tooltip("玩家头像攻击动画冲到敌人面前（最远处）的时间点，特效在这一刻才出现")]
    [SerializeField] private float playerAttackHitDelay = 0.5f;
    [Tooltip("单个特效从出现到消失的时长（秒）")]
    [SerializeField] private float hitEffectDuration = 0.3f;
    [Tooltip("多段攻击时，每段特效之间的间隔（秒）")]
    [SerializeField] private float hitEffectStagger = 0.07f;
    [Tooltip("一次攻击最多同时叠几个特效（防止攻击段数过高刷屏）")]
    [SerializeField] private int hitEffectMaxCount = 6;
    [Tooltip("敌人挨打时的抖动幅度（像素），0 = 不抖")]
    [SerializeField] private float enemyShakeAmplitude = 6f;
    [Tooltip("敌人抖动的持续时间（秒）")]
    [SerializeField] private float enemyShakeDuration = 0.18f;

    [Header("敌人阵亡特效")]
    [Tooltip("阵亡表现的总时长（秒）：抖动 + 下移 + 淡出一起走完")]
    [SerializeField] private float enemyDeathDuration = 0.6f;
    [Tooltip("阵亡时精灵图向下移动的距离（像素）")]
    [SerializeField] private float enemyDeathDropDistance = 24f;

    private Vector2 enemyBasePos;                  // 敌人图原位（抖动/下移的基准），开战时记录
    private Color   enemyBaseColor = Color.white;  // 敌人图原色（淡出的基准）
    private bool    enemyBaseCached;
    private Coroutine enemyShakeCo;                // 受击抖动协程，阵亡时要先停掉

    /// <summary>表现倍速（1=正常、2=两倍、4=四倍），由 BattleManager 按战斗日志间隔同步。</summary>
    private float playbackSpeed = 1f;

    public float PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            playbackSpeed = Mathf.Max(0.01f, value);
            Animator anim = PlayerAvatarAnimator;   // 已解析过就顺手更新播放速度
            if (anim != null) anim.speed = playbackSpeed;
        }
    }

    /// <summary>把"正常速度下的秒数"换算成当前倍速下的秒数。</summary>
    private float Scaled(float seconds) => seconds / Mathf.Max(0.01f, playbackSpeed);

    private List<string> logLines = new List<string>();
    private bool isAnimating = false;

    /// <summary>
    /// 当前场景中的战斗界面实例（挂在 BattleWindow 上）。
    /// 玩家在战斗中受伤/攻击时，PlayerData 通过它驱动左侧头像的动画。
    /// </summary>
    public static BattleUI Instance { get; private set; }

    public bool IsOpen => battleWindow != null && battleWindow.activeInHierarchy;

    void Awake()
    {
        Instance = this;

        // 默认关闭
        if (battleWindow != null)
            battleWindow.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 打开战斗窗口并初始化双方数据
    /// </summary>
    public void OpenBattle(PlayerData playerData, EnemyController enemy)
    {
        if (battleWindow == null) return;

        logLines.Clear();
        battleWindow.SetActive(true);
        isAnimating = false;

        // 头像动画复位到待机，避免上一场战斗残留的攻击/受伤姿态
        ResetPlayerAvatarAnim();
        // 复位敌人图（上一场可能以阵亡收尾，留下了淡出/下移），并记录本场基准位置与颜色
        CacheEnemyVisual();

        if (enemyNameText != null)
            enemyNameText.text = enemy != null ? enemy.EnemyName : "？？？";

        SetEnemySprite(enemy != null ? enemy.EnemySprite : null);

        UpdatePlayerPanel(playerData);
        UpdateEnemyPanel(enemy);

        // 初始日志
        if(playerData.Speed >= enemy.Speed)
            AddLog($"<color=#7799CC>我方</color>速度更快，获得先手");
    }

    /// <summary>
    /// 关闭战斗窗口
    /// </summary>
    public void CloseBattle()
    {
        if (battleWindow == null) return;
        // 抖动/阵亡淡出可能正好被打断，先把敌人图还原再隐藏
        RestoreEnemyVisual();
        battleWindow.SetActive(false);
        logLines.Clear();
        isAnimating = false;
    }

    /// <summary>
    /// 更新玩家面板
    /// </summary>
    public void UpdatePlayerPanel(PlayerData playerData)
    {
        if (playerStatsText == null || playerData == null) return;

        playerStatsText.text =
            $"生命值：{playerData.HP}\n" +
            $"攻击力：{playerData.Attack}\n" +
            $"防御力：{playerData.Defense}\n" +
            $"攻击段数：{playerData.AttackCount}\n" +
            $"生命偷取：{playerData.LifeSteal}\n" +
            $"反伤系数：{playerData.ReflectDamage}\n" +
            $"减伤系数：{playerData.DamageReduction}%\n" +
            $"魔力充能：{playerData.ManaCharge}\n" +
            // 注意：这里显示的是 min(魔力充能, 魔力输出上限)，也就是「这一击的魔力输出」。
            // 但面板是在魔力消耗之后才刷新的（结算在 BattleResolver 里：消耗魔力 → 重算本回合伤害，
            // 之后 BattleManager.PlayPlayerAttack 才 UpdatePlayerPanel），
            // 所以除了开局那一次，玩家看到的其实是「下一击」的输出值。
            $"魔力输出：{Mathf.Min(playerData.ManaCharge, playerData.ManaMax)}\n" +
            $"速度：{playerData.Speed}";
    }

    /// <summary>
    /// 更新敌人面板
    /// </summary>
    public void UpdateEnemyPanel(EnemyController enemy)
    {
        if (enemyStatsText == null || enemy == null) return;

        enemyStatsText.text =
            $"生命值：{enemy.HP}\n" +
            $"攻击力：{enemy.Attack}\n" +
            $"防御力：{enemy.Defense}\n" +
            $"攻击段数：{enemy.AttackCount}\n" +
            $"生命偷取：{enemy.LifeSteal}\n" +
            $"反伤系数：{enemy.ReflectDamage}\n" +
            $"减伤系数：{enemy.DamageReduction}%\n" +
            $"魔力充能：{enemy.ManaCharge}\n" +
            $"魔力输出：{Mathf.Min(enemy.ManaCharge, enemy.ManaMax)}\n" +
            $"速度：{enemy.Speed}";
    }

    /// <summary>
    /// 更新回合数显示
    /// </summary>
    public void UpdateTurn(int turn)
    {
        if (turnText != null)
            turnText.text = $"回合：{turn}";
    }

    /// <summary>
    /// 向战斗日志添加一行
    /// </summary>
    public void AddLog(string message)
    {
        if (battleLogText == null) return;

        logLines.Add(message);
        if (logLines.Count > 6)
            logLines.RemoveAt(0);

        battleLogText.text = string.Join("\n", logLines);
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    public void ClearLog()
    {
        logLines.Clear();
        if (battleLogText != null)
            battleLogText.text = string.Empty;
    }

    /// <summary>
    /// 直接设置玩家/敌人图片（可选）
    /// </summary>
    public void SetPlayerSprite(Sprite sprite)
    {
        if (playerImage != null && sprite != null)
            playerImage.sprite = sprite;
    }

    /// <summary>玩家头像的 Animator（与 playerImage 同对象，懒解析）</summary>
    private Animator PlayerAvatarAnimator
    {
        get
        {
            if (playerAvatarAnimator == null && playerImage != null)
            {
                playerAvatarAnimator = playerImage.GetComponent<Animator>();
                // 攻击/受击动画跟随战斗倍速（1/2/4 倍）
                if (playerAvatarAnimator != null) playerAvatarAnimator.speed = playbackSpeed;
            }
            return playerAvatarAnimator;
        }
    }

    /// <summary>
    /// 播放玩家头像的攻击动画（PlayerUI.controller 的 isAttack，Trigger 型）。
    /// 玩家每次出手调用一次即可，Attack → Idle 由片段退出时间自动收回。
    /// </summary>
    public void PlayPlayerAttack()
    {
        // 窗口关着说明不在战斗中，此时设置 Trigger 会积压到下一场战斗才播，直接丢弃
        if (!IsOpen) return;

        Animator anim = PlayerAvatarAnimator;
        if (anim != null) anim.SetTrigger(IsAttackHash);
    }

    /// <summary>播放玩家头像的受伤动画（PlayerUI.controller 的 isHurt，Trigger 型）。</summary>
    public void PlayPlayerHurt()
    {
        if (!IsOpen) return;

        Animator anim = PlayerAvatarAnimator;
        if (anim != null) anim.SetTrigger(IsHurtHash);
    }

    /// <summary>把头像动画强制拉回待机状态。</summary>
    private void ResetPlayerAvatarAnim()
    {
        Animator anim = PlayerAvatarAnimator;
        if (anim != null) anim.Play(PlayerIdleStateName, 0, 0f);
    }

    public void SetEnemySprite(Sprite sprite)
    {
        if (enemyImage != null && sprite != null)
            enemyImage.sprite = sprite;
    }

    // ============================================================
    //  敌人受击特效
    // ============================================================

    /// <summary>
    /// 播放敌人受击表现（敌人图抖动 + 斩击特效）。按攻击段数依次叠出多个特效，
    /// 只在战斗界面内显示（地图上的敌人不受影响）。玩家每次出手调用一次即可。
    ///
    /// 注意：调用后不会立刻出现，而是等玩家头像冲到敌人面前（攻击动画最远处）才开始，
    /// 时序由 playerAttackHitDelay 控制。
    /// </summary>
    /// <param name="hitCount">本次攻击的段数（通常传 playerData.AttackCount）</param>
    public void PlayEnemyHit(int hitCount)
    {
        if (!IsOpen || enemyImage == null) return;

        StartCoroutine(EnemyHitSequence(hitCount));
    }

    /// <summary>命中表现的时序：等头像冲到敌人面前 → 敌人抖一下 → 叠出斩击特效。</summary>
    private IEnumerator EnemyHitSequence(int hitCount)
    {
        // 等待和间隔都按倍速缩短，保证 2/4 倍速下打击感还跟得上
        float delay = Scaled(Mathf.Max(0f, playerAttackHitDelay));
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 等待期间战斗可能已经结束（窗口关闭），此时不要再表演
        if (!IsOpen || enemyImage == null) yield break;

        if (enemyShakeCo != null) StopCoroutine(enemyShakeCo);
        enemyShakeCo = StartCoroutine(EnemyShakeRoutine());

        if (hitEffectSprite == null) yield break;

        int count = Mathf.Clamp(hitCount, 1, Mathf.Max(1, hitEffectMaxCount));
        for (int i = 0; i < count; i++)
            StartCoroutine(EnemyHitEffectRoutine(Scaled(i * hitEffectStagger)));
    }

    /// <summary>
    /// 复位并记录敌人图的基准位置与颜色。
    /// 每次开战调用：先把上一场残留的淡出/下移清掉，再记录本场基准。
    /// </summary>
    private void CacheEnemyVisual()
    {
        if (enemyImage == null) return;

        if (enemyBaseCached)
        {
            enemyImage.rectTransform.anchoredPosition = enemyBasePos;
            enemyImage.color = enemyBaseColor;
        }

        enemyBasePos = enemyImage.rectTransform.anchoredPosition;
        enemyBaseColor = enemyImage.color;
        enemyBaseCached = true;
    }

    /// <summary>把敌人图放回原位、恢复不透明度。</summary>
    private void RestoreEnemyVisual()
    {
        if (!enemyBaseCached || enemyImage == null) return;
        enemyImage.rectTransform.anchoredPosition = enemyBasePos;
        enemyImage.color = enemyBaseColor;
    }

    /// <summary>敌人挨打：以原位为中心快速抖动，幅度随时间衰减，结束后归位。</summary>
    private IEnumerator EnemyShakeRoutine()
    {
        if (enemyImage == null) yield break;
        RectTransform rt = enemyImage.rectTransform;
        if (!enemyBaseCached) CacheEnemyVisual();

        float dur = Mathf.Max(0.05f, enemyShakeDuration);
        float amp = Mathf.Max(0f, enemyShakeAmplitude);
        float t = 0f;
        while (t < dur)
        {
            if (rt == null) yield break;   // 战斗界面被销毁，提前收工

            t += Time.deltaTime * (playbackSpeed > 0.01f ? playbackSpeed : 0.01f);   // 虚拟时间：倍速越高走得越快
            float damp = 1f - Mathf.Clamp01(t / dur);   // 越接近结束抖得越轻
            float a = amp * damp;
            rt.anchoredPosition = enemyBasePos + new Vector2(
                UnityEngine.Random.Range(-a, a),
                UnityEngine.Random.Range(-a, a) * 0.5f);   // 竖向幅度减半，像被推了一下
            yield return null;
        }
        rt.anchoredPosition = enemyBasePos;
    }

    // ============================================================
    //  敌人阵亡特效
    // ============================================================

    /// <summary>
    /// 播放敌人阵亡表现：精灵图轻轻抖动、缓缓下移，并从抖动一开始就逐渐透明，最后完全消失。
    /// 战斗胜利、敌人被击败时调用一次。
    /// </summary>
    public void PlayEnemyDeath()
    {
        if (!IsOpen || enemyImage == null) return;

        StartCoroutine(EnemyDeathRoutine());
    }

    private IEnumerator EnemyDeathRoutine()
    {
        if (enemyImage == null) yield break;
        if (!enemyBaseCached) CacheEnemyVisual();

        // 停掉还在跑的受击抖动，避免两个协程抢同一个位置
        if (enemyShakeCo != null)
        {
            StopCoroutine(enemyShakeCo);
            enemyShakeCo = null;
        }

        Image img = enemyImage;
        RectTransform rt = img.rectTransform;
        Color c0 = enemyBaseColor;

        float dur = Mathf.Max(0.05f, enemyDeathDuration);
        float drop = Mathf.Max(0f, enemyDeathDropDistance);
        float amp = Mathf.Max(0f, enemyShakeAmplitude) * 0.7f;   // 比受击抖得轻一些
        float t = 0f;
        while (t < dur)
        {
            if (rt == null || img == null) yield break;

            t += Time.deltaTime * (playbackSpeed > 0.01f ? playbackSpeed : 0.01f);
            float p = Mathf.Clamp01(t / dur);

            // 抖动幅度随进度衰减，同时整体缓缓下沉
            float a = amp * (1f - p);
            rt.anchoredPosition = enemyBasePos + new Vector2(
                UnityEngine.Random.Range(-a, a),
                -drop * p + UnityEngine.Random.Range(-a, a) * 0.5f);

            // 从抖动一开始就匀速淡出，落到 0
            img.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - p));
            yield return null;
        }

        if (rt != null) rt.anchoredPosition = enemyBasePos + new Vector2(0f, -drop);
        if (img != null) img.color = new Color(c0.r, c0.g, c0.b, 0f);
    }

    /// <summary>单个斩击特效：延迟 delay 后生成在敌人图上，随机偏移/旋转，播完自毁。</summary>
    private IEnumerator EnemyHitEffectRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 延迟期间战斗可能已经结束（窗口关闭），此时不要再生成
        if (!IsOpen || enemyImage == null) yield break;

        GameObject go = new GameObject("EnemyHitEffect", typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        // 挂在敌人图下、并作为最后一个子物体 → uGUI 按层级绘制，天然盖在敌人精灵上面
        rt.SetParent(enemyImage.rectTransform, false);

        // 与敌人图同尺寸并居中，再随机偏移/旋转/缩放，让多段攻击不叠成一坨
        Vector2 size = enemyImage.rectTransform.rect.size;
        if (size.x < 1f || size.y < 1f) size = new Vector2(100f, 100f);   // 布局未计算时的兜底
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(
            UnityEngine.Random.Range(-0.22f, 0.22f) * size.x,
            UnityEngine.Random.Range(-0.22f, 0.22f) * size.y);
        float baseAngle = UnityEngine.Random.Range(-20f, 20f);
        float baseScale = UnityEngine.Random.Range(0.75f, 1.05f);
        rt.localRotation = Quaternion.Euler(0f, 0f, baseAngle);

        Image img = go.GetComponent<Image>();
        img.sprite = hitEffectSprite;
        img.preserveAspect = false;
        img.raycastTarget = false;   // 绝不能挡住敌人图下方的 UI 交互

        // 快速亮起 → 缓慢消失，同时轻微放大
        float dur = Mathf.Max(0.05f, hitEffectDuration);
        float t = 0f;
        while (t < dur)
        {
            if (go == null) yield break;   // 战斗界面被销毁，提前收工

            t += Time.deltaTime * (playbackSpeed > 0.01f ? playbackSpeed : 0.01f);   // 虚拟时间：倍速越高走得越快
            float p = Mathf.Clamp01(t / dur);
            float alpha = p < 0.25f
                ? Mathf.InverseLerp(0f, 0.25f, p)
                : 1f - Mathf.InverseLerp(0.25f, 1f, p);

            img.color = new Color(1f, 1f, 1f, alpha);
            rt.localScale = Vector3.one * baseScale * Mathf.Lerp(0.85f, 1.15f, p);
            yield return null;
        }

        if (go != null) Destroy(go);
    }
}
