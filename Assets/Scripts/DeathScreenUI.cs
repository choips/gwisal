using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 플레이어가 사망하면 "YOU DIED" 화면을 서서히 띄우고,
// 부활 버튼을 누르면 시작 위치에서 부활시킵니다.
// 패널을 끄지(SetActive) 않고 CanvasGroup으로 투명하게 숨기므로, 이 스크립트는 사망 패널 자체에 붙입니다.
[RequireComponent(typeof(CanvasGroup))]
public class DeathScreenUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 PlayerStats를 자동으로 찾습니다")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("부활 버튼")]
    [SerializeField] private Button reviveButton;

    [Header("연출")]
    [Tooltip("사망 후 화면이 나타나기 시작할 때까지의 시간(초)")]
    [SerializeField] private float showDelay = 1f;

    [Tooltip("화면이 완전히 나타날 때까지 걸리는 시간(초)")]
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup canvasGroup; // 패널 전체의 투명도와 클릭 가능 여부를 한 번에 조절
    private Coroutine showRoutine;   // 진행 중인 나타나기 연출

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (reviveButton != null)
        {
            reviveButton.onClick.AddListener(OnReviveClicked);
        }

        HideImmediate();
    }

    private void OnEnable()
    {
        if (playerStats == null) return;
        playerStats.Died += Show;
        playerStats.Revived += HideImmediate;
    }

    private void OnDisable()
    {
        if (playerStats == null) return;
        playerStats.Died -= Show;
        playerStats.Revived -= HideImmediate;
    }

    private void Show()
    {
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return new WaitForSeconds(showDelay);

        // 화면이 나타나는 동안 뒤쪽 UI(인벤토리 등)가 눌리지 않도록 클릭을 막음
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true; // 완전히 나타난 뒤에만 부활 버튼을 누를 수 있음
        showRoutine = null;
    }

    // 부활 성공 시 PlayerStats의 Revived 이벤트로 HideImmediate가 호출됨
    private void OnReviveClicked()
    {
        if (playerStats != null)
        {
            playerStats.Respawn();
        }
    }

    // 투명하게 만들고 클릭도 통과시켜, 화면에 없는 것처럼 만듦
    private void HideImmediate()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
