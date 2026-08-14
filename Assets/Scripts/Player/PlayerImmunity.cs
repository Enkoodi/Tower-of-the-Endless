using UnityEngine;

/// <summary>
/// 玩家免疫组件 — 由护身符装备拾取时添加到玩家身上。
/// 同时免疫魔力光环（MagicAuraAttack）和夹击（PincerAttack）伤害。
/// 注意：不影响正常战斗中的魔力伤害。
/// </summary>
public class PlayerImmunity : MonoBehaviour, IMagicDamageImmune
{
    public bool IsImmuneToMagicDamage => true;
}
