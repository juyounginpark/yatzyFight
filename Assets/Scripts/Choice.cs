using UnityEngine;

public class Choice : MonoBehaviour
{
    [Header("Choice 설정")]
    public Material choiceMaterial;
    public float choiceScale = 1.5f;

    [Header("Lock 설정 (Dice 전용)")]
    public Material lockMaterial;
    public float lockScale = 1.3f;

    [Header("Select 설정 (Dice 외)")]
    public Material selectMaterial;
    public float selectScale = 1.3f;

    [Header("레이어")]
    public LayerMask selectableLayer;

    private Camera mainCam;
    private Role role;
    private GameObject currentChoice;
    private GameObject currentHoverObject;

    // 주사위별 잠금 상태
    private GameObject[] lockObjects;
    private bool[] lockedDice;

    // Dice 외 선택 상태
    private GameObject selectedObject;
    private GameObject selectClone;

    void Start()
    {
        mainCam = Camera.main;

        role = FindObjectOfType<Role>();
        diceUI = FindObjectOfType<DiceUI>();
        attack = FindObjectOfType<Attack>();
        if (role != null)
        {
            int count = role.diceCount;
            lockObjects = new GameObject[count];
            lockedDice = new bool[count];
        }
    }

    private DiceUI diceUI;
    private Attack attack;

    void Update()
    {
        // 최초 롤 전 또는 롤 중에는 호버/클릭 전부 차단
        if (role != null && (role.IsRolling || !role.HasRolled))
        {
            if (currentHoverObject != null)
            {
                DestroyChoice();
                currentHoverObject = null;
            }
            return;
        }

        HandleHover();
        HandleClick();
        HandleAttack();
        UpdateClonePositions();
    }

    void HandleHover()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        GameObject target = null;

        if (Physics.Raycast(ray, out hit))
        {
            // Selectable 레이어에 해당하는 루트 개체 찾기
            GameObject hitObj = GetSelectableRoot(hit.collider.gameObject);
            if (hitObj != null)
                target = hitObj;
        }

        if (target == null)
        {
            if (currentHoverObject != null)
            {
                DestroyChoice();
                currentHoverObject = null;
            }
            return;
        }

        if (currentHoverObject == target) return;

        DestroyChoice();
        currentHoverObject = target;

        // Dice lock된 상태면 호버 효과 안 띄움
        if (target.CompareTag("Dice"))
        {
            int diceIndex = GetDiceIndex(target);
            if (diceIndex >= 0 && lockedDice[diceIndex]) return;
        }

        // 이미 select된 개체면 호버 효과 안 띄움
        if (selectedObject == target) return;

        CreateClone(target, choiceMaterial, choiceScale, ref currentChoice);
    }

    void HandleClick()
    {
        if (!Input.GetMouseButtonDown(0) || currentHoverObject == null) return;

        if (currentHoverObject.CompareTag("Dice"))
        {
            // Dice: lock/unlock
            int index = GetDiceIndex(currentHoverObject);
            if (index < 0) return;

            if (!lockedDice[index])
            {
                lockedDice[index] = true;
                DestroyChoice();

                if (role != null && role.IdleBaseY != null && index < role.IdleBaseY.Length)
                {
                    Vector3 pos = currentHoverObject.transform.position;
                    pos.y = role.IdleBaseY[index];
                    currentHoverObject.transform.position = pos;
                }

                CreateClone(currentHoverObject, lockMaterial, lockScale, ref lockObjects[index]);
            }
            else
            {
                lockedDice[index] = false;
                if (lockObjects[index] != null)
                {
                    Destroy(lockObjects[index]);
                    lockObjects[index] = null;
                }
                CreateClone(currentHoverObject, choiceMaterial, choiceScale, ref currentChoice);
            }
        }
        else
        {
            // Dice 외: select/deselect 토글
            if (selectedObject == currentHoverObject)
            {
                // 이미 선택된 개체 → 해제
                if (selectClone != null)
                {
                    Destroy(selectClone);
                    selectClone = null;
                }
                selectedObject = null;
                CreateClone(currentHoverObject, choiceMaterial, choiceScale, ref currentChoice);
            }
            else
            {
                // 기존 선택 해제
                if (selectClone != null)
                {
                    Destroy(selectClone);
                    selectClone = null;
                }
                selectedObject = currentHoverObject;
                DestroyChoice();
                CreateClone(currentHoverObject, selectMaterial, selectScale, ref selectClone);
            }
        }
    }

    void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        if (role == null || !role.HasRolled || role.IsRolling) return;
        if (attack != null && attack.IsAttacking) return;

        if (selectedObject != null && diceUI != null && diceUI.LastScore > 0)
        {
            // select된 개체 있으면: Attack으로 위임 (빔 + 데미지 + lock해제 + 롤)
            if (attack != null)
                attack.Execute(selectedObject, diceUI.LastScore);
        }
        else
        {
            // 빈 곳: 일반 롤
            if (!role.IsRolling)
                role.RollAllDice();
        }
    }

    public void Deselect()
    {
        if (selectClone != null)
        {
            Destroy(selectClone);
            selectClone = null;
        }
        selectedObject = null;
    }

    public void UnlockAll()
    {
        if (lockedDice == null) return;

        for (int i = 0; i < lockedDice.Length; i++)
        {
            lockedDice[i] = false;
            if (lockObjects[i] != null)
            {
                Destroy(lockObjects[i]);
                lockObjects[i] = null;
            }
        }
    }

    public GameObject SelectedObject => selectedObject;

    void CreateClone(GameObject target, Material mat, float scale, ref GameObject result)
    {
        if (mat == null) return;

        MeshFilter mf = target.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        GameObject clone = new GameObject("[Clone]");
        clone.transform.position = target.transform.position;
        clone.transform.rotation = target.transform.rotation;
        clone.transform.localScale = target.transform.lossyScale * scale;

        MeshFilter cloneMF = clone.AddComponent<MeshFilter>();
        cloneMF.sharedMesh = mf.sharedMesh;

        MeshRenderer cloneMR = clone.AddComponent<MeshRenderer>();
        cloneMR.material = mat;

        SetLayerRecursive(clone, LayerMask.NameToLayer("Ignore Raycast"));
        result = clone;
    }

    void UpdateClonePositions()
    {
        // choice 복제본 → 호버 중인 원본 따라가기
        if (currentChoice != null && currentHoverObject != null)
        {
            currentChoice.transform.position = currentHoverObject.transform.position;
            currentChoice.transform.rotation = currentHoverObject.transform.rotation;
        }

        // lock 복제본 → 각 주사위 따라가기
        if (lockObjects != null && role != null && role.DiceObjects != null)
        {
            for (int i = 0; i < lockObjects.Length; i++)
            {
                if (lockObjects[i] == null || role.DiceObjects[i] == null) continue;
                lockObjects[i].transform.position = role.DiceObjects[i].transform.position;
                lockObjects[i].transform.rotation = role.DiceObjects[i].transform.rotation;
            }
        }

        // select 복제본 → 선택된 원본 따라가기
        if (selectClone != null && selectedObject != null)
        {
            selectClone.transform.position = selectedObject.transform.position;
            selectClone.transform.rotation = selectedObject.transform.rotation;
        }
    }

    void DestroyChoice()
    {
        if (currentChoice != null)
        {
            Destroy(currentChoice);
            currentChoice = null;
        }
    }

    GameObject GetSelectableRoot(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (((1 << current.gameObject.layer) & selectableLayer) != 0)
                return current.gameObject;
            current = current.parent;
        }
        return null;
    }

    int GetDiceIndex(GameObject obj)
    {
        string name = obj.name;
        if (name.StartsWith("Dice_"))
        {
            int num;
            if (int.TryParse(name.Substring(5), out num))
                return num - 1;
        }
        return -1;
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    public bool IsDiceLocked(int index)
    {
        if (lockedDice == null || index < 0 || index >= lockedDice.Length)
            return false;
        return lockedDice[index];
    }

    public bool IsDiceSelected(int index)
    {
        if (IsDiceLocked(index)) return true;
        if (currentHoverObject == null) return false;
        return GetDiceIndex(currentHoverObject) == index;
    }
}
