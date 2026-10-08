using UnityEngine;
using UnityEngine.UI;

// 적 머리 위에 떠 있는 체력바 (World Space Canvas에 붙임)
// 적이 어느 방향을 보든 항상 카메라를 향하고, 체력이 줄면 바가 부드럽게 줄어듭니다.
// 기본적으로 체력이 가득 찬 적은 바를 숨기고, 한 대라도 맞으면 보여줍니다.
[RequireComponent(typeof(Canvas))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("체력만큼 채워지는 이미지 (Image Type: Filled, Fill Method: Horizontal)")]
    [SerializeField] private Image fillImage;

    [Tooltip("비워두면 부모 오브젝트에서 Enemy를 자동으로 찾습니다")]
    [SerializeField] private Enemy enemy;

    [Header("표시 설정")]
    [Tooltip("체크하면 체력이 가득 찬 동안에는 체력바를 숨깁니다")]
    [SerializeField] private bool hideWhenFull = true;

    [Tooltip("바가 목표 길이로 따라가는 속도 (0이면 즉시 변경)")]
    [SerializeField] private float smoothSpeed = 10f;

    private Canvas canvas;          // 켜고 끄는 것으로 체력바를 숨김
    private Transform cameraTransform;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();

        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy>();
        }

        if (enemy == null)
        {
            Debug.LogWarning($"{name}: 부모에서 Enemy를 찾지 못해 체력바가 동작하지 않습니다.");
            enabled = false;
            return;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = enemy.HealthRatio;
        }
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        float ratio = enemy.HealthRatio;

        canvas.enabled = !hideWhenFull || ratio < 1f;

        if (fillImage != null)
        {
            fillImage.fillAmount = smoothSpeed > 0f
                ? Mathf.Lerp(fillImage.fillAmount, ratio, smoothSpeed * Time.deltaTime)
                : ratio;
        }
    }

    // 적이 회전한 뒤에 카메라와 같은 방향을 보게 해서, 항상 정면으로 보이게 함
    private void LateUpdate()
    {
        if (cameraTransform != null)
        {
            transform.rotation = cameraTransform.rotation;
        }
    }
}
