using System;                    // Action(이벤트) 사용
using TMPro;                     // TextMeshProUGUI 사용
using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent 사용 (사망 시 이동 정지)
using UnityEngine.UI;            // Image 사용

// 플레이어의 능력치(레벨, 경험치, 체력, 기력, 공격력)와 성장(레벨업)을 관리하는 스크립트
// 체력바/기력바/경험치바 UI(Image의 Fill Amount)도 매 프레임 갱신합니다.
public class PlayerStats : MonoBehaviour
{
    [Header("레벨 / 경험치")]
    public int level = 1;               // 현재 레벨
    public float currentXP = 0f;        // 현재 경험치
    public float requiredXP = 100f;     // 다음 레벨까지 필요한 경험치

    [Header("체력 / 공격력")]
    public float maxHP = 100f;          // 최대 체력
    public float currentHP;             // 현재 체력
    public float attackPower = 10f;     // 공격력 (한 번 공격 시 주는 데미지)

    [Header("기력(MP)")]
    public float maxMP = 100f;          // 최대 기력
    public float currentMP;             // 현재 기력

    [Tooltip("1초마다 자연 회복되는 기력")]
    [SerializeField] private float mpRegenPerSecond = 5f;

    [Header("레벨업 시 성장치")]
    [Tooltip("레벨업할 때마다 늘어나는 최대 체력")]
    [SerializeField] private float hpPerLevel = 20f;

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

    private Vector3 spawnPosition;    // 부활할 위치 (게임 시작 위치)
    private Quaternion spawnRotation; // 부활할 때 바라볼 방향
    private float invincibleUntil;    // 무적이 끝나는 시각
    private Renderer[] renderers;     // 무적 중 깜빡일 캐릭터 외형

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
        if (IsDead || currentMP >= maxMP) return;

        currentMP = Mathf.Min(currentMP + mpRegenPerSecond * Time.deltaTime, maxMP);
    }

    // 기력 소모: 충분하면 소모하고 true, 부족하면 false
    public bool UseMP(float amount)
    {
        if (currentMP < amount)
        {
            Debug.Log("기력이 부족합니다!");
            return false;
        }

        currentMP -= amount;
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

    // 레벨업: 레벨 +1, 최대 체력/공격력 증가, 체력/기력 회복, 필요 경험치 증가
    private void LevelUp()
    {
        level++;
        maxHP += hpPerLevel;
        attackPower += attackPerLevel;
        currentHP = maxHP;
        currentMP = maxMP;
        requiredXP *= xpMultiplier;

        Debug.Log($"레벨 업! 현재 레벨: {level}");
    }

    // 세이브 데이터로 능력치를 덮어씀 (사망 상태였다면 부활)
    public void ApplySaveData(SaveData data)
    {
        level = data.level;
        currentXP = data.currentXP;
        maxHP = data.maxHP;
        attackPower = data.attackPower;

        // 예전 형식의 세이브처럼 값이 없으면 0이 들어오므로 기본값으로 보정
        requiredXP = data.requiredXP > 0f ? data.requiredXP : 100f;

        // 사망한 상태로 저장된 경우에도 쓰러진 채로 시작하지 않도록 체력을 채움
        currentHP = data.currentHP > 0f ? Mathf.Min(data.currentHP, maxHP) : maxHP;

        // 기력은 버전 2부터 저장됨. 예전 세이브 파일이면 기력을 가득 채움
        if (data.version >= 2)
        {
            if (data.maxMP > 0f) maxMP = data.maxMP;
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
        if (IsDead || IsInvincible) return;

        currentHP = Mathf.Max(currentHP - damage, 0f);
        Debug.Log($"플레이어 피격! 남은 체력: {currentHP} / {maxHP}");

        if (currentHP <= 0f)
        {
            Die();
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
