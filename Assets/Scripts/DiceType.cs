using UnityEngine;

public enum ElementType
{
    Normal,
    Earth,
    Fire,
    Water,
    Wind
}

[System.Serializable]
public class DiceTypeData
{
    public ElementType type;
    public GameObject auraPrefab;
    [Range(0f, 100f)]
    public float probability = 20f;
}

public class DiceType : MonoBehaviour
{
    [Header("타입 데이터베이스")]
    public DiceTypeData[] typeDatabase = new DiceTypeData[]
    {
        new DiceTypeData { type = ElementType.Earth, auraPrefab = null, probability = 15f },
        new DiceTypeData { type = ElementType.Fire, auraPrefab = null, probability = 15f },
        new DiceTypeData { type = ElementType.Water, auraPrefab = null, probability = 15f },
        new DiceTypeData { type = ElementType.Wind, auraPrefab = null, probability = 15f },
    };

    [Header("Aura 오프셋")]
    public Vector3 auraOffset = Vector3.zero;

    private Role role;
    private Choice choice;
    private ElementType[] diceTypes;
    private GameObject[] auraObjects;
    private bool wasRolling;

    void Start()
    {
        role = FindObjectOfType<Role>();
        choice = FindObjectOfType<Choice>();

        if (role != null)
        {
            diceTypes = new ElementType[role.diceCount];
            auraObjects = new GameObject[role.diceCount];

            // 게임 시작 시 모든 주사위 Normal로 초기화, aura 없음
            for (int i = 0; i < diceTypes.Length; i++)
                diceTypes[i] = ElementType.Normal;
        }
    }

    void Update()
    {
        if (role == null) return;

        // 롤 시작 시 모든 aura 즉시 제거 + 타입 Normal로 리셋
        if (!wasRolling && role.IsRolling)
        {
            ClearAllAuras();
            ResetAllTypes();
        }

        if (wasRolling && !role.IsRolling)
        {
            AssignRandomTypes();
        }
        wasRolling = role.IsRolling;

        UpdateAuraPositions();
    }

    void ResetAllTypes()
    {
        if (diceTypes == null) return;

        for (int i = 0; i < diceTypes.Length; i++)
        {
            if (choice != null && choice.IsDiceLocked(i)) continue;
            diceTypes[i] = ElementType.Normal;
        }
    }

    void AssignRandomTypes()
    {
        if (role.DiceObjects == null) return;

        for (int i = 0; i < diceTypes.Length; i++)
        {
            // lock된 주사위는 타입/aura 유지
            if (choice != null && choice.IsDiceLocked(i)) continue;

            // 기존 aura 제거
            ClearAura(i);

            // 확률 기반 랜덤 타입 지정
            diceTypes[i] = GetRandomType();

            // aura 생성
            SpawnAura(i);
        }
    }

    ElementType GetRandomType()
    {
        // 각 원소의 확률로 개별 판정, 해당 안 되면 Normal
        // 100 기준으로 랜덤 판정
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        for (int i = 0; i < typeDatabase.Length; i++)
        {
            cumulative += typeDatabase[i].probability;
            if (roll < cumulative)
                return typeDatabase[i].type;
        }

        return ElementType.Normal;
    }

    void SpawnAura(int index)
    {
        if (role.DiceObjects == null || role.DiceObjects[index] == null) return;

        DiceTypeData data = GetTypeData(diceTypes[index]);
        if (data == null || data.auraPrefab == null) return;

        Vector3 pos = role.DiceObjects[index].transform.position + auraOffset;
        auraObjects[index] = Instantiate(data.auraPrefab, pos, Quaternion.identity);
    }

    void ClearAllAuras()
    {
        if (auraObjects == null) return;

        for (int i = 0; i < auraObjects.Length; i++)
        {
            if (choice != null && choice.IsDiceLocked(i)) continue;
            ClearAura(i);
        }
    }

    void ClearAura(int index)
    {
        if (auraObjects[index] != null)
        {
            Destroy(auraObjects[index]);
            auraObjects[index] = null;
        }
    }

    void UpdateAuraPositions()
    {
        if (auraObjects == null || role.DiceObjects == null) return;

        for (int i = 0; i < auraObjects.Length; i++)
        {
            if (auraObjects[i] == null || role.DiceObjects[i] == null) continue;
            auraObjects[i].transform.position = role.DiceObjects[i].transform.position + auraOffset;
            auraObjects[i].transform.rotation = Quaternion.identity;
        }
    }

    DiceTypeData GetTypeData(ElementType type)
    {
        for (int i = 0; i < typeDatabase.Length; i++)
        {
            if (typeDatabase[i].type == type)
                return typeDatabase[i];
        }
        return null;
    }

    /// <summary>
    /// 특정 주사위의 현재 타입 반환
    /// </summary>
    public ElementType GetDiceType(int index)
    {
        if (diceTypes == null || index < 0 || index >= diceTypes.Length)
            return ElementType.Normal;
        return diceTypes[index];
    }
}
