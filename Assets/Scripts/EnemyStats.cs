using UnityEngine;

/// <summary>
/// 敌人数据资产 — 右键 Create → MagicTower → Enemy Stats 创建 .asset
/// 拖入 EnemyController 的 Stats 字段即可。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "MagicTower/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    [Header("基本信息")]
    public string enemyName = "???";
    public Sprite enemySprite;

    [Header("战斗属性")]
    public int hp = 10;
    public int attack = 5;
    public int defense = 0;
    public int attackCount = 1;
    public int lifeSteal = 0;
    public int reflectDamage = 0;
    public int manaCharge = 0;
    public int manaMax = 100;
    public int speed = 5;
    public int goldReward = 0;
}
