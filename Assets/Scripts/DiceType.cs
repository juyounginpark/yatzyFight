using UnityEngine;

public enum ElementType
{
    Standard,
    Earth,
    Fire,
    Water,
    Wind,
    Fired
}

[System.Serializable]
public class DiceTypeData
{
    public ElementType type;
    public GameObject auraPrefab;
    [Range(0f, 100f)]
    public float probability = 20f;
    public bool useProbability = true;
}

[DefaultExecutionOrder(-10)]
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

    [Header("Fired 전용")]
    public GameObject firedPrefab;
    public Vector3 firedOffset = new Vector3(0f, -1.5f, 0f);

    private Role role;
    private Choice choice;
    private ElementType[] diceTypes;
    private GameObject[] auraObjects;
    private GameObject[] firedOverlays;
    private bool wasRolling;

    void Start()
    {
        role = FindObjectOfType<Role>();
        choice = FindObjectOfType<Choice>();

        if (role != null)
        {
            diceTypes = new ElementType[role.diceCount];
            auraObjects = new GameObject[role.diceCount];
            firedOverlays = new GameObject[role.diceCount];

            // 게임 시작 시 모든 주사위 Standard로 초기화, aura 없음
            for (int i = 0; i < diceTypes.Length; i++)
                diceTypes[i] = ElementType.Standard;
        }
    }

    void Update()
    {
        if (role == null) return;

        // 롤 시작 시 모든 aura 즉시 제거 + 타입 Standard로 리셋
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
            diceTypes[i] = ElementType.Standard;
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
        // 각 원소의 확률로 개별 판정, 해당 안 되면 Standard
        // 100 기준으로 랜덤 판정
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;

        for (int i = 0; i < typeDatabase.Length; i++)
        {
            cumulative += typeDatabase[i].probability;
            if (roll < cumulative)
                return typeDatabase[i].type;
        }

        return ElementType.Standard;
    }

    void SpawnAura(int index)
    {
        if (role.DiceObjects == null || role.DiceObjects[index] == null) return;

        if (diceTypes[index] == ElementType.Fired)
        {
            if (firedPrefab == null) return;
            Vector3 pos = role.DiceObjects[index].transform.position + firedOffset;
            auraObjects[index] = Instantiate(firedPrefab, pos, Quaternion.identity);
            return;
        }

        DiceTypeData data = GetTypeData(diceTypes[index]);
        if (data == null || data.auraPrefab == null) return;

        Vector3 pos2 = role.DiceObjects[index].transform.position + auraOffset;
        auraObjects[index] = Instantiate(data.auraPrefab, pos2, Quaternion.identity);
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
        ClearFiredOverlay(index);
    }

    public void SpawnFiredOverlay(int index)
    {
        if (firedPrefab == null || role.DiceObjects == null || role.DiceObjects[index] == null) return;
        if (firedOverlays[index] != null) return; // 이미 있으면 스킵

        Vector3 pos = role.DiceObjects[index].transform.position + firedOffset;
        firedOverlays[index] = Instantiate(firedPrefab, pos, Quaternion.identity);
    }

    void ClearFiredOverlay(int index)
    {
        if (firedOverlays != null && firedOverlays[index] != null)
        {
            Destroy(firedOverlays[index]);
            firedOverlays[index] = null;
        }
    }

    void UpdateAuraPositions()
    {
        if (auraObjects == null || role.DiceObjects == null) return;

        for (int i = 0; i < auraObjects.Length; i++)
        {
            if (role.DiceObjects[i] == null) continue;

            if (auraObjects[i] != null)
            {
                Vector3 offset = (diceTypes[i] == ElementType.Fired) ? firedOffset : auraOffset;
                auraObjects[i].transform.position = role.DiceObjects[i].transform.position + offset;
                auraObjects[i].transform.rotation = Quaternion.identity;
            }

            if (firedOverlays != null && firedOverlays[i] != null)
            {
                firedOverlays[i].transform.position = role.DiceObjects[i].transform.position + firedOffset;
                firedOverlays[i].transform.rotation = Quaternion.identity;
            }
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

    public ElementType GetDiceType(int index)
    {
        if (diceTypes == null || index < 0 || index >= diceTypes.Length)
            return ElementType.Standard;
        return diceTypes[index];
    }

    public void SetDiceType(int index, ElementType newType)
    {
        if (diceTypes == null || index < 0 || index >= diceTypes.Length) return;
        ClearAura(index);
        diceTypes[index] = newType;
        SpawnAura(index);
    }
}
