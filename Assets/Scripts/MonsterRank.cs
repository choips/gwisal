using UnityEngine;

// 몬스터 등급 (기획: 일반 · 정예 · 이름 있는 정예 · 하수인)
// 세이브·씬 파일에 번호로 저장되므로 값은 바꾸지 않음 (0=일반, 1=정예, 2=이름 있는 정예, 3=하수인)
// 예전 이름 Champion·Unique는 Elite·NamedElite로만 바꿈 (📬 전달-26)
public enum MonsterRank { Normal, Elite, NamedElite, Minion }

// Inspector에서 "최소 ~ 최대"를 입력받는 정수 범위 (예: 정예 무리의 일반 2~3마리)
[System.Serializable]
public struct IntRange
{
    public int min;
    public int max;

    public IntRange(int minValue, int maxValue)
    {
        min = minValue;
        max = maxValue;
    }

    // min 이상 max 이하의 정수 하나를 무작위로 뽑음 (min > max로 잘못 입력해도 동작)
    public int Roll()
    {
        int low = Mathf.Min(min, max);
        int high = Mathf.Max(min, max);
        return Random.Range(low, high + 1); // 정수 Random.Range는 끝값을 포함하지 않으므로 +1
    }
}
