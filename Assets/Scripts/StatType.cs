// 능력치 종류 (기획서 "능력치 & 계산식"의 플레이어 능력치 표)
// 세이브·아이템 옵션에 번호로 저장되므로 번호를 바꾸거나 중간에 끼워 넣지 않습니다. 새 능력치는 반드시 맨 뒤에 추가합니다.
// % 능력치는 비율로 저장합니다 (0.1 = 10%). 예) 치명타 확률 0.1 = 10%, 치명타 피해 1.0 = +100%(×2)
public enum StatType
{
    // 공격
    WeaponDamageMin = 0,     // 무기 피해 최소
    WeaponDamageMax = 1,     // 무기 피해 최대
    AddedDamagePercent = 2,  // 추가 피해 %
    SkillDamagePercent = 3,  // 스킬 피해 %
    FireDamagePercent = 4,   // 화(火) 속성 피해 %
    WaterDamagePercent = 5,  // 수(水) 속성 피해 %
    PoisonDamagePercent = 6, // 독(毒) 속성 피해 %
    YinDamagePercent = 7,    // 음(陰) 속성 피해 %
    CritChance = 8,          // 치명타 확률
    CritDamage = 9,          // 치명타 피해 (+%)
    AttackSpeed = 10,        // 공격 속도 (시전 속도, +%)

    // 방어
    MaxHealth = 11,          // 최대 체력
    Armor = 12,              // 방어력 (물리 피해 감소로 바뀜)
    FireResistance = 13,     // 화 저항
    WaterResistance = 14,    // 수 저항
    PoisonResistance = 15,   // 독 저항
    YinResistance = 16,      // 음 저항
    DamageReduction = 17,    // 피해 감소 % (모든 피해)
    HealthRegen = 18,        // 체력 재생 (초당)
    HealthOnKill = 19,       // 처치 시 체력 회복

    // 기력
    MaxMana = 20,            // 최대 기력
    ManaRegen = 21,          // 기력 회복 (초당)
    ManaCostReduction = 22,  // 기력 소모 감소

    // 유틸
    MoveSpeed = 23,          // 이동 속도 (+%)
    CooldownReduction = 24,  // 쿨타임 감소
    PotionEffect = 25,       // 물약 회복량 %
    ItemFind = 26,           // 아이템 발견 %
    GoldFind = 27,           // 엽전 획득 %
    SkillRank = 28,          // 스킬 랭크 +N
    MaxResistance = 29,      // 최대 저항 +% (저항 상한을 올림)
}
