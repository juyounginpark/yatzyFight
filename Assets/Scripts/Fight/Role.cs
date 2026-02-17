using UnityEngine;

[ExecuteInEditMode]
public class Role : MonoBehaviour
{
    [Header("주사위 설정")]
    public GameObject dicePrefab;
    public int diceCount = 5;
    public float spacing = 2f;
    public LayerMask diceLayer;

    [Header("굴림 애니메이션")]
    public float rollDuration = 1.2f;
    public float rollSpeed = 900f;

    [Header("Idle 애니메이션")]
    public float idleAmplitude = 0.15f;
    public float idleSpeedMin = 1.0f;
    public float idleSpeedMax = 1.8f;

    private GameObject[] diceObjects;
    private bool isRolling;
    private bool hasRolled;
    private int rollingCount;

    // idle용
    private float[] idleBaseY;
    private float[] idleSpeed;
    private float[] idleOffset;
    private float[] idleCurrentY;
    private Choice choice;

    public bool IsRolling => isRolling;
    public bool HasRolled => hasRolled;
    public GameObject[] DiceObjects => diceObjects;
    public float[] IdleBaseY => idleBaseY;

    /// <summary>
    /// 주사위의 현재 회전값으로 면 번호(1~6) 반환
    /// </summary>
    public int GetDiceValue(int index)
    {
        if (diceObjects == null || index < 0 || index >= diceObjects.Length)
            return 0;

        Quaternion rot = diceObjects[index].transform.rotation;

        float minAngle = float.MaxValue;
        int bestFace = 0;

        for (int i = 0; i < faceRotations.Length; i++)
        {
            float angle = Quaternion.Angle(rot, Quaternion.Euler(faceRotations[i]));
            if (angle < minAngle)
            {
                minAngle = angle;
                bestFace = i + 1; // 1~6
            }
        }

        return bestFace;
    }

    //    1
    // 3 2 4 5
    //    6
    // 2가 정면(카메라 방향) 기준 전개도 매핑
    public static readonly Vector3[] faceRotations = new Vector3[]
    {
        new Vector3(-90, 0, 0),     // 1번 면 (위 → 정면)
        new Vector3(0, 0, 0),       // 2번 면 (정면 그대로)
        new Vector3(0, 0, -90),     // 3번 면 (왼쪽 → 정면)
        new Vector3(0, 0, 90),      // 4번 면 (오른쪽 → 정면)
        new Vector3(180, 0, 0),     // 5번 면 (뒤 → 정면)
        new Vector3(90, 0, 0),      // 6번 면 (아래 → 정면)
    };


    void OnEnable()
    {
        if (!Application.isPlaying)
            SpawnPreview();
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
            ClearPreview();
    }

    void OnValidate()
    {
        // Inspector 값 변경 시 Edit Mode 미리보기 갱신
        if (!Application.isPlaying)
        {
            // OnValidate에서 직접 Destroy 불가 -> 다음 프레임에 실행
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ClearPreview();
                SpawnPreview();
            };
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            ClearPreview();
            SpawnDice();
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(KeyCode.Space) && !isRolling)
        {
            RollAllDice();
        }

        UpdateIdle();
    }

    void UpdateIdle()
    {
        if (diceObjects == null || isRolling) return;

        for (int i = 0; i < diceObjects.Length; i++)
        {
            if (diceObjects[i] == null) continue;

            // 선택 중(hover 또는 lock)이면 baseY로 부드럽게 복귀
            bool selected = choice != null && choice.IsDiceSelected(i);

            Vector3 pos = diceObjects[i].transform.position;

            if (selected)
            {
                // 부드럽게 원래 위치로
                pos.y = Mathf.Lerp(pos.y, idleBaseY[i], Time.deltaTime * 8f);
            }
            else
            {
                // 둥둥 떠다니기
                float target = idleBaseY[i] + Mathf.Sin(Time.time * idleSpeed[i] + idleOffset[i]) * idleAmplitude;
                pos.y = Mathf.Lerp(pos.y, target, Time.deltaTime * 5f);
            }

            diceObjects[i].transform.position = pos;
        }
    }

    // ===== Edit Mode 미리보기 =====
    void SpawnPreview()
    {
        if (dicePrefab == null) return;

        ClearPreview();

        float half = (diceCount - 1) * 0.5f;
        for (int i = 0; i < diceCount; i++)
        {
            float x = (i - half) * spacing;
            Vector3 position = transform.position + new Vector3(x, 0, 0);

#if UNITY_EDITOR
            GameObject preview = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(dicePrefab, transform);
            preview.transform.position = position;
            preview.transform.rotation = Quaternion.identity;
            preview.name = "[Preview] Dice_" + (i + 1);
            preview.hideFlags = HideFlags.DontSave;
#endif
        }
    }

    void ClearPreview()
    {
        // 자식 오브젝트 중 미리보기용만 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith("[Preview]"))
                DestroyImmediate(child);
        }
    }

    // ===== Play Mode =====
    void SpawnDice()
    {
        diceObjects = new GameObject[diceCount];
        idleBaseY = new float[diceCount];
        idleSpeed = new float[diceCount];
        idleOffset = new float[diceCount];
        idleCurrentY = new float[diceCount];

        float half = (diceCount - 1) * 0.5f;
        for (int i = 0; i < diceCount; i++)
        {
            float x = (i - half) * spacing;
            Vector3 position = transform.position + new Vector3(x, 0, 0);
            diceObjects[i] = Instantiate(dicePrefab, position, Quaternion.identity, transform);
            diceObjects[i].name = "Dice_" + (i + 1);
            diceObjects[i].tag = "Dice";
            // diceLayer에서 실제 레이어 번호 추출
            int layer = 0;
            int mask = diceLayer.value;
            while (mask > 1) { mask >>= 1; layer++; }
            if (diceLayer.value != 0)
                SetLayerRecursive(diceObjects[i], layer);

            idleBaseY[i] = position.y;
            idleSpeed[i] = Random.Range(idleSpeedMin, idleSpeedMax);
            idleOffset[i] = Random.Range(0f, Mathf.PI * 2f);
            idleCurrentY[i] = position.y;
        }

        choice = FindObjectOfType<Choice>();
    }

    public void RollAllDice()
    {
        isRolling = true;

        Choice choice = FindObjectOfType<Choice>();

        int rollCount = 0;
        for (int i = 0; i < diceObjects.Length; i++)
        {
            // 잠금된 주사위는 스킵
            if (choice != null && choice.IsDiceLocked(i))
                continue;

            Vector3 randomAxis = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;

            float speedVariation = rollSpeed + Random.Range(-150f, 150f);
            float durationVariation = rollDuration + Random.Range(-0.2f, 0.2f);
            StartCoroutine(RollDice(diceObjects[i], randomAxis, speedVariation, durationVariation));
            rollCount++;
        }

        rollingCount = rollCount;

        // 전부 잠금이면 바로 해제
        if (rollCount == 0)
            isRolling = false;
    }

    System.Collections.IEnumerator RollDice(GameObject dice, Vector3 axis, float speed, float duration)
    {
        float elapsed = 0f;

        // 1단계: 가속 (0% ~ 15%) - 빠르게 속도 올라감
        // 2단계: 최고속 유지 (15% ~ 50%) - 풀스피드로 회전
        // 3단계: 감속 (50% ~ 100%) - 부드럽게 느려짐

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float speedMultiplier;

            if (t < 0.15f)
            {
                // 가속: EaseIn (0 -> 1)
                float accelT = t / 0.15f;
                speedMultiplier = accelT * accelT;
            }
            else if (t < 0.5f)
            {
                // 최고속 유지
                speedMultiplier = 1f;
            }
            else
            {
                // 감속: EaseOut cubic (1 -> 0) 부드럽게
                float decelT = (t - 0.5f) / 0.5f;
                speedMultiplier = 1f - (decelT * decelT * decelT);
            }

            float currentSpeed = speed * speedMultiplier;
            dice.transform.Rotate(axis, currentSpeed * Time.deltaTime, Space.World);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 최종: faceRotations 중 랜덤으로 선택 (카메라 정면으로 면이 보이도록)
        Quaternion currentRot = dice.transform.rotation;
        int randomFace = Random.Range(0, faceRotations.Length);
        Quaternion targetRot = Quaternion.Euler(faceRotations[randomFace]);

        // 스냅도 보간으로 부드럽게
        float snapTime = 0f;
        while (snapTime < 0.15f)
        {
            snapTime += Time.deltaTime;
            float snapT = Mathf.Clamp01(snapTime / 0.15f);
            dice.transform.rotation = Quaternion.Slerp(currentRot, targetRot, snapT);
            yield return null;
        }
        dice.transform.rotation = targetRot;

        rollingCount--;
        if (rollingCount <= 0)
        {
            isRolling = false;
            hasRolled = true;
        }
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
