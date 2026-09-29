using UnityEngine;

/// <summary>
/// 神圣盾效果 — 拾取神圣盾装备时添加到玩家身上。
/// 同时免疫魔力光环（MagicAuraAttack）和夹击（PincerAttack）伤害。
/// 注意：不影响正常战斗中的魔力伤害。
/// </summary>
public class HolyShieldEffect : MonoBehaviour, IMagicDamageImmune
{
    public bool IsImmuneToMagicDamage => true;
}
