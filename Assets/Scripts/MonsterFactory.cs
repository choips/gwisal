using TMPro;                     // TMP_FontAsset 사용 (머리 위 이름표 글꼴)
using UnityEngine;

// 몬스터 한 마리 만들기 (씬에 붙이지 않는 정적 클래스)
// 풀에서 꺼냄 → 귀신 데이터·레벨 적용 → 등급 적용(BalanceConfig의 배율) → 몸 색·크기 → 머리 위 이름표
// 고정 배치 무리(EnemyPack)와 무작위 배치기(EnemySpawner)가 함께 사용합니다.
public static class MonsterFactory
{
    public static readonly Color EliteColor = new Color(0.4f, 0.6f, 1f);      // 파랑 (정예)
    public static readonly Color NamedEliteColor = new Color(1f, 0.8f, 0.2f); // 금색 (이름 있는 정예)
    public static readonly Color MinionColor = new Color(0.85f, 0.85f, 0.85f); // 밝은 회색

    private const float LabelFontSize = 3.5f;
    private const float LabelHeight = 1.7f; // 몸 중심에서 이름표까지의 높이(m), 몸 크기에 맞춰 늘어남

    // 이름 있는 정예 이름의 앞말·뒷말 수 (화면 글자 표 enemy.named_elite_prefix.0~5, enemy.named_elite_suffix.0~2)
    private const int NamedElitePrefixCount = 6;
    private const int NamedEliteSuffixCount = 3;

    // 몬스터 한 마리를 풀에서 꺼내 준비함 (실패하면 null)
    // labelText: 머리 위 이름표 (비우면 이름표 없음), config: 레벨 성장·등급 배율 (null이면 배율 1)
    public static Enemy Spawn(GameObject prefab, EnemyData data, int level, MonsterRank rank, string labelText,
                              Vector3 position, BalanceConfig config, TMP_FontAsset labelFont)
    {
        if (prefab == null || data == null) return null;

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject monsterObject = ObjectPoolManager.Spawn(prefab, position, rotation);
        if (monsterObject == null) return null;

        if (!monsterObject.TryGetComponent(out Enemy enemy))
        {
            Debug.LogWarning($"{prefab.name} 프리팹에 Enemy 스크립트가 없어 몬스터로 쓸 수 없습니다.");
            ObjectPoolManager.Release(monsterObject);
            return null;
        }

        monsterObject.name = string.IsNullOrEmpty(labelText) ? data.LocalizedName : labelText;

        // 레벨 성장 배율 × 개발자 메뉴(F1) 배율 (출시 빌드는 DevCheats 배율이 항상 1)
        float healthMultiplier = (config != null ? config.GetEnemyHealthMultiplier(level) : 1f) * DevCheats.EnemyHealthMult;
        float damageMultiplier = (config != null ? config.GetEnemyDamageMultiplier(level) : 1f) * DevCheats.EnemyDamageMult;
        enemy.ApplyData(data, level, healthMultiplier, damageMultiplier);

        float healthMult = config != null ? config.GetRankHealthMult(rank) : 1f;
        float damageMult = config != null ? config.GetRankDamageMult(rank) : 1f;
        float expMult = config != null ? config.GetRankExpMult(rank) : 1f;
        int lootDrops = config != null ? config.GetRankLootDrops(rank) : 1;
        float rarityBonus = config != null ? config.GetRankRarityBonus(rank) : 1f;
        lootDrops = Mathf.Max(0, Mathf.RoundToInt(lootDrops * DevCheats.EnemyLootMult));
        rarityBonus *= DevCheats.EnemyLootMult;
        enemy.ApplyRank(rank, healthMult, damageMult, expMult, lootDrops, rarityBonus);

        enemy.SetBodyColor(GetBodyColor(data.bodyColor, rank));

        Vector3 bodyScale = Vector3.Scale(monsterObject.transform.localScale, data.bodyScale) * GetRankScale(rank);
        monsterObject.transform.localScale = bodyScale;

        if (!string.IsNullOrEmpty(labelText) && labelFont != null)
        {
            monsterObject.AddComponent<ItemNameLabel>().Setup(
                labelText, GetLabelColor(rank), labelFont, LabelFontSize, LabelHeight * bodyScale.y);
        }

        return enemy;
    }

    public static string MakeEliteName(EnemyData data)
    {
        return Loc.Format("enemy.elite_name", data.LocalizedName);
    }

    public static string MakeMinionName(EnemyData data)
    {
        return Loc.Format("enemy.minion_name", data.LocalizedName);
    }

    // 예: "천년 묵은 망자 왕"
    public static string MakeNamedEliteName(EnemyData data)
    {
        string prefix = Loc.Get("enemy.named_elite_prefix." + Random.Range(0, NamedElitePrefixCount));
        string suffix = Loc.Get("enemy.named_elite_suffix." + Random.Range(0, NamedEliteSuffixCount));
        return Loc.Format("enemy.named_elite_name", prefix, data.LocalizedName, suffix);
    }

    public static Color GetLabelColor(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Elite: return EliteColor;
            case MonsterRank.NamedElite: return NamedEliteColor;
            case MonsterRank.Minion: return MinionColor;
            default: return Color.white;
        }
    }

    // 정예는 파란빛, 이름 있는 정예는 금빛이 돌게 몸 색을 섞음
    private static Color GetBodyColor(Color typeColor, MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Elite: return Color.Lerp(typeColor, EliteColor, 0.5f);
            case MonsterRank.NamedElite: return Color.Lerp(typeColor, NamedEliteColor, 0.5f);
            default: return typeColor;
        }
    }

    // 강한 등급일수록 조금 더 크게
    private static float GetRankScale(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Elite: return 1.15f;
            case MonsterRank.NamedElite: return 1.35f;
            default: return 1f;
        }
    }
}
