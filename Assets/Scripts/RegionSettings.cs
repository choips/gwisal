using System.Collections;
using System.Collections.Generic;
using TMPro;                     // TMP_FontAsset 사용 (머리 위 이름표 글꼴)
using UnityEngine;

// 지역 설정 (지역 씬마다 하나, 빈 오브젝트에 붙임)
// 지역 레벨(= 이 지역 몬스터 레벨)과 귀신 프리팹을 정하고,
// 지역 준비가 끝나면 이 씬에 놓인 고정 배치 무리(EnemyPack)를 모두 채웁니다.
// 위협 알림("챔피언 무리 ○개…")과 "모든 요괴를 처치했습니다" 알림도 여기서 띄웁니다.
public class RegionSettings : MonoBehaviour
{
    [Tooltip("지역 레벨 = 이 지역 몬스터 레벨 (체력·피해 배율, 떨어지는 아이템 레벨)")]
    [Min(1)] [SerializeField] private int regionLevel = 1;

    [Tooltip("귀신 프리팹 (Assets/Prefabs/Enemy)")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("챔피언·유니크·하수인 머리 위 이름표 글꼴 (비워 두면 Enemy 프리팹의 LootDropper 글꼴)")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("이 지역에서 살아 있는 몬스터 수")]
    [SerializeField] private int aliveEnemyCount;

    private readonly List<EnemyPack> packs = new List<EnemyPack>();
    private BalanceConfig config;   // 레벨 성장·등급 배율 (플레이어가 가진 밸런스 숫자 모음)
    private bool hasPopulated;      // 무리를 다 세웠는지 (마지막 한 마리 처치 알림용)

    public int RegionLevel => regionLevel;

    // 지역 씬이 막 불러와진 순간에는 플레이어가 아직 입구로 옮겨지기 전이므로, 지역 준비가 끝날 때까지 기다림
    private IEnumerator Start()
    {
        while (RegionManager.IsBusy) yield return null;

        if (enemyPrefab == null)
        {
            Debug.LogWarning($"{name}: Enemy Prefab이 연결되지 않아 고정 배치 무리를 세우지 않습니다.");
            yield break;
        }

        if (labelFont == null && enemyPrefab.TryGetComponent(out LootDropper lootDropper))
        {
            labelFont = lootDropper.LabelFont;
        }

        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null && stats.Stats != null) config = stats.Stats.Config;

        CollectPacks();

        int championPacks = 0;
        int uniquePacks = 0;
        foreach (EnemyPack pack in packs)
        {
            int spawned = pack.Populate(this);
            if (spawned == 0) continue;

            aliveEnemyCount += spawned;
            if (pack.Kind == EnemyPack.PackKind.Champion) championPacks++;
            else if (pack.Kind == EnemyPack.PackKind.Unique) uniquePacks++;
        }

        hasPopulated = true;
        GameLog.Region($"고정 배치 완료 (지역 레벨 {regionLevel}): 무리 {packs.Count}개, 총 {aliveEnemyCount}마리");
        if (aliveEnemyCount > 0)
        {
            NotificationUI.Show(Loc.Format("notice.area_threats", championPacks, uniquePacks));
        }
    }

    // 이 씬에서 켜져 있는 고정 배치 무리를 모두 찾음 (꺼 둔 무리는 세우지 않음, 지역을 불러올 때 한 번만)
    private void CollectPacks()
    {
        packs.Clear();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            packs.AddRange(root.GetComponentsInChildren<EnemyPack>());
        }
    }

    // 무리가 구성원 한 마리를 세울 때 호출 (이 지역의 레벨·프리팹·글꼴로 만듦)
    public Enemy SpawnMonster(EnemyData data, MonsterRank rank, string labelText, Vector3 position)
    {
        return MonsterFactory.Spawn(enemyPrefab, data, regionLevel, rank, labelText, position, config, labelFont);
    }

    // 무리 구성원이 죽을 때 무리가 호출
    public void OnPackMemberDied()
    {
        aliveEnemyCount = Mathf.Max(aliveEnemyCount - 1, 0);

        if (hasPopulated && aliveEnemyCount == 0)
        {
            NotificationUI.Show(Loc.Get("notice.area_cleared"), NotificationUI.NoticeType.Success);
        }
    }
}
