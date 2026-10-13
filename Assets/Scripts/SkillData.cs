using UnityEngine;

// 스킬 하나의 데이터 (ScriptableObject — Project 창에 파일로 저장되는 데이터 묶음)
// 만드는 법: Project 창에서 우클릭 → Create → 귀살 → Skill Data
// 스킬의 "숫자와 연결"(쿨타임, 기력 소모, 피해 배율, 프리팹)은 여기에, "동작"은 스킬별 스크립트(SkillBase를 상속)에 둡니다.
// 같은 동작에 숫자만 다른 스킬은 이 파일만 하나 더 만들면 됩니다.
[CreateAssetMenu(fileName = "SkillData_New", menuName = "귀살/Skill Data", order = 0)]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("영문 ID (세이브·스킬 트리에서 이 스킬을 가리키는 이름, 예: amulet_projectile). 한 번 정하면 바꾸지 않습니다.")]
    public string skillId;

    [Tooltip("화면에 보이는 이름 (예: 부적 투사체)")]
    public string displayName;

    [Tooltip("스킬바·스킬 창에 표시할 아이콘 (비워 두어도 됨)")]
    public Sprite icon;

    [Tooltip("스킬 설명")]
    [TextArea(2, 4)] public string description;

    [Header("사용 조건")]
    [Tooltip("다시 쓸 수 있을 때까지의 대기 시간(초, 0이면 쿨타임 없음)")]
    [Min(0f)] public float cooldown = 1f;

    [Tooltip("한 번 쓸 때 소모하는 기력(MP) (0이면 기력을 쓰지 않음)")]
    [Min(0f)] public float manaCost = 0f;

    [Header("피해")]
    [Tooltip("무기 피해에 곱해지는 배율 (2 = 무기 피해의 200%)")]
    [Min(0f)] public float damageMultiplier = 2f;

    [Header("생성할 오브젝트")]
    [Tooltip("스킬을 쓰면 생성할 프리팹 (부적 투사체 = Amulet, 부적 폭발 = AoEExplosion)")]
    public GameObject prefab;
}
