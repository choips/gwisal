using UnityEngine;

// 피해 측정기 (DPS 미터) — 씬에 붙이지 않는 정적 클래스
// 적이 피해를 받을 때마다(Enemy.TakeDamage) 기록해서 누적 피해·초당 피해(DPS)를 계산합니다. 개발자 메뉴(F1)에서 봅니다.
// 근접 공격·부적·폭발 등 모든 공격이 함께 집계되고, 적 체력을 넘친 피해도 그대로 셉니다 (빌드의 화력 비교용).
// IdleTimeout초 동안 피해가 없으면 측정이 멈추고, 그다음 피해부터 새 전투로 다시 측정합니다.
public static class DamageMeter
{
    private const float IdleTimeout = 5f;   // 이 시간(초) 동안 피해가 없으면 한 전투가 끝난 것으로 봄
    private const float MinElapsed = 1f;    // 첫 타 직후 DPS가 터무니없이 커지지 않도록 최소 측정 시간(초)

    public static float TotalDamage { get; private set; } // 이번 측정의 누적 피해
    public static int HitCount { get; private set; }      // 이번 측정의 적중 횟수
    public static float MaxHit { get; private set; }      // 이번 측정에서 가장 큰 한 방

    private static float firstHitTime; // 이번 측정의 첫 피해 시각
    private static float lastHitTime;  // 가장 최근 피해 시각

    // 측정이 멈춘 상태인지 (마지막 피해 후 IdleTimeout초가 지남)
    public static bool IsIdle => HitCount > 0 && Time.time - lastHitTime > IdleTimeout;

    // 이번 측정 시간(초): 첫 피해부터 지금까지, 멈췄으면 마지막 피해까지
    public static float ElapsedSeconds
    {
        get
        {
            if (HitCount == 0) return 0f;
            float endTime = IsIdle ? lastHitTime : Time.time;
            return endTime - firstHitTime;
        }
    }

    // 초당 피해 (누적 피해 ÷ 측정 시간, 측정 시간은 최소 1초로 계산)
    public static float Dps => HitCount == 0 ? 0f : TotalDamage / Mathf.Max(ElapsedSeconds, MinElapsed);

    // 적이 받은 피해를 기록 (Enemy.TakeDamage에서 호출)
    public static void Record(float damage)
    {
        if (damage <= 0f) return;

        // 한동안 피해가 없었다면 새 전투로 보고 처음부터 측정
        if (IsIdle) Reset();

        if (HitCount == 0) firstHitTime = Time.time;

        TotalDamage += damage;
        HitCount++;
        if (damage > MaxHit) MaxHit = damage;
        lastHitTime = Time.time;
    }

    // 측정 기록을 모두 비움 (개발자 메뉴의 초기화 버튼)
    public static void Reset()
    {
        TotalDamage = 0f;
        HitCount = 0;
        MaxHit = 0f;
        firstHitTime = 0f;
        lastHitTime = 0f;
    }

    // 플레이를 시작할 때마다 이전 플레이의 기록을 비움
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Reset();
    }
}
