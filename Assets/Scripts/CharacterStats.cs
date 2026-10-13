using System;
using System.Collections.Generic;

// 캐릭터 한 명의 능력치 계산기 (씬에 붙이지 않는 일반 C# 클래스 — PlayerStats 등이 하나씩 만들어 가짐)
// 기본값(레벨에 따른 값 등) + 수정치(장비·버프)를 모아 최종값을 계산합니다.
//   최종값 = (기본값 + 고정 더하기 합) × (1 + % 더하기 합) × (1 + % 곱하기 각각) → 상한 적용
// 기본값이나 수정치가 바뀔 때만 다시 계산하고(매 프레임 계산하지 않음), 그사이에는 계산해 둔 값을 돌려줍니다.
public class CharacterStats
{
    private static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length; // 능력치 종류 수

    private readonly BalanceConfig config;            // 상한 등 밸런스 숫자
    private readonly float[] baseValues = new float[StatCount];  // 능력치별 기본값
    private readonly float[] finalValues = new float[StatCount]; // 능력치별 최종값 (계산해 둔 값)
    private readonly List<StatModifier> modifiers = new List<StatModifier>(); // 붙어 있는 수정치 목록
    private bool isDirty = true; // 다시 계산해야 하는지

    // 최종값이 다시 계산될 때마다 1씩 늘어나는 번호 (UI가 "바뀌었을 때만" 글자를 새로 만들 때 비교용)
    public int Version { get; private set; }

    // 최종값이 다시 계산된 직후 알림 (체력바 최대치 갱신 등)
    public event Action Changed;

    public CharacterStats(BalanceConfig config)
    {
        this.config = config;
    }

    // 최종값 읽기 (바뀐 것이 있으면 이때 한 번 다시 계산)
    public float Get(StatType stat)
    {
        if (isDirty) Recalculate();
        return finalValues[(int)stat];
    }

    // 기본값 읽기 (수정치를 적용하기 전 값)
    public float GetBase(StatType stat)
    {
        return baseValues[(int)stat];
    }

    // 기본값 바꾸기 (레벨에 따른 값 등)
    public void SetBase(StatType stat, float value)
    {
        int index = (int)stat;
        if (baseValues[index] == value) return;

        baseValues[index] = value;
        isDirty = true;
    }

    public void AddModifier(StatModifier modifier)
    {
        modifiers.Add(modifier);
        isDirty = true;
    }

    // 특정 출처(장비 등)가 준 수정치를 모두 지움 — 지운 것이 있으면 true
    public bool RemoveModifiersFromSource(object source)
    {
        int removed = modifiers.RemoveAll(m => Equals(m.Source, source));
        if (removed > 0) isDirty = true;
        return removed > 0;
    }

    // 바뀐 것이 있으면 지금 바로 다시 계산하고 Changed 알림을 보냄 (장비를 바꾼 직후 등)
    public void Refresh()
    {
        if (isDirty) Recalculate();
    }

    // 모든 능력치의 최종값을 다시 계산
    private void Recalculate()
    {
        isDirty = false;

        for (int i = 0; i < StatCount; i++)
        {
            StatType stat = (StatType)i;
            float flat = baseValues[i];
            float percentAdd = 0f;
            float percentMult = 1f;

            for (int m = 0; m < modifiers.Count; m++)
            {
                StatModifier modifier = modifiers[m];
                if (modifier.Stat != stat) continue;

                switch (modifier.Type)
                {
                    case StatModifierType.Flat: flat += modifier.Value; break;
                    case StatModifierType.PercentAdd: percentAdd += modifier.Value; break;
                    case StatModifierType.PercentMult: percentMult *= 1f + modifier.Value; break;
                }
            }

            finalValues[i] = flat * (1f + percentAdd) * percentMult;
        }

        ApplyCaps();

        Version++;
        Changed?.Invoke();
    }

    // 상한 적용 (저항 상한은 "최대 저항 +%"로 올라가므로 다른 능력치를 다 계산한 뒤 적용)
    private void ApplyCaps()
    {
        for (int i = 0; i < StatCount; i++)
        {
            float cap = config != null ? config.GetCap((StatType)i, finalValues[(int)StatType.MaxResistance]) : float.MaxValue;
            if (finalValues[i] > cap) finalValues[i] = cap;
        }
    }
}
