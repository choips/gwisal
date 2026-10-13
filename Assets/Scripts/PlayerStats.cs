using System;                    // Action(이벤트) 사용
using TMPro;                     // TextMeshProUGUI 사용
using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent 사용 (사망 시 이동 정지)
using UnityEngine.UI;            // Image 사용

// 플레이어의 능력치(레벨, 경험치, 체력, 기력, 공격력)와 성장(레벨업)을 관리하는 스크립트
// 최대 체력·최대 기력·기력 회복은 능력치 계산기(Stats: BalanceConfig의 레벨 기본값 + 장비 수정치)에서 받아 옵니다.
// 체력바/기력바/경험치바 UI(Image의 Fill Amount)도 매 프레임 갱신합니다.
public class PlayerStats : MonoBehaviour
{
    [Header("레벨 / 경험치")]
    public int level = 1;               // 현재 레벨
    public float currentXP = 0f;        // 현재 경험치
    public float requiredXP = 100f;     // 다음 레벨까지 필요한 경험치

    [Header("체력 / 공격력")]
    [Tooltip("최대 체력 — 플레이 중에는 레벨·장비로 자동 계산됨 (BalanceConfig에서 조정, 여기서 바꿔도 덮어씀)")]
    public float maxHP = 100f;          // 최대 체력
    public float currentHP;             // 현재 체력
    public float attackPower = 10f;     // 공격력 (한 번 공격 시 주는 데미지)

    [Header("치명타(Critical)")]
    [Tooltip("치명타가 터질 확률 (0.1 = 10%)")]
    [Range(0f, 1f)] public float critChance = 0.1f;   // 치명타 확률

    [Tooltip("치명타일 때 데미지에 곱해지는 배율 (2 = 2배)")]
    public float critMultiplier = 2f;                 // 치명타 배율

    [Header("기력(MP)")]
    [Tooltip("최대 기력 — 플레이 중에는 레벨·장비로 자동 계산됨 (BalanceConfig에서 조정, 여기서 바꿔도 덮어씀)")]
    public float maxMP = 100f;          // 최대 기력
    public float currentMP;             // 현재 기력

    [Header("레벨업 시 성장치 (최대 체력·기력은 BalanceConfig)")]
    [Tooltip("레벨업할 때마다 늘어나는 공격력")]
    [SerializeField] private float attackPerLevel = 5f;

    [Tooltip("레벨업할 때마다 필요 경험치에 곱해지는 배율 (1.5 = 1.5배)")]
    [SerializeField] private float xpMultiplier = 1.5f;

    [Header("UI 연결")]
    [Tooltip("체력바의 채워지는 이미지 (Image Type: Filled)")]
    [SerializeField] private Image hpBarFill;

    [Tooltip("기력바의 채워지는 이미지 (Image Type: Filled)")]
    [SerializeField] private Image mpBarFill;

    [Tooltip("경험치바의 채워지는 이미지 (Image Type: Filled)")]
    [SerializeField] private Image xpBarFill;

    [Tooltip("(선택) 레벨을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("바가 목표 길이로 부드럽게 따라가는 속도 (0이면 즉시 변경)")]
    [SerializeField] private float barSmoothSpeed = 10f;

    [Header("피격 데미지 숫자")]
    [Tooltip("맞았을 때 머리 위에 띄울 데미지 숫자 프리팹 (적과 같은 DamageText 프리팹)")]
    [SerializeField] private GameObject damageTextPrefab;

    [Tooltip("플레이어 중심에서 숫자가 나타날 높이")]
    [SerializeField] private float damageTextHeight = 1.5f;

    [Tooltip("플레이어가 받은 데미지 숫자의 색 (치명타 빨강과 구분)")]
    [SerializeField] private Color damageTextColor = new Color(0.75f, 0.35f, 1f); // 보라색

    [Header("능력치 계산 (새 구조, S1-6 전환 중)")]
    [Tooltip("밸런스 숫자 모음 파일 (Project 창의 BalanceConfig를 끌어다 놓기, 비워 두면 코드의 기본 숫자 사용)")]
    [SerializeField] private BalanceConfig balanceConfig;

    [Header("부활")]
    [Tooltip("부활 직후 피해를 받지 않는 시간(초)")]
    [SerializeField] private float respawnInvincibleTime = 2f; // 부활 후 무적 시간

    [Tooltip("무적 중 캐릭터가 깜빡이는 간격(초)")]
    [SerializeField] private float blinkInterval = 0.1f;

    // 플레이어가 사망했는지 여부 (적 AI가 공격을 멈출 때 확인)
    public bool IsDead { get; private set; }

    // 부활 직후 무적 상태인지 여부
    public bool IsInvincible => Time.time < invincibleUntil;

    // 사망/부활 순간을 다른 스크립트(사망 화면 UI 등)에 알리는 이벤트
    public event Action Died;
    public event Action Revived;

    // 새 능력치 계산기 (레벨 기본값 + 장비 수정치 → 최종값)
    // S1-6 2단계부터 최대 체력·최대 기력·기력 회복은 여기서 옴 (공격은 3단계에서 전환)
    public CharacterStats Stats { get; private set; }

    private float manaRegenPerSecond; // 초당 기력 회복 (능력치 계산기에서 받아 둔 값)

    private Vector3 spawnPosition;    // 부활할 위치 (게임 시작 위치)
    private Quaternion spawnRotation; // 부활할 때 바라볼 방향
    private float invincibleUntil;    // 무적이 끝나는 시각
    private Renderer[] renderers;     // 무적 중 깜빡일 캐릭터 외형

    private void Awake()
    {
        if (balanceConfig == null)
        {
            Debug.LogWarning("PlayerStats: Balance Config가 연결되지 않아 코드의 기본 숫자를 사용합니다. Player의 Player Stats에 BalanceConfig 파일을 끌어다 놓으세요.");
            balanceConfig = ScriptableObject.CreateInstance<BalanceConfig>();
        }

        Stats = new CharacterStats(balanceConfig);
        ApplyLevelToStats();
    }

    // 현재 레벨에 맞는 기본값(체력·기력 등)을 능력치 계산기에 넣음 (레벨이 바뀔 때마다 호출)
    private void ApplyLevelToStats()
    {
        if (Stats == null) return;

        balanceConfig.ApplyPlayerBase(Stats, level);
        SyncFromStats();
    }

    // 장비가 주는 능력치 추가 (InventoryManager가 장착할 때 호출, item = 출처)
    public void AddEquipmentBonus(ItemData item, StatType stat, float value)
    {
        if (Stats == null || item == null) return;

        Stats.AddModifier(new StatModifier(stat, StatModifierType.Flat, value, item));
        SyncFromStats();
    }

    // 장비가 주던 능력치를 모두 지움 (InventoryManager가 벗을 때 호출)
    public void RemoveEquipmentBonus(ItemData item)
    {
        if (Stats == null || item == null) return;

        if (Stats.RemoveModifiersFromSource(item))
        {
            SyncFromStats();
        }
    }

    // 능력치 계산기의 최종값을 최대 체력·최대 기력·기력 회복에 반영
    // 최대치가 늘거나 줄면 현재 체력·기력도 같은 만큼 늘리거나 줄임 (장비를 벗었다 다시 껴서 체력을 채우는 것 방지)
    // 살아 있으면 현재 체력은 최소 1 (장비를 벗다가 죽지 않도록)
    private void SyncFromStats()
    {
        Stats.Refresh();

        float newMaxHP = Stats.Get(StatType.MaxHealth);
        float newMaxMP = Stats.Get(StatType.MaxMana);

        currentHP += newMaxHP - maxHP;
        currentMP += newMaxMP - maxMP;

        maxHP = newMaxHP;
        maxMP = newMaxMP;
        manaRegenPerSecond = Stats.Get(StatType.ManaRegen);

        currentHP = Mathf.Clamp(currentHP, IsDead ? 0f : 1f, maxHP);
        currentMP = Mathf.Clamp(currentMP, 0f, maxMP);
    }

    private void Start()
    {
        // 시작 시 체력과 기력을 최대치로 채움
        currentHP = maxHP;
        currentMP = maxMP;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        renderers = GetComponentsInChildren<Renderer>();

        // 첫 화면에서는 바가 차오르는 연출 없이 바로 맞춰 둠
        if (hpBarFill != null) hpBarFill.fillAmount = GetHPRatio();
        if (mpBarFill != null) mpBarFill.fillAmount = GetMPRatio();
        if (xpBarFill != null) xpBarFill.fillAmount = GetXPRatio();
    }

    private void Update()
    {
        RegenerateMP();
        UpdateBlink();
        UpdateUI();
    }

    // 무적 중에는 일정 간격으로 보였다 안 보였다 하고, 끝나면 다시 보이게 함
    private void UpdateBlink()
    {
        if (renderers == null) return;

        bool visible = !IsInvincible || blinkInterval <= 0f
            || Mathf.FloorToInt(Time.time / blinkInterval) % 2 == 0;

        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    // 시간에 따라 기력을 서서히 회복 (최대치를 넘지 않음)
    private void RegenerateMP()
    {
        if (IsDead) return;

        // 개발자 메뉴의 기력 무한: 항상 가득 채움
        if (DevCheats.InfiniteMana)
        {
            currentMP = maxMP;
            return;
        }

        if (currentMP >= maxMP) return;

        currentMP = Mathf.Min(currentMP + manaRegenPerSecond * Time.deltaTime, maxMP);
    }

    // 치명타 판정: 확률에 당첨되면 배율만큼 커진 데미지를 돌려주고 isCritical = true
    // (근접 공격, 부적, 폭발 모두 이 함수로 최종 데미지를 정함)
    public float RollDamage(float baseDamage, out bool isCritical)
    {
        // System.Random과 이름이 겹치므로 UnityEngine.Random을 명시
        isCritical = UnityEngine.Random.value < critChance;
        return isCritical ? baseDamage * critMultiplier : baseDamage;
    }

    // 기력 소모: 충분하면 소모하고 true, 부족하면 false
    public bool UseMP(float amount)
    {
        if (DevCheats.InfiniteMana) return true; // 개발자 메뉴의 기력 무한: 소모하지 않음

        if (currentMP < amount)
        {
            NotificationUI.Show("기력이 부족합니다!", NotificationUI.NoticeType.Warning);
            return false;
        }

        currentMP -= amount;
        return true;
    }

    // 기력 회복 (기력 물약 등): 회복했으면 true, 이미 가득 찼거나 사망 중이면 false (물약을 아끼기 위함)
    public bool RestoreMP(float amount)
    {
        if (IsDead || amount <= 0f) return false;

        if (currentMP >= maxMP)
        {
            NotificationUI.Show("기력이 이미 가득 차 있습니다.");
            return false;
        }

        currentMP = Mathf.Min(currentMP + amount, maxMP);
        return true;
    }

    // 경험치를 얻고, 필요 경험치를 넘으면 레벨업
    public void AddExperience(float amount)
    {
        currentXP += amount;
        Debug.Log($"경험치 +{amount} ({currentXP} / {requiredXP})");

        // 한 번에 많은 경험치를 얻으면 여러 레벨이 오를 수 있으므로 while로 반복
        while (currentXP >= requiredXP)
        {
            // 남는 경험치는 다음 레벨로 이월
            currentXP -= requiredXP;
            LevelUp();
        }
    }

    // 개발자 메뉴: 경험치를 모으지 않고 바로 count만큼 레벨업 (보통 레벨업과 똑같이 성장, 모아 둔 경험치는 0으로)
    public void DevLevelUp(int count)
    {
        if (IsDead || count <= 0) return;

        currentXP = 0f;
        for (int i = 0; i < count; i++)
        {
            LevelUp();
        }
    }

    // 레벨업: 레벨 +1, 최대 체력·기력 증가(BalanceConfig) / 공격력 증가, 체력/기력 회복, 필요 경험치 증가
    private void LevelUp()
    {
        level++;
        ApplyLevelToStats();
        attackPower += attackPerLevel;
        currentHP = maxHP;
        currentMP = maxMP;
        requiredXP *= xpMultiplier;

        NotificationUI.Show($"레벨 업! (레벨 {level})", NotificationUI.NoticeType.Success);
    }

    // 세이브 데이터로 능력치를 덮어씀 (사망 상태였다면 부활)
    // 장비는 이보다 먼저 복원되어 있어야 함 (최대 체력·기력이 장비를 포함해 정해진 뒤 현재 체력·기력을 넣기 위해 — GameController 참고)
    // 저장된 최대 체력·최대 기력은 쓰지 않고 레벨·장비로 다시 계산 (세이브 형식은 그대로)
    public void ApplySaveData(SaveData data)
    {
        level = data.level;
        ApplyLevelToStats();
        currentXP = data.currentXP;
        attackPower = data.attackPower;

        // 예전 형식의 세이브처럼 값이 없으면 0이 들어오므로 기본값으로 보정
        requiredXP = data.requiredXP > 0f ? data.requiredXP : 100f;

        // 사망한 상태로 저장된 경우에도 쓰러진 채로 시작하지 않도록 체력을 채움
        currentHP = data.currentHP > 0f ? Mathf.Min(data.currentHP, maxHP) : maxHP;

        // 기력은 버전 2부터 저장됨. 예전 세이브 파일이면 기력을 가득 채움
        if (data.version >= 2)
        {
            currentMP = Mathf.Clamp(data.currentMP, 0f, maxMP);
        }
        else
        {
            currentMP = maxMP;
        }

        if (IsDead)
        {
            Revive();
        }
    }

    // 사망 상태 해제: 조작과 이동을 다시 가능하게 함
    private void Revive()
    {
        IsDead = false;

        if (TryGetComponent(out PlayerController controller))
        {
            controller.enabled = true;
        }

        if (TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        Revived?.Invoke();
    }

    // 부활 버튼: 시작 위치로 돌아가 체력/기력을 가득 채우고 잠시 무적
    public void Respawn()
    {
        if (!IsDead) return;

        // NavMeshAgent가 붙은 오브젝트는 transform.position 대신 Warp로 순간이동해야 함
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.Warp(spawnPosition);
        }
        else
        {
            transform.position = spawnPosition;
        }
        transform.rotation = spawnRotation;

        currentHP = maxHP;
        currentMP = maxMP;
        invincibleUntil = Time.time + respawnInvincibleTime;

        Revive();
        Debug.Log("부활했습니다!");
    }

    // 적의 공격을 받아 체력 감소 (체력바는 Update의 UpdateUI에서 자동 갱신)
    public void TakeDamage(float damage)
    {
        if (IsDead || IsInvincible || DevCheats.GodMode) return; // 개발자 메뉴의 무적이면 피해 없음

        currentHP = Mathf.Max(currentHP - damage, 0f);
        Debug.Log($"플레이어 피격! 남은 체력: {currentHP} / {maxHP}");

        // 마지막 일격에도 숫자가 보이도록 사망 처리보다 먼저 생성
        ShowDamageText(damage);

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    // 머리 위에 받은 데미지 숫자 띄우기
    private void ShowDamageText(float damage)
    {
        if (damageTextPrefab == null) return;

        Vector3 spawnPosition = transform.position + Vector3.up * damageTextHeight;
        GameObject textObject = ObjectPoolManager.Spawn(damageTextPrefab, spawnPosition, Quaternion.identity);

        if (textObject.TryGetComponent(out DamageText damageText))
        {
            damageText.Setup(damage);
            damageText.SetColor(damageTextColor);
        }
    }

    // 플레이어 사망 처리: 조작과 이동을 멈추고 사망 화면 등에 알림
    private void Die()
    {
        IsDead = true;
        Debug.Log("플레이어 사망!");

        if (TryGetComponent(out PlayerController controller))
        {
            controller.enabled = false;
        }

        if (TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        Died?.Invoke();
    }

    // 체력바, 기력바, 경험치바, 레벨 텍스트를 현재 수치에 맞게 갱신
    private void UpdateUI()
    {
        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = SmoothFill(hpBarFill.fillAmount, GetHPRatio());
        }

        if (mpBarFill != null)
        {
            mpBarFill.fillAmount = SmoothFill(mpBarFill.fillAmount, GetMPRatio());
        }

        if (xpBarFill != null)
        {
            xpBarFill.fillAmount = SmoothFill(xpBarFill.fillAmount, GetXPRatio());
        }

        if (levelText != null)
        {
            levelText.text = $"Lv. {level}";
        }
    }

    // 현재 바 길이에서 목표 길이로 조금씩 다가감
    private float SmoothFill(float current, float target)
    {
        if (barSmoothSpeed <= 0f) return target;
        return Mathf.Lerp(current, target, barSmoothSpeed * Time.deltaTime);
    }

    // 체력 비율 (0 ~ 1)
    private float GetHPRatio()
    {
        return maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
    }

    // 기력 비율 (0 ~ 1)
    private float GetMPRatio()
    {
        return maxMP > 0f ? Mathf.Clamp01(currentMP / maxMP) : 0f;
    }

    // 경험치 비율 (0 ~ 1)
    private float GetXPRatio()
    {
        return requiredXP > 0f ? Mathf.Clamp01(currentXP / requiredXP) : 0f;
    }
}
