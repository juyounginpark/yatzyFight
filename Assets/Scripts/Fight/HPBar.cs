using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(HP))]
public class HPBar : MonoBehaviour
{
    [Header("UI 설정")]
    public Vector2 barSize = new Vector2(100f, 10f);
    public Vector3 worldOffset = new Vector3(0, 2f, 0);

    [Header("색상")]
    public Color highColor = Color.green;
    public Color midColor = Color.yellow;
    public Color lowColor = Color.red;

    [Header("데미지 텍스트")]
    public TMP_FontAsset dmgFont;
    public float dmgTextDuration = 1f;
    public float dmgTextRiseSpeed = 80f;
    public int dmgFontSize = 24;
    public Color dmgTextColor = Color.white;

    private HP hp;
    private Camera mainCam;
    private Canvas canvas;
    private RectTransform barRoot;
    private RectTransform fillRect;
    private Image fillImage;
    private System.Collections.Generic.List<GameObject> activeDmgTexts = new System.Collections.Generic.List<GameObject>();

    void Start()
    {
        hp = GetComponent<HP>();
        mainCam = Camera.main;
        CreateUI();

        hp.onDamaged.AddListener(OnDamaged);
    }

    private int prevHP;

    void OnDamaged()
    {
        int damage = prevHP - hp.CurrentHP;
        if (damage > 0)
            SpawnDamageText(damage);
        prevHP = hp.CurrentHP;
    }

    void Update()
    {
        if (hp == null || barRoot == null) return;

        UpdatePosition();
        UpdateBar();
    }

    void CreateUI()
    {
        canvas = FindHPCanvas();
        prevHP = hp.CurrentHP;

        // Bar Root
        GameObject rootObj = new GameObject("HPBar_" + gameObject.name);
        rootObj.transform.SetParent(canvas.transform, false);
        barRoot = rootObj.AddComponent<RectTransform>();
        barRoot.pivot = new Vector2(0.5f, 0.5f);
        barRoot.sizeDelta = barSize;

        // Background
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(barRoot, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.5f);

        // Fill (sizeDelta.x로 비율 조절, 왼쪽 정렬)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barRoot, false);
        fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(barSize.x, 0);
        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = highColor;
    }

    Canvas FindHPCanvas()
    {
        GameObject existing = GameObject.Find("HPCanvas");
        if (existing != null)
            return existing.GetComponent<Canvas>();

        GameObject canvasObj = new GameObject("HPCanvas");
        Canvas c = canvasObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        return c;
    }

    void UpdatePosition()
    {
        Vector3 worldPos = transform.position + worldOffset;
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0)
        {
            barRoot.gameObject.SetActive(false);
            return;
        }

        barRoot.gameObject.SetActive(true);
        barRoot.position = screenPos;
    }

    void UpdateBar()
    {
        float ratio = hp.Ratio;

        // sizeDelta.x로 fill 너비 조절
        fillRect.sizeDelta = new Vector2(barSize.x * ratio, 0);

        if (ratio > 0.5f)
            fillImage.color = Color.Lerp(midColor, highColor, (ratio - 0.5f) * 2f);
        else
            fillImage.color = Color.Lerp(lowColor, midColor, ratio * 2f);
    }

    void SpawnDamageText(int damage)
    {
        GameObject textObj = new GameObject("DmgText");
        textObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.position = barRoot.position + new Vector3(0, 20f, 0);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (dmgFont != null)
            tmp.font = dmgFont;
        tmp.text = "-" + damage;
        tmp.fontSize = dmgFontSize;
        tmp.color = dmgTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        rt.sizeDelta = new Vector2(200f, 50f);

        activeDmgTexts.Add(textObj);
        StartCoroutine(AnimateDamageText(textObj, rt, tmp));
    }

    System.Collections.IEnumerator AnimateDamageText(GameObject obj, RectTransform rt, TextMeshProUGUI tmp)
    {
        float elapsed = 0f;
        Vector3 startPos = rt.position;

        while (elapsed < dmgTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dmgTextDuration;

            rt.position = startPos + new Vector3(0, dmgTextRiseSpeed * t, 0);

            Color c = tmp.color;
            c.a = 1f - t;
            tmp.color = c;

            yield return null;
        }

        activeDmgTexts.Remove(obj);
        Destroy(obj);
    }

    void OnDestroy()
    {
        if (barRoot != null)
            Destroy(barRoot.gameObject);

        // 남아있는 데미지 텍스트 전부 정리
        foreach (var obj in activeDmgTexts)
        {
            if (obj != null) Destroy(obj);
        }
        activeDmgTexts.Clear();
    }
}
