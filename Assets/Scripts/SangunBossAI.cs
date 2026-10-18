using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 산군(山君) 보스 패턴 AI — Enemy와 같은 오브젝트에 붙음 (EnemyData.isBoss면 ApplyData에서 자동 추가)
// 1페이즈: 앞발 / 덮치기(AoEIndicator 예고) / 창귀 소환
// 2페이즈(체력 50%↓): 즉시 소환 + 소환 간격 단축 + 창귀 삼키기
// 수치·간격은 EnemyData 보스 칸에서 읽음
[RequireComponent(typeof(Enemy))]
public class SangunBossAI : MonoBehaviour
{
    private enum Pattern { Idle, ClawTelegraph, PounceTelegraph, PounceLeap, Summoning, Swallowing }

    private Enemy enemy;
    private EnemyData data;
    private Pattern pattern = Pattern.Idle;
    private float nextClawTime;
    private float nextPounceTime;
    private float nextSummonTime;
    private bool phase2;
    private int swallowStacks;
    private Vector3 pounceTarget;
    private AoEIndicator aoe;
    private readonly List<Enemy> summons = new List<Enemy>();
    private RegionSettings region;
    private Coroutine patternRoutine;
    private bool timersReady; // 깨어난 뒤 EnemyData로 첫 쿨타임만 한 번 잡음
    private float nextBossRepathTime; // SetDestination을 매 프레임 부르면 JobTempAlloc 경고가 남

    private const float BossRepathInterval = 0.25f;

    public IReadOnlyList<Enemy> Summons => summons;
    public bool IsPhase2 => phase2;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        phase2 = false;
        swallowStacks = 0;
        pattern = Pattern.Idle;
        timersReady = false;
        nextClawTime = 0f;
        nextPounceTime = 0f;
        nextSummonTime = 0f;
        nextBossRepathTime = 0f;
        summons.Clear();
    }

    private void OnDisable()
    {
        if (patternRoutine != null) StopCoroutine(patternRoutine);
        patternRoutine = null;
        if (aoe != null)
        {
            aoe.Hide();
            Destroy(aoe.gameObject);
            aoe = null;
        }
    }

    private void Update()
    {
        if (enemy == null || enemy.IsDead || !enemy.UsesBossAi) return;
        if (!enemy.IsAwake) return;

        data = enemy.Data;
        if (data == null) return;

        // 1페이즈: 앞발은 바로, 덮치기·소환은 간격만큼 기다린 뒤 (2페이즈 진입 시 소환은 즉시)
        if (!timersReady)
        {
            nextClawTime = Time.time + 0.3f;
            nextPounceTime = Time.time + Mathf.Min(4f, data.pounceCooldown);
            nextSummonTime = Time.time + data.summonIntervalPhase1;
            timersReady = true;
        }

        PlayerStats player = enemy.PlayerTarget;
        if (player == null || player.IsDead)
        {
            enemy.RequestStopMoving();
            if (aoe != null) aoe.Hide();
            return;
        }

        // 2페이즈 진입
        if (!phase2 && enemy.HealthRatio <= data.phase2HealthRatio)
        {
            EnterPhase2();
        }

        CleanupDeadSummons();

        if (pattern != Pattern.Idle) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // 삼키기 우선 (2페이즈, 스택 여유, 가까운 창귀)
        if (phase2 && swallowStacks < data.swallowMaxStacks && TryFindSwallowTarget(out Enemy prey))
        {
            patternRoutine = StartCoroutine(SwallowRoutine(prey));
            return;
        }

        // 소환
        if (Time.time >= nextSummonTime && CountAliveSummons() < data.summonMaxAlive)
        {
            patternRoutine = StartCoroutine(SummonRoutine());
            return;
        }

        // 덮치기
        if (Time.time >= nextPounceTime && distance > data.clawRange * 0.8f)
        {
            patternRoutine = StartCoroutine(PounceRoutine(player.transform.position));
            return;
        }

        // 앞발 (평타)
        if (Time.time >= nextClawTime)
        {
            if (distance > data.clawRange)
            {
                MoveToward(player.transform.position);
            }
            else
            {
                patternRoutine = StartCoroutine(ClawRoutine(toPlayer));
            }
            return;
        }

        // 대기: 사거리 유지
        if (distance > data.attackRange) MoveToward(player.transform.position);
        else
        {
            enemy.RequestStopMoving();
            enemy.RequestLookAt(toPlayer);
        }
    }

    private void EnterPhase2()
    {
        phase2 = true;
        GameLog.Combat($"{name} 2페이즈 진입!");
        if (BossHealthBarUI.Instance != null) BossHealthBarUI.Instance.SetPhase2(true);
        nextSummonTime = 0f; // 바로 소환 가능
        if (pattern == Pattern.Idle && CountAliveSummons() < data.summonMaxAlive)
        {
            patternRoutine = StartCoroutine(SummonRoutine());
        }
    }

    private IEnumerator ClawRoutine(Vector3 toPlayer)
    {
        pattern = Pattern.ClawTelegraph;
        enemy.RequestStopMoving();
        enemy.RequestLookAt(toPlayer);

        // 예고: 보스 앞쪽에 빨간 원
        EnsureAoe();
        Vector3 telegraphPos = transform.position + transform.forward * (data.clawRange * 0.5f);
        telegraphPos.y = transform.position.y;
        float end = Time.time + data.clawTelegraph;
        while (Time.time < end)
        {
            if (enemy.IsDead) yield break;
            if (aoe != null) aoe.Show(telegraphPos, data.clawRange * 0.55f, false);
            yield return null;
        }
        if (aoe != null) aoe.Hide();

        // 피해: 전방 부채꼴
        if (!enemy.IsDead) ApplyConeDamage(data.clawRange, data.clawArcDegrees, 1f);

        nextClawTime = Time.time + data.attackCooldown;
        pattern = Pattern.Idle;
        patternRoutine = null;
    }

    private IEnumerator PounceRoutine(Vector3 playerPos)
    {
        pattern = Pattern.PounceTelegraph;
        enemy.RequestStopMoving();

        pounceTarget = playerPos;
        pounceTarget.y = transform.position.y;
        EnsureAoe();

        float end = Time.time + data.pounceTelegraph;
        while (Time.time < end)
        {
            if (enemy.IsDead) yield break;
            // 예고 중에도 플레이어 위치를 따라가되, 너무 민감하지 않게 마지막 위치 유지 + 약한 추적
            if (enemy.PlayerTarget != null)
            {
                Vector3 p = enemy.PlayerTarget.transform.position;
                p.y = transform.position.y;
                pounceTarget = Vector3.Lerp(pounceTarget, p, 0.15f);
            }
            if (aoe != null) aoe.Show(pounceTarget, data.pounceRadius, false);
            yield return null;
        }
        if (aoe != null) aoe.Hide();

        // 뛰어들기 (거리만큼 시간을 주고, 잠깐 속도를 올려 착지까지 가게 함)
        pattern = Pattern.PounceLeap;
        NavMeshAgent agent = enemy.Agent;
        float savedSpeed = agent != null ? agent.speed : 0f;
        if (agent != null && agent.isOnNavMesh)
        {
            float leapDist = HorizontalDistance(transform.position, pounceTarget);
            float leapDuration = Mathf.Clamp(leapDist / Mathf.Max(1f, savedSpeed * 1.8f) + 0.25f, 0.5f, 2.5f);
            agent.speed = Mathf.Max(savedSpeed * 2.2f, 8f);
            agent.isStopped = false;
            agent.SetDestination(pounceTarget);
            float leapEnd = Time.time + leapDuration;
            while (Time.time < leapEnd && !enemy.IsDead)
            {
                Vector3 flat = pounceTarget - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude <= 1.0f * 1.0f) break;
                yield return null;
            }
            if (agent != null) agent.speed = savedSpeed;
            enemy.RequestStopMoving();
        }

        // 피해: 예고 원(플레이어가 서 있던 곳)과 실제 착지점 둘 다 판정 (피하면 빗나감)
        if (!enemy.IsDead)
        {
            Vector3 landing = transform.position;
            landing.y = pounceTarget.y;
            ApplyCircleDamage(pounceTarget, data.pounceRadius, data.pounceDamageMult);
            // 착지가 예고와 어긋나도, 보스 발밑 원 안에 있으면 맞음 (중복 피해 방지: 이미 맞았으면 Deal이 한 번만)
            // → 위 호출에서 안 맞았을 때만 착지점으로 한 번 더
            if (enemy.PlayerTarget != null && !enemy.PlayerTarget.IsDead)
            {
                float toTelegraph = HorizontalDistance(pounceTarget, enemy.PlayerTarget.transform.position);
                if (toTelegraph > data.pounceRadius)
                {
                    ApplyCircleDamage(landing, data.pounceRadius, data.pounceDamageMult);
                }
            }
        }

        nextPounceTime = Time.time + data.pounceCooldown;
        pattern = Pattern.Idle;
        patternRoutine = null;
    }

    private IEnumerator SummonRoutine()
    {
        pattern = Pattern.Summoning;
        enemy.RequestStopMoving();

        CleanupDeadSummons();
        int want = data.summonCount;
        int room = Mathf.Max(0, data.summonMaxAlive - CountAliveSummons());
        int canSpawn = Mathf.Min(want, room);
        GameLog.Combat($"{name} 창귀 소환! (목표 {canSpawn}마리, 현재 생존 {CountAliveSummons()})");

        // 실패(풀·NavMesh)해도 목표 수까지 여러 번 시도 — 1마리만 나오는 경우 줄임
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(canSpawn * 3, canSpawn);
        while (spawned < canSpawn && attempts < maxAttempts)
        {
            Enemy e = SpawnSummonAtIndex(spawned);
            if (e != null)
            {
                summons.Add(e);
                spawned++;
            }
            attempts++;
            yield return null;
        }

        if (spawned < canSpawn)
            Debug.LogWarning($"산군 소환: {canSpawn}마리 중 {spawned}마리만 소환됨 (시도 {attempts}회)");

        float interval = phase2 ? data.summonIntervalPhase2 : data.summonIntervalPhase1;
        nextSummonTime = Time.time + interval;
        pattern = Pattern.Idle;
        patternRoutine = null;
    }

    private IEnumerator SwallowRoutine(Enemy prey)
    {
        pattern = Pattern.Swallowing;
        GameLog.Combat($"{name} 창귀 삼키기!");

        // 창귀가 플레이어만 쫓아 영원히 못 먹는 문제 방지: 추격 AI를 끄고 보스 쪽으로 끌어옴
        if (prey != null) prey.SetChasePaused(true);

        NavMeshAgent bossAgent = enemy.Agent;
        float savedBossSpeed = bossAgent != null ? bossAgent.speed : 5f;
        if (bossAgent != null && bossAgent.isOnNavMesh)
            bossAgent.speed = Mathf.Max(savedBossSpeed * 1.6f, 8f);

        NavMeshAgent preyAgent = prey != null ? prey.Agent : null;
        float savedPreySpeed = preyAgent != null ? preyAgent.speed : 0f;
        if (preyAgent != null && preyAgent.isOnNavMesh)
        {
            preyAgent.speed = Mathf.Max(savedPreySpeed, 6f);
            preyAgent.isStopped = false;
        }

        float deadline = Time.time + 5f; // 이 안에 못 닿으면 근처면 강제 삼킴
        bool swallowed = false;
        float nextPullRepath = 0f;

        while (prey != null && !prey.IsDead && !enemy.IsDead && Time.time < deadline)
        {
            float dist = HorizontalDistance(transform.position, prey.transform.position);
            if (dist <= data.swallowRange)
            {
                PerformSwallow(prey);
                swallowed = true;
                break;
            }

            // 길찾기 요청은 간격 두고 (매 프레임 SetDestination → JobTempAlloc 경고)
            if (Time.time >= nextPullRepath)
            {
                nextPullRepath = Time.time + BossRepathInterval;
                if (preyAgent != null && preyAgent.isOnNavMesh)
                    preyAgent.SetDestination(transform.position);
                MoveToward(prey.transform.position, force: true);
            }
            yield return null;
        }

        // 시간이 끝났어도 어느 정도 가까우면 삼킴 (따라잡기 직전에서 실패하지 않게)
        if (!swallowed && prey != null && !prey.IsDead && !enemy.IsDead
            && HorizontalDistance(transform.position, prey.transform.position) <= data.swallowRange * 3f)
        {
            PerformSwallow(prey);
            swallowed = true;
        }

        if (bossAgent != null) bossAgent.speed = savedBossSpeed;
        if (prey != null)
        {
            prey.SetChasePaused(false);
            if (preyAgent != null) preyAgent.speed = savedPreySpeed;
            if (!swallowed) GameLog.Combat($"{name} 삼키기 실패 (창귀가 멀어짐)");
        }

        enemy.RequestStopMoving();
        pattern = Pattern.Idle;
        patternRoutine = null;
    }

    private void PerformSwallow(Enemy prey)
    {
        if (prey == null || prey.IsDead) return;

        enemy.HealPercent(data.swallowHealPercent);
        enemy.AddDamageBonus(data.swallowDamageBonus);
        swallowStacks++;

        // GameLog.Combat은 F1 "전투 로그 표시"를 켜야 Console에 보임 → 화면 알림으로도 확인
        string msg = Loc.Format("boss.swallow", swallowStacks, data.swallowMaxStacks);
        if (string.IsNullOrEmpty(msg) || msg.Contains("No translation"))
            msg = $"창귀 삼킴! ({swallowStacks}/{data.swallowMaxStacks})";
        NotificationUI.Show(msg, NotificationUI.NoticeType.Warning);
        GameLog.Combat($"{name} 창귀 삼킴! 스택 {swallowStacks}/{data.swallowMaxStacks} (체력+{data.swallowHealPercent:0%} · 피해 ×{enemy.DamageBonusMult:0.##})");

        prey.SetChasePaused(false);
        prey.Died -= OnSummonDied;
        prey.Despawn();
        summons.Remove(prey);
    }

    // index로 원형 배치해 서로·보스와 겹치지 않게 소환
    private Enemy SpawnSummonAtIndex(int index)
    {
        if (region == null) region = FindFirstObjectByType<RegionSettings>();
        if (region == null) return null;

        EnemyData summonData = data.summonEnemyData;
        if (summonData == null && EnemyDatabase.Instance != null)
        {
            summonData = EnemyDatabase.Instance.Find("changgwi");
        }
        if (summonData == null)
        {
            Debug.LogWarning("산군: 소환할 창귀 EnemyData를 찾지 못했습니다.");
            return null;
        }

        // 원형으로 배치 (실패 시 랜덤 재시도)
        float angle = (index / Mathf.Max(1f, data.summonCount)) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
        float radius = 3.5f + Random.Range(0f, 1.5f);
        Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            Vector2 offset = Random.insideUnitCircle * 5f;
            pos = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(pos, out hit, 8f, NavMesh.AllAreas))
                return null;
        }
        pos = hit.position;

        Enemy spawned = region.SpawnMonster(summonData, enemy.Level, MonsterRank.Normal, null, pos, noRewards: true);
        if (spawned != null)
        {
            spawned.WakeUp();
            spawned.Died += OnSummonDied;
        }
        return spawned;
    }

    private void OnSummonDied(Enemy e)
    {
        summons.Remove(e);
    }

    // 처치·클리어 때 소환수 정리 (해방)
    public void DespawnAllSummons()
    {
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            if (summons[i] != null && !summons[i].IsDead) summons[i].Despawn();
        }
        summons.Clear();
    }

    private bool TryFindSwallowTarget(out Enemy prey)
    {
        prey = null;
        float best = float.MaxValue;
        foreach (Enemy s in summons)
        {
            if (s == null || s.IsDead) continue;
            float d = HorizontalDistance(transform.position, s.transform.position);
            if (d < best)
            {
                best = d;
                prey = s;
            }
        }
        return prey != null;
    }

    private int CountAliveSummons()
    {
        int n = 0;
        foreach (Enemy s in summons)
        {
            // 풀로 돌아가 꺼진 개체는 살아 있는 소환으로 세지 않음
            if (s != null && !s.IsDead && s.gameObject.activeInHierarchy) n++;
        }
        return n;
    }

    private void CleanupDeadSummons()
    {
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            Enemy s = summons[i];
            if (s == null || s.IsDead || !s.gameObject.activeInHierarchy) summons.RemoveAt(i);
        }
    }

    // force: true면 쿨타임 무시하고 바로 목적지 갱신 (삼키기·덮치기 시작 시)
    private void MoveToward(Vector3 worldPos, bool force = false)
    {
        NavMeshAgent agent = enemy.Agent;
        if (agent == null || !agent.isOnNavMesh) return;
        if (!force && Time.time < nextBossRepathTime) return;

        nextBossRepathTime = Time.time + BossRepathInterval;
        agent.isStopped = false;
        agent.SetDestination(worldPos);
    }

    private void ApplyConeDamage(float range, float arcDegrees, float damageMult)
    {
        PlayerStats player = enemy.PlayerTarget;
        if (player == null || player.IsDead) return;

        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > range) return;

        float angle = Vector3.Angle(transform.forward, to.normalized);
        if (angle <= arcDegrees * 0.5f)
        {
            enemy.DealAttackDamage(damageMult);
        }
    }

    private void ApplyCircleDamage(Vector3 center, float radius, float damageMult)
    {
        PlayerStats player = enemy.PlayerTarget;
        if (player == null || player.IsDead) return;
        if (HorizontalDistance(center, player.transform.position) <= radius)
        {
            enemy.DealAttackDamage(damageMult);
        }
    }

    private void EnsureAoe()
    {
        if (aoe != null) return;

        // 플레이어 부적 폭발 조준원과 겹치지 않도록 보스 전용으로 따로 만듦
        GameObject go = new GameObject("BossAoEIndicator");
        go.AddComponent<LineRenderer>();
        aoe = go.AddComponent<AoEIndicator>();
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
