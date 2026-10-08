using System.Collections.Generic;

// 세이브 파일(JSON)에 저장할 데이터 묶음
// MonoBehaviour가 아닌 순수 C# 클래스이며, JsonUtility로 변환하려면 [System.Serializable]이 필요합니다.
// JsonUtility는 public 필드만 저장하므로 모든 변수를 public으로 선언합니다.
[System.Serializable]
public class SaveData
{
    // 저장 형식 버전 (1: 기력 없음, 2: 기력 추가)
    public const int CurrentVersion = 2;

    // 이 파일이 어떤 형식으로 저장되었는지 (예전 파일에는 값이 없어서 0으로 읽힘)
    public int version;

    // 플레이어 상태
    public int level;
    public float currentXP;
    public float requiredXP;   // 다음 레벨까지 필요한 경험치 (레벨마다 달라지므로 함께 저장)
    public float maxHP;
    public float currentHP;
    public float attackPower;
    public float maxMP;        // 최대 기력 (버전 2부터)
    public float currentMP;    // 현재 기력 (버전 2부터)

    // 인벤토리 (가방)
    public List<ItemData> inventoryItems = new List<ItemData>();

    // 장착 중인 장비 (장착하지 않았다면 이름이 빈 아이템으로 저장됨)
    public ItemData equippedWeapon;
    public ItemData equippedArmor;
}
