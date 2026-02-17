using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(HP))]
public class PlayerHPUI : MonoBehaviour
{
    [Header("바 설정")]
    public Vector2 barSize = new Vector2(400f, 20f);
    public Vector2 screenOffset = new Vector2(0f, 40f);

    [Header("색상")]
    public Color bgColor = new Color(0f, 0f, 0f, 0.5f);
    public Color highColor = Color.green;
    public Color midColor = Color.yellow;
    public Color lowColor = Color.red;

    [Header("데미지 텍스트")]
    public TMP_FontAsset dmgFont;
    public float dmgTextDuration = 1f;
    public float dmgTextRiseSpeed = 80f;
    public int dmgFontSize = 28;
    public Color dmgTextColor = Color.red;

    private HP hp;
    private Canvas canvas;
    private RectTransform barRoot;
    private RectTransform fillRect;
    private Image fillImage;
    private int prevHP;

    void Start()
    {
        hp = GetComponent<HP>();
        prevHP = hp.CurrentHP;
        hp.onDamaged.AddListener(OnDamaged);

        GameFlow gameFlow = FindObjectOfType<GameFlow>();
        if (gameFlow != null && gameFlow.playerHP == null)
            gameFlow.playerHP = hp;

        CreateUI();
    }

    void CreateUI()
    {
        // 캔버스
        GameObject existing = GameObject.Find("HPCanvas");
        if (existing != null)
            canvas = existing.GetComponent<Canvas>();
        else
        {
            GameObject canvasObj = new GameObject("HPCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 바 루트 (하단 중앙 앵커)
        GameObject rootObj = new GameObject("PlayerHPBar");
        rootObj.transform.SetParent(canvas.transform, false);
        barRoot = rootObj.AddComponent<RectTransform>();
        barRoot.anchorMin = new Vector2(0.5f, 0f);
        barRoot.anchorMax = new Vector2(0.5f, 0f);
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.anchoredPosition = screenOffset;
        barRoot.sizeDelta = barSize;

        // 배경
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(barRoot, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = bgColor;

        // 채우기
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barRoot, false);
        fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(barSize.x, 0f);
        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = highColor;
    }

    void OnDamaged()
    {
        int damage = prevHP - hp.CurrentHP;
        if (damage > 0)
            SpawnDamageText(damage);
        prevHP = hp.CurrentHP;
    }

    void Update()
    {
        if (hp == null || fillRect == null) return;

        float ratio = hp.Ratio;
        fillRect.sizeDelta = new Vector2(barSize.x * ratio, 0f);

        if (ratio > 0.5f)
            fillImage.color = Color.Lerp(midColor, highColor, (ratio - 0.5f) * 2f);
        else
            fillImage.color = Color.Lerp(lowColor, midColor, ratio * 2f);
    }

    void SpawnDamageText(int damage)
    {
        if (canvas == null || barRoot == null) return;

        GameObject textObj = new GameObject("PlayerDmgText");
        textObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.position = barRoot.position + new Vector3(0, 30f, 0);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (dmgFont != null)
            tmp.font = dmgFont;
        tmp.text = "-" + damage;
        tmp.fontSize = dmgFontSize;
        tmp.color = dmgTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        rt.sizeDelta = new Vector2(200f, 50f);

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

        Destroy(obj);
    }

    void OnDestroy()
    {
        if (barRoot != null)
            Destroy(barRoot.gameObject);
    }
}
