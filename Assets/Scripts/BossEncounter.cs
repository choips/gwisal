using System;
using UnityEngine;

// 보스전 흐름 (산군 F1 테스트·나중에 굴 씬에 배치)
// - 전투 시작: 체력바 Bind, (옵션) 입구 닫기
// - 플레이어 사망: 보스 체력 풀 회복
// - 보스 처치: 소환 창귀 해방, 알림, BossCleared 이벤트 (S7 클리어 화면 대비)
// - 귀환 불가: IsActive 플래그 (귀환 스킬이 생기면 이 값을 보고 막음)
// - CC 짧게/면역: 아직 CC 시스템 없음 — 훅만 주석으로 남김
public class BossEncounter : MonoBehaviour
{
    // 슬라이스 클리어·S7에서 구독 가능
    public static event Action<EnemyData> BossCleared;

    // 보스전 중이면 true — 나중에 귀환 스킬이 이 값을 보고 막음
    public static bool IsActive { get; private set; }

    [Header("연결")]
    [Tooltip("이 전투의 보스 (비우면 자동으로 찾거나 Begin에서 받음)")]
    [SerializeField] private Enemy boss;

    [Tooltip("보스 체력바 (비우면 BossHealthBarUI.EnsureExists)")]
    [SerializeField] private BossHealthBarUI healthBar;

    [Tooltip("전투 시작 시 켤 입구 장벽 (없으면 비움 — 굴 씬에서 연결)")]
    [SerializeField] private GameObject entranceBarrier;

    private PlayerStats player;
    private bool cleared;

    // F1 소환 등에서 호출 — 씬에 Encounter가 없으면 임시로 만듦
    public static BossEncounter BeginFor(Enemy bossEnemy)
    {
        if (bossEnemy == null) return null;

        BossEncounter encounter = FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
        {
            GameObject go = new GameObject("BossEncounter");
            encounter = go.AddComponent<BossEncounter>();
        }

        encounter.StartEncounter(bossEnemy);
        return encounter;
    }

    public void StartEncounter(Enemy bossEnemy)
    {
        if (bossEnemy == null) return;

        EndEncounterInternal(false);

        boss = bossEnemy;
        cleared = false;
        IsActive = true;

        if (healthBar == null) healthBar = BossHealthBarUI.EnsureExists();
        if (healthBar != null) healthBar.Bind(boss);

        if (entranceBarrier != null) entranceBarrier.SetActive(true);

        player = FindFirstObjectByType<PlayerStats>();
        if (player != null) player.Died += OnPlayerDied;

        boss.Died += OnBossDied;

        // CC 훅 (시스템 추가 시): 보스 CC 저항 짧게 / 플레이어 CC 면역 2초
        // 이름은 화면 위 보스 체력바에 표시 — 알림으로 띄우면 3초 뒤 사라져 헷갈림
        GameLog.Combat($"보스전 시작: {boss.Data?.LocalizedName}");
    }

    private void OnPlayerDied()
    {
        // 플레이어 사망 시 보스 체력 회복 (기획: 보스 공통 규칙)
        if (boss != null && !boss.IsDead) boss.HealToFull();
        GameLog.Combat("플레이어 사망 → 보스 체력 회복");
    }

    private void OnBossDied(Enemy dead)
    {
        if (cleared) return;
        cleared = true;

        if (dead != null && dead.TryGetComponent(out SangunBossAI ai))
        {
            ai.DespawnAllSummons();
        }

        NotificationUI.Show(Loc.Get("notice.boss_cleared"), NotificationUI.NoticeType.Success);
        BossCleared?.Invoke(dead != null ? dead.Data : null);

        EndEncounterInternal(true);
    }

    private void OnDestroy()
    {
        EndEncounterInternal(false);
    }

    private void EndEncounterInternal(bool fromClear)
    {
        if (player != null)
        {
            player.Died -= OnPlayerDied;
            player = null;
        }

        if (boss != null)
        {
            boss.Died -= OnBossDied;
        }

        if (healthBar != null) healthBar.Unbind();

        // 클리어가 아니면 입구를 다시 열 수 있음 (테스트용). 클리어 후는 열린 채로 둬도 됨
        if (!fromClear && entranceBarrier != null) entranceBarrier.SetActive(false);

        IsActive = false;
        boss = null;
    }
}
