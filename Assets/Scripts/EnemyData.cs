using UnityEngine;

/// <summary>
/// 敌人数据资产 — 右键 Create → MagicTower → Enemy Data 创建 .asset 文件
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "MagicTower/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("基本信息")]
    public string enemyName = "绿史莱姆";
    public Sprite enemySprite;

    [Header("战斗属性")]
    [Tooltip("攻击力")]
    public int attack = 10;
    [Tooltip("防御力")]
    public int defense = 5;
    [Tooltip("生命值")]
    public int hp = 50;
    [Tooltip("击败后获得的金币")]
    public int goldReward = 5;

    [Header("特殊属性")]
    [Tooltip("是否先手攻击")]
    public bool firstStrike = false;
    [Tooltip("是否远程攻击")]
    public bool rangedAttack = false;
}
