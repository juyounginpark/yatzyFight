using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Choice : MonoBehaviour
{
    [Header("Choice 설정 (Dice: 클론, 적: 머티리얼)")]
    public Material choiceMaterial;
    public float choiceScale = 1.5f;

    [Header("Select 설정 (적: 머티리얼)")]
    public Material selectMaterial;

    [Header("Lock 설정 (Dice 전용)")]
    public Material lockMaterial;
    public float lockScale = 1.3f;

    [Header("호버 툴팁")]
    public TMP_FontAsset tooltipFont;
    public int tooltipFontSize = 20;
    public Color tooltipColor = Color.yellow;
    public Vector3 tooltipWorldOffset = new Vector3(0, 1.2f, 0);

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

    // 적 머티리얼 하이라이트 (클론 대신 직접 교체)
    private Renderer[] hoverRenderers;
    private Material[][] hoverOriginalMats;
    private Renderer[] selectRenderers;
    private Material[][] selectOriginalMats;

    // 호버 툴팁
    private Canvas tooltipCanvas;
    private GameObject tooltipObj;
    private TextMeshProUGUI tooltipTMP;
    private RectTransform tooltipRT;
    private int currentHoverDiceIndex = -1;

    private DiceUI diceUI;
    private Attack attack;
    private RetryUI retryUI;
    private GameFlow gameFlow;

    void Start()
    {
        mainCam = Camera.main;
        role = FindObjectOfType<Role>();
        diceUI = FindObjectOfType<DiceUI>();
        attack = FindObjectOfType<Attack>();
        retryUI = FindObjectOfType<RetryUI>();
        gameFlow = FindObjectOfType<GameFlow>();
        if (role != null)
        {
            int count = role.diceCount;
            lockObjects = new GameObject[count];
            lockedDice = new bool[count];
        }
    }

    void Update()
    {
        // 적 턴에는 모든 입력 차단
        if (gameFlow != null && !gameFlow.CanPlayerAct)
        {
            if (currentHoverObject != null)
            {
                DestroyChoice();
                RestoreHoverHighlight();
                DestroyTooltip();
                currentHoverObject = null;
                currentHoverDiceIndex = -1;
            }
            return;
        }

        // 롤 중에는 호버/클릭 전부 차단
        if (role != null && role.IsRolling)
        {
            if (currentHoverObject != null)
            {
                DestroyChoice();
                RestoreHoverHighlight();
                DestroyTooltip();
                currentHoverObject = null;
                currentHoverDiceIndex = -1;
            }
            return;
        }

        // 최초 롤 전에는 우클릭(롤)만 허용
        if (role != null && !role.HasRolled)
        {
            HandleAttack();
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
            GameObject hitObj = GetSelectableRoot(hit.collider.gameObject);
            if (hitObj != null)
                target = hitObj;
        }

        if (target == null)
        {
            if (currentHoverObject != null)
            {
                DestroyChoice();
                RestoreHoverHighlight();
                DestroyTooltip();
                currentHoverObject = null;
                currentHoverDiceIndex = -1;
            }
            return;
        }

        if (currentHoverObject == target) return;

        DestroyChoice();
        RestoreHoverHighlight();
        DestroyTooltip();
        currentHoverObject = target;
        currentHoverDiceIndex = -1;

        // Dice lock된 상태면 호버 효과 안 띄움 (but 툴팁은 표시)
        if (target.CompareTag("Dice"))
        {
            int diceIndex = GetDiceIndex(target);
            if (diceIndex >= 0 && lockedDice[diceIndex])
            {
                if (diceUI != null && role != null && role.HasRolled)
                {
                    currentHoverDiceIndex = diceIndex;
                    ShowTooltip(diceIndex);
                }
                return;
            }
        }

        // 이미 select된 개체면 호버 효과 안 띄움
        if (selectedObject == target) return;

        if (target.CompareTag("Dice"))
            CreateClone(target, choiceMaterial, choiceScale, ref currentChoice);
        else
            ApplyHighlight(target, choiceMaterial, ref hoverRenderers, ref hoverOriginalMats);

        // 다이스 호버 시 기여도 툴팁 표시
        if (target.CompareTag("Dice") && diceUI != null && role != null && role.HasRolled)
        {
            int di = GetDiceIndex(target);
            if (di >= 0)
            {
                currentHoverDiceIndex = di;
                ShowTooltip(di);
            }
        }
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
            // Dice 외: select/deselect 토글 (머티리얼 교체)
            if (selectedObject == currentHoverObject)
            {
                // 이미 선택된 개체 → 해제
                RestoreSelectHighlight();
                selectedObject = null;
                ApplyHighlight(currentHoverObject, choiceMaterial, ref hoverRenderers, ref hoverOriginalMats);
            }
            else
            {
                // 기존 선택 해제 + 새 선택
                RestoreSelectHighlight();
                RestoreHoverHighlight();
                selectedObject = currentHoverObject;
                ApplyHighlight(currentHoverObject, selectMaterial != null ? selectMaterial : choiceMaterial, ref selectRenderers, ref selectOriginalMats);
            }
        }
    }

    void HandleAttack()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        if (role == null || role.IsRolling) return;

        // 최초 롤 전에는 무조건 롤 허용
        if (!role.HasRolled)
        {
            role.RollAllDice();
            return;
        }

        if (attack != null && attack.IsAttacking) return;

        if (role.HasRolled && selectedObject != null && diceUI != null && diceUI.LastScore > 0)
        {
            if (attack != null)
                attack.Execute(selectedObject, diceUI.LastScore);
        }
        else
        {
            if (!role.IsRolling)
            {
                if (retryUI != null && !retryUI.ConsumeRetry())
                    return;

                role.RollAllDice();
            }
        }
    }

    public void Deselect()
    {
        RestoreSelectHighlight();
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

    // ===== 머티리얼 하이라이트 (적 등 Dice 외) =====

    void ApplyHighlight(GameObject target, Material mat, ref Renderer[] renderers, ref Material[][] originals)
    {
        if (mat == null) return;

        renderers = target.GetComponentsInChildren<Renderer>();
        originals = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originals[i] = renderers[i].sharedMaterials;
            Material[] mats = new Material[originals[i].Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = mat;
            renderers[i].sharedMaterials = mats;
        }
    }

    void RestoreHoverHighlight()
    {
        RestoreHighlight(ref hoverRenderers, ref hoverOriginalMats);
    }

    void RestoreSelectHighlight()
    {
        RestoreHighlight(ref selectRenderers, ref selectOriginalMats);
    }

    void RestoreHighlight(ref Renderer[] renderers, ref Material[][] originals)
    {
        if (renderers == null || originals == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].sharedMaterials = originals[i];
        }

        renderers = null;
        originals = null;
    }

    // ===== 클론 생성 (Dice 전용) =====

    void CreateClone(GameObject target, Material mat, float scale, ref GameObject result)
    {
        if (mat == null) return;

        MeshFilter mf = target.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        GameObject clone = new GameObject("[Clone]");
        clone.transform.position = mf.transform.position;
        clone.transform.rotation = mf.transform.rotation;
        clone.transform.localScale = mf.transform.lossyScale * scale;

        MeshFilter cloneMF = clone.AddComponent<MeshFilter>();
        cloneMF.sharedMesh = mf.sharedMesh;

        MeshRenderer cloneMR = clone.AddComponent<MeshRenderer>();
        cloneMR.material = mat;

        SetLayerRecursive(clone, LayerMask.NameToLayer("Ignore Raycast"));
        result = clone;
    }

    // ===== 클론 위치 동기화 (Dice 전용) =====

    void UpdateClonePositions()
    {
        // choice 복제본 → 호버 중인 주사위 따라가기
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

        // 툴팁 위치 갱신
        UpdateTooltipPosition();
    }

    void DestroyChoice()
    {
        if (currentChoice != null)
        {
            Destroy(currentChoice);
            currentChoice = null;
        }
    }

    // ===== 유틸 =====

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

    // ===== 호버 툴팁 =====

    void ShowTooltip(int diceIndex)
    {
        DestroyTooltip();

        if (tooltipCanvas == null)
        {
            GameObject existing = GameObject.Find("HPCanvas");
            if (existing != null)
                tooltipCanvas = existing.GetComponent<Canvas>();
            else
            {
                GameObject canvasObj = new GameObject("HPCanvas");
                tooltipCanvas = canvasObj.AddComponent<Canvas>();
                tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                tooltipCanvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        int contribution = diceUI.GetDiceContribution(diceIndex);

        tooltipObj = new GameObject("DiceTooltip");
        tooltipObj.transform.SetParent(tooltipCanvas.transform, false);

        tooltipRT = tooltipObj.AddComponent<RectTransform>();
        tooltipRT.sizeDelta = new Vector2(120f, 40f);

        tooltipTMP = tooltipObj.AddComponent<TextMeshProUGUI>();
        if (tooltipFont != null)
            tooltipTMP.font = tooltipFont;
        tooltipTMP.text = "DMG " + contribution;
        tooltipTMP.fontSize = tooltipFontSize;
        tooltipTMP.color = tooltipColor;
        tooltipTMP.alignment = TextAlignmentOptions.Center;
        tooltipTMP.raycastTarget = false;

        UpdateTooltipPosition();
    }

    void UpdateTooltipPosition()
    {
        if (tooltipObj == null || mainCam == null) return;
        if (currentHoverDiceIndex < 0 || role == null || role.DiceObjects == null) return;

        GameObject diceObj = role.DiceObjects[currentHoverDiceIndex];
        if (diceObj == null) return;

        Vector3 worldPos = diceObj.transform.position + tooltipWorldOffset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

        if (screenPos.z > 0)
            tooltipRT.position = screenPos;
    }

    void DestroyTooltip()
    {
        if (tooltipObj != null)
        {
            Destroy(tooltipObj);
            tooltipObj = null;
            tooltipTMP = null;
            tooltipRT = null;
        }
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
