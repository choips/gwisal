using UnityEngine;

// 몬스터 등급 (디아블로 방식)
// 일반 / 챔피언(무리 전체가 강함) / 유니크(이름 있는 우두머리) / 하수인(유니크를 따르는 부하)
// 세이브 파일 등에 숫자로 저장될 수 있으므로 새 등급은 맨 뒤에 추가
public enum MonsterRank { Normal, Champion, Unique, Minion }

// 몬스터 종류 하나의 설정 (잡귀, 강시, 아귀, 도깨비 등)
// Enemy 프리팹의 기본 능력치에 아래 배율을 곱해서 종류마다 다르게 만듭니다.
[System.Serializable]
public class MonsterType
{
    [Tooltip("몬스터 이름 (머리 위 이름표와 Console에 표시)")]
    public string monsterName = "잡귀";

    [Tooltip("몸 색")]
    public Color color = Color.gray;

    [Tooltip("몸 크기 배율 (X 너비, Y 키, Z 두께)")]
    public Vector3 scale = Vector3.one;

    [Tooltip("체력 배율 (Enemy 프리팹의 Max Health에 곱함)")]
    public float healthMultiplier = 1f;

    [Tooltip("공격력 배율 (Enemy 프리팹의 Attack Damage에 곱함)")]
    public float damageMultiplier = 1f;

    [Tooltip("이동 속도 배율 (NavMeshAgent의 Speed에 곱함)")]
    public float speedMultiplier = 1f;

    [Tooltip("공격 쿨타임 배율 (작을수록 자주 공격)")]
    public float attackCooldownMultiplier = 1f;

    [Tooltip("처치 경험치 배율")]
    public float experienceMultiplier = 1f;

    [Tooltip("무리로 나올 확률 가중치 (클수록 자주 나옴, 0이면 안 나옴)")]
    public float spawnWeight = 10f;

    public MonsterType() { }

    public MonsterType(string name, Color bodyColor, Vector3 bodyScale, float health, float damage,
                       float speed, float attackCooldown, float experience, float weight)
    {
        monsterName = name;
        color = bodyColor;
        scale = bodyScale;
        healthMultiplier = health;
        damageMultiplier = damage;
        speedMultiplier = speed;
        attackCooldownMultiplier = attackCooldown;
        experienceMultiplier = experience;
        spawnWeight = weight;
    }
}

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
