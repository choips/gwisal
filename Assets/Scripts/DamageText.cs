using TMPro;                     // TMP_Text 사용
using UnityEngine;

// 플로팅 데미지 텍스트
// 생성되면 살짝 커졌다 작아지며(팝) 위로 떠오르고, 점점 투명해지다가 스스로 파괴됩니다.
// 3D TextMeshPro와 World Space Canvas의 TextMeshPro - Text (UI) 모두 지원합니다.
public class DamageText : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("1초에 위로 떠오르는 거리")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Tooltip("생성 위치를 좌우로 랜덤하게 흩뜨리는 범위 (여러 숫자가 겹치지 않게)")]
    [SerializeField] private float randomOffsetX = 0.3f;

    [Header("사라지기")]
    [Tooltip("생성 후 완전히 사라질 때까지의 시간(초)")]
    [SerializeField] private float lifeTime = 1.2f;

    [Header("통통 튀는 효과")]
    [Tooltip("처음 나타날 때의 크기 배율 (1보다 크면 커졌다가 원래 크기로 돌아옴)")]
    [SerializeField] private float popScale = 1.6f;

    [Tooltip("원래 크기로 돌아오는 데 걸리는 시간(초)")]
    [SerializeField] private float popDuration = 0.15f;

    private TMP_Text text;          // 숫자를 표시할 텍스트 컴포넌트
    private Camera mainCamera;      // 텍스트가 항상 바라볼 카메라
    private Vector3 originalScale;  // 프리팹에 설정된 원래 크기
    private float elapsed;          // 생성 후 지난 시간

    private void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();
        mainCamera = Camera.main;
        originalScale = transform.localScale;

        // lifeTime초 뒤 스스로 파괴
        Destroy(gameObject, lifeTime);
    }

    // 전달받은 데미지를 텍스트로 표시
    public void Setup(float damageAmount)
    {
        if (text != null)
        {
            // 소수점 없이 정수로 표시
            text.text = Mathf.RoundToInt(damageAmount).ToString();
        }

        transform.position += new Vector3(Random.Range(-randomOffsetX, randomOffsetX), 0f, 0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // 위로 서서히 떠오름 (텍스트가 카메라 쪽으로 기울어 있으므로 월드 기준 위쪽으로 이동)
        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime, Space.World);

        // 처음에 크게 나타났다가 원래 크기로 줄어드는 팝 효과
        float popProgress = popDuration > 0f ? Mathf.Clamp01(elapsed / popDuration) : 1f;
        transform.localScale = originalScale * Mathf.Lerp(popScale, 1f, popProgress);

        // 시간이 지날수록 점점 투명해짐 (1 → 0)
        if (text != null)
        {
            text.alpha = 1f - Mathf.Clamp01(elapsed / lifeTime);
        }
    }

    // 카메라 이동이 끝난 뒤 회전을 맞춰야 떨림 없이 항상 정면으로 보임
    private void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
