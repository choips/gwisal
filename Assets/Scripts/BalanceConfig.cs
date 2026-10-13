using UnityEngine;

// 밸런스 숫자 모음 (ScriptableObject 데이터 파일 — Project 창에서 Create > 귀살 > Balance Config로 만듦)
// 기획서 "능력치 & 계산식"의 상수·상한을 한곳에 모아, 코드를 고치지 않고 Inspector에서 숫자만 바꿔 조정합니다.
// 플레이 중에 바꿔도 바로 반영되고, 플레이를 멈춰도 값이 유지됩니다 (ScriptableObject 파일이므로).
// 지금 숫자는 "지금 게임 체감 유지"용 출발점입니다 (2026-10-13 결정: 기획 수치로의 전환은 EnemyData·귀신 작업 때 엑셀 기준으로).
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "귀살/Balance Config")]
public class BalanceConfig : ScriptableObject
{
    [Header("플레이어 기본 공격 (장비 없을 때)")]
    [Tooltip("맨손 무기 피해 최소 (기획 수치 1)")]
    [Min(0f)] public float unarmedDamageMin = 8f;

    [Tooltip("맨손 무기 피해 최대 (기획 수치 3)")]
    [Min(0f)] public float unarmedDamageMax = 12f;

    [Tooltip("기본 치명타 확률 (0.1 = 10%, 기획 수치 0.05)")]
    [Range(0f, 1f)] public float baseCritChance = 0.1f;

    [Tooltip("기본 치명타 피해 (+%, 1 = +100% = 2배, 기획 수치 0.5)")]
    [Min(0f)] public float baseCritDamage = 1f;

    [Header("플레이어 체력 / 기력 (레벨 1 기준 + 레벨당 증가)")]
    [Min(1f)] public float baseMaxHealth = 100f;   // 레벨 1 최대 체력
    [Min(0f)] public float healthPerLevel = 10f;   // 레벨당 최대 체력 증가
    [Min(1f)] public float baseMaxMana = 100f;     // 레벨 1 최대 기력
    [Min(0f)] public float manaPerLevel = 2f;      // 레벨당 최대 기력 증가
    [Min(0f)] public float baseManaRegen = 5f;     // 초당 기력 회복

    [Header("방어력 공식: 물리 피해 감소 = 방어력 ÷ (방어력 + A × 적 레벨 + B)")]
    [Tooltip("A: 적 레벨 1마다 더해지는 값 (클수록 높은 레벨 적에게 방어력 효과가 줄어듦)")]
    [Min(0f)] public float armorPerEnemyLevel = 15f;

    [Tooltip("B: 고정으로 더해지는 값")]
    [Min(1f)] public float armorBase = 50f;

    [Tooltip("물리 피해 감소 상한 (0.85 = 85%)")]
    [Range(0f, 1f)] public float armorReductionCap = 0.85f;

    [Header("적 레벨 성장: 레벨 1 값 × 성장률 ^ (몬스터 레벨 − 1)")]
    [Tooltip("레벨 1마다 적 체력에 곱해지는 배율 (1.12 = 12%씩, 레벨 13 ≈ 3.9배)")]
    [Min(1f)] public float enemyHealthGrowth = 1.12f;

    [Tooltip("레벨 1마다 적 피해에 곱해지는 배율 (1.09 = 9%씩, 레벨 13 ≈ 2.8배)")]
    [Min(1f)] public float enemyDamageGrowth = 1.09f;

    [Header("상한 (비율: 0.75 = 75%)")]
    [Range(0f, 1f)] public float critChanceCap = 0.75f;        // 치명타 확률
    [Min(0f)] public float attackSpeedCap = 1f;                // 공격 속도 (+100%)
    [Range(0f, 1f)] public float resistanceCap = 0.75f;        // 저항 기본 상한
    [Range(0f, 1f)] public float resistanceCapLimit = 0.8f;    // "최대 저항 +%"로 올릴 수 있는 저항 상한의 끝
    [Range(0f, 1f)] public float damageReductionCap = 0.5f;    // 피해 감소 %
    [Range(0f, 1f)] public float manaCostReductionCap = 0.4f;  // 기력 소모 감소
    [Min(0f)] public float moveSpeedCap = 0.5f;                // 이동 속도 (+50%)
    [Range(0f, 1f)] public float cooldownReductionCap = 0.4f;  // 쿨타임 감소

    // 플레이어의 레벨에 따른 기본값을 능력치 계산기에 넣음 (장비·버프 수정치는 따로 붙음)
    public void ApplyPlayerBase(CharacterStats stats, int level)
    {
        int levelsGained = Mathf.Max(0, level - 1); // 레벨 1이면 0

        stats.SetBase(StatType.WeaponDamageMin, unarmedDamageMin);
        stats.SetBase(StatType.WeaponDamageMax, unarmedDamageMax);
        stats.SetBase(StatType.CritChance, baseCritChance);
        stats.SetBase(StatType.CritDamage, baseCritDamage);
        stats.SetBase(StatType.MaxHealth, baseMaxHealth + healthPerLevel * levelsGained);
        stats.SetBase(StatType.MaxMana, baseMaxMana + manaPerLevel * levelsGained);
        stats.SetBase(StatType.ManaRegen, baseManaRegen);
    }

    // 몬스터 레벨에 따른 적 체력 배율 (레벨 1이면 1)
    public float GetEnemyHealthMultiplier(int monsterLevel)
    {
        return Mathf.Pow(enemyHealthGrowth, Mathf.Max(0, monsterLevel - 1));
    }

    // 몬스터 레벨에 따른 적 피해 배율 (레벨 1이면 1)
    public float GetEnemyDamageMultiplier(int monsterLevel)
    {
        return Mathf.Pow(enemyDamageGrowth, Mathf.Max(0, monsterLevel - 1));
    }

    // 능력치의 상한 (상한이 없으면 아주 큰 수)
    // maxResistanceBonus: "최대 저항 +%" 최종값 (저항 상한을 올림, resistanceCapLimit까지)
    public float GetCap(StatType stat, float maxResistanceBonus)
    {
        switch (stat)
        {
            case StatType.CritChance: return critChanceCap;
            case StatType.AttackSpeed: return attackSpeedCap;
            case StatType.FireResistance:
            case StatType.WaterResistance:
            case StatType.PoisonResistance:
            case StatType.YinResistance:
                return Mathf.Min(resistanceCap + maxResistanceBonus, resistanceCapLimit);
            case StatType.DamageReduction: return damageReductionCap;
            case StatType.ManaCostReduction: return manaCostReductionCap;
            case StatType.MoveSpeed: return moveSpeedCap;
            case StatType.CooldownReduction: return cooldownReductionCap;
            default: return float.MaxValue;
        }
    }
}
