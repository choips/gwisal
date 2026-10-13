// 수정치를 더하는 방식 (세이브·아이템 옵션에 번호로 저장될 수 있으므로 새 값은 맨 뒤에만 추가)
public enum StatModifierType
{
    Flat = 0,        // 고정값 더하기 (예: 최대 체력 +20)
    PercentAdd = 1,  // % 더하기 — 같은 능력치의 % 더하기끼리는 합산 후 한 번에 곱함 (예: +10%와 +20% → ×1.3)
    PercentMult = 2, // % 곱하기 — 하나씩 따로 곱함 (예: ×1.1과 ×1.2 → ×1.32)
}

// 능력치 수정치 하나 (예: "장착한 갑옷이 최대 체력 +20", "레벨 보너스")
// 출처(Source)를 함께 기억해서, 장비를 벗을 때 그 장비가 준 수정치만 골라 지울 수 있습니다.
public readonly struct StatModifier
{
    public readonly StatType Stat;          // 바꿀 능력치
    public readonly StatModifierType Type;  // 더하는 방식
    public readonly float Value;            // 값 (Flat: 그대로 더함 / % 방식: 0.1 = 10%)
    public readonly object Source;          // 출처 (장비 아이템, 버프 등 — 지울 때 비교용)

    public StatModifier(StatType stat, StatModifierType type, float value, object source)
    {
        Stat = stat;
        Type = type;
        Value = value;
        Source = source;
    }
}
