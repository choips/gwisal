using UnityEngine;

// 피해 공식 모음 (씬에 붙이지 않는 정적 클래스) — 기획서 "능력치 & 계산식"의 ① 플레이어가 주는 피해
// 근접 공격·부적 투사체·부적 폭발이 모두 이 함수를 써서 같은 규칙으로 피해를 정합니다.
//   기본 피해 = 무기 피해(최소~최대 중 무작위) × 스킬 배율
//   최종 피해 = 기본 피해 × (1 + 추가 피해% + 스킬 피해%) × (치명타면 1 + 치명타 피해%)
// 아직 없는 것: 속성 피해%·적 저항·레벨 차 보정 (적 레벨·저항은 EnemyData(S1-7) 이후, 속성은 스킬 설계 때)
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
}
