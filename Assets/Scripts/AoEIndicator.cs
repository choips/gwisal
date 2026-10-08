using UnityEngine;

// 부적 폭발(Amulet Explosion) 조준 원
// Q 키를 누르고 있는 동안 마우스가 가리키는 바닥에 폭발 범위를 원으로 미리 보여 줍니다.
// 원은 LineRenderer로 코드에서 직접 그리므로 따로 모델·이미지·재질을 만들 필요가 없습니다.
// 사용 가능하면 금색, 기력(MP) 부족이나 쿨타임 중이면 빨간색으로 표시됩니다.
[RequireComponent(typeof(LineRenderer))]
public class AoEIndicator : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("원을 이루는 점의 개수 (많을수록 매끄러움)")]
    [SerializeField] private int segments = 64;

    [Tooltip("원 테두리 선의 두께")]
    [SerializeField] private float lineWidth = 0.08f;

    [Tooltip("바닥과 겹쳐 깜빡이지 않도록 바닥에서 띄우는 높이")]
    [SerializeField] private float heightOffset = 0.05f;

    [Header("색")]
    [Tooltip("스킬을 쓸 수 있을 때의 색")]
    [SerializeField] private Color readyColor = new Color(1f, 0.85f, 0.2f, 0.9f); // 금색

    [Tooltip("기력이 부족하거나 쿨타임 중일 때의 색")]
    [SerializeField] private Color notReadyColor = new Color(1f, 0.25f, 0.25f, 0.9f); // 빨간색

    [Header("연출")]
    [Tooltip("원이 숨 쉬듯 커졌다 작아지는 정도 (0.03 = 3%, 0이면 끔)")]
    [SerializeField] private float pulseAmount = 0.03f;

    [Tooltip("숨 쉬는 속도")]
    [SerializeField] private float pulseSpeed = 6f;

    private LineRenderer line;    // 원을 그리는 선
    private Vector3[] unitCircle; // 반경 1짜리 원의 점들 (매번 다시 계산하지 않도록 저장)
    private Vector3[] points;     // 실제 반경을 곱해 선에 넘겨줄 점들

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        SetupLine();

        // 바닥과 평행하게 눕힘 (선이 로컬 XY 평면에 그려지고, X축으로 90도 돌리면 XY 평면이 바닥이 됨)
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        BuildCircle();
        Hide();
    }

    // LineRenderer 기본 설정 (Inspector에서 일일이 바꾸지 않아도 되도록 코드에서 지정)
    private void SetupLine()
    {
        line.useWorldSpace = false;                    // 오브젝트를 옮기면 원도 함께 이동
        line.loop = true;                              // 마지막 점과 첫 점을 이어 닫힌 원으로 만듦
        line.alignment = LineAlignment.TransformZ;     // 카메라가 아니라 바닥을 향해 납작하게 그림
        line.widthMultiplier = lineWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // Sprites/Default는 정점 색과 투명도를 그대로 보여 주고, 빌드에도 항상 포함되는 셰이더
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            line.material = new Material(shader);
        }
    }

    // 반경 1짜리 원의 점들을 미리 만들어 둠 (로컬 XY 평면 = 눕힌 뒤의 바닥 평면)
    private void BuildCircle()
    {
        int count = Mathf.Max(segments, 8);
        unitCircle = new Vector3[count];
        points = new Vector3[count];
        line.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count; // 라디안 각도
            unitCircle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
    }

    // 바닥 위치에 원을 띄움 (조준하는 동안 PlayerController가 매 프레임 호출)
    // groundPoint: 마우스가 가리키는 바닥 좌표, radius: 폭발 반경, isReady: 지금 쓸 수 있는지
    public void Show(Vector3 groundPoint, float radius, bool isReady)
    {
        transform.position = groundPoint + Vector3.up * heightOffset;

        // 반경에 맞게 점을 펼치고, 살짝 커졌다 작아지는 연출을 더함
        // (오브젝트 크기(Scale)를 바꾸지 않고 점 위치를 직접 바꿔서 선 두께가 변하지 않게 함)
        float currentRadius = radius * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = unitCircle[i] * currentRadius;
        }
        line.SetPositions(points);

        Color color = isReady ? readyColor : notReadyColor;
        line.startColor = color;
        line.endColor = color;

        line.enabled = true;
    }

    // 원 숨기기
    public void Hide()
    {
        if (line != null) line.enabled = false;
    }
}
