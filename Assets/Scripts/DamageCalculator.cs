using UnityEngine;

// 피해 공식 모음 (씬에 붙이지 않는 정적 클래스) — 기획서 "능력치 & 계산식"의 ①②
// ① 플레이어가 주는 피해 — 근접 공격·부적 투사체·부적 폭발이 모두 같은 함수를 씀
//   기본 피해 = 무기 피해(최소~최대 중 무작위) × 스킬 배율
//   최종 피해 = 기본 피해 × (1 + 추가 피해% + 스킬 피해%) × (치명타면 1 + 치명타 피해%)
// ② 플레이어가 받는 피해 (물리)
//   받는 피해 = 적 피해 × (1 − 물리 피해 감소) × (1 − 피해 감소%)
//   물리 피해 감소 = 방어력 ÷ (방어력 + 15 × 적 레벨 + 50)   (최대 85%, 숫자는 BalanceConfig)
// 아직 없는 것: 속성 피해%·저항·레벨 차 보정 (적 레벨·저항은 EnemyData(S1-7) 이후, 속성은 스킬 설계 때)
public static class DamageCalculator
{
    // 치명타 판정 전 피해 (무기 피해를 굴리고 배율·% 증가를 적용)
    // skillMultiplier: 스킬 배율 (기본 공격 = 1, 부적 = 2 → 무기 피해의 200%)
    // isSkill: 스킬이면 "스킬 피해 %"도 더함
    public static float RollBaseDamage(CharacterStats stats, float skillMultiplier, bool isSkill)
    {
        float min = stats.Get(StatType.WeaponDamageMin);
        float max = Mathf.Max(min, stats.Get(StatType.WeaponDamageMax));
        float weaponDamage = Random.Range(min, max);

        float increase = stats.Get(StatType.AddedDamagePercent);
        if (isSkill) increase += stats.Get(StatType.SkillDamagePercent); // 같은 구간(% 증가)은 더함

        return weaponDamage * skillMultiplier * (1f + increase);
    }

    // 치명타 판정: 당첨되면 (1 + 치명타 피해%)를 곱하고 isCritical = true
    public static float ApplyCritical(CharacterStats stats, float baseDamage, out bool isCritical)
    {
        isCritical = Random.value < stats.Get(StatType.CritChance);
        return isCritical ? baseDamage * (1f + stats.Get(StatType.CritDamage)) : baseDamage;
    }

    // ② 방어력이 줄여 주는 물리 피해 비율 (0 ~ 상한, 적 레벨이 높을수록 같은 방어력의 효과가 줄어듦)
    public static float GetArmorReduction(CharacterStats stats, int attackerLevel)
    {
        BalanceConfig config = stats.Config;
        float armor = Mathf.Max(0f, stats.Get(StatType.Armor));
        if (armor <= 0f || config == null) return 0f;

        float reduction = armor / (armor + config.armorPerEnemyLevel * Mathf.Max(1, attackerLevel) + config.armorBase);
        return Mathf.Min(reduction, config.armorReductionCap);
    }

    // ② 물리 피해를 방어력·피해 감소%로 줄임
    public static float ReducePhysicalDamage(CharacterStats stats, float damage, int attackerLevel)
    {
        if (stats == null || damage <= 0f) return damage;

        float armorReduction = GetArmorReduction(stats, attackerLevel);
        float damageReduction = Mathf.Clamp01(stats.Get(StatType.DamageReduction));
        return damage * (1f - armorReduction) * (1f - damageReduction);
    }
}
