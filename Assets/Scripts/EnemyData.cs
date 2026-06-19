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
    public int hp = 50;
    public int attack = 10;
    public int defense = 5;
    public int attackCount = 1;
    public int lifeSteal = 0;
    public int reflectDamage = 0;
    public int manaCharge = 0;
    public int manaMax = 100;
    public int speed = 5;
    public int goldReward = 5;
}
