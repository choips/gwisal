using TMPro;
using UnityEngine;

// 바닥에 떨어진 아이템 위에 이름을 등급 색으로 띄우는 이름표 (디아블로 방식)
// LootDropper가 아이템을 떨어뜨릴 때 자동으로 붙이므로 직접 추가할 필요가 없습니다.
// 이름표는 아이템의 자식이 아닌 별도 오브젝트라서, 아이템이 굴러도 기울어지지 않고 항상 카메라를 향합니다.
// 아이템을 주워서 아이템이 사라지면 이름표도 함께 사라집니다.
public class ItemNameLabel : MonoBehaviour
{
    private Transform labelTransform; // 이름표 오브젝트
    private Camera mainCamera;        // 이름표가 바라볼 카메라
    private float heightOffset;       // 아이템 중심에서 이름표까지의 높이

    // 이름표 만들기 (LootDropper가 아이템 생성 직후 호출)
    // text: 표시할 이름, color: 등급 색, font: 한글 글꼴, fontSize: 글자 크기, height: 아이템 위 높이
    public void Setup(string text, Color color, TMP_FontAsset font, float fontSize, float height)
    {
        mainCamera = Camera.main;
        heightOffset = height;

        GameObject labelObject = new GameObject($"{name} 이름표");

        // UI가 아닌 3D 공간용 TextMeshPro (캔버스 없이 월드에 바로 그려짐, 콜라이더가 없어 클릭을 막지 않음)
        // TextMeshPro를 붙이면 Transform이 RectTransform으로 바뀌므로, 위치 참조는 반드시 붙인 뒤에 가져옴
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        labelTransform = label.transform;
        label.font = font;
        label.text = text;
        label.color = color;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; // 이름이 길어도 한 줄로
        label.rectTransform.sizeDelta = new Vector2(10f, 2f);

        // 밝은 바닥에서도 잘 보이도록 검은 테두리
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;

        UpdateLabelTransform();
    }

    private void LateUpdate()
    {
        UpdateLabelTransform();
    }

    // 이름표를 아이템 바로 위에 두고 카메라와 같은 방향을 보게 함 (항상 정면으로 읽힘)
    private void UpdateLabelTransform()
    {
        if (labelTransform == null) return;

        labelTransform.position = transform.position + Vector3.up * heightOffset;

        if (mainCamera != null)
        {
            labelTransform.rotation = mainCamera.transform.rotation;
        }
    }

    // 아이템을 줍거나 씬이 바뀌어 아이템이 사라지면 이름표도 제거
    private void OnDestroy()
    {
        if (labelTransform != null)
        {
            Destroy(labelTransform.gameObject);
        }
    }
}
