using UnityEngine;

// 몬스터 등급 (디아블로 방식)
// 일반 / 챔피언(무리 전체가 강함) / 유니크(이름 있는 우두머리) / 하수인(유니크를 따르는 부하)
// 세이브 파일 등에 숫자로 저장될 수 있으므로 새 등급은 맨 뒤에 추가
// (귀신 종류별 능력치는 EnemyData 데이터 파일에 있음)
public enum MonsterRank { Normal, Champion, Unique, Minion }

// Inspector에서 "최소 ~ 최대"를 입력받는 정수 범위 (예: 챔피언 2~4마리)
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
