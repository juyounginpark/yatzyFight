using UnityEngine;
using UnityEngine.Events;

public class HP : MonoBehaviour
{
    [Header("체력 설정")]
    public int maxHP = 1000;

    [Header("피격 애니메이션")]
    public Material hitMaterial;
    public float hitShakeDuration = 0.3f;
    public float hitShakeIntensity = 0.2f;
    public float hitFlashDuration = 0.15f;

    [Header("이벤트")]
    public UnityEvent onDeath;
    public UnityEvent onDamaged;

    private int currentHP;
    private Vector3 originalPosition;
    private float shakeTimer;
    private Renderer[] renderers;
    private Material[] originalMaterials;
    private float flashTimer;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public float Ratio => (float)currentHP / maxHP;
    public bool IsDead => currentHP <= 0;

    void Start()
    {
        currentHP = maxHP;
        originalPosition = transform.position;
        CacheRenderers();
    }

    void Update()
    {
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            float t = shakeTimer / hitShakeDuration;
            Vector3 offset = Random.insideUnitSphere * hitShakeIntensity * t;
            offset.z = 0f;
            transform.position = originalPosition + offset;

            if (shakeTimer <= 0)
                transform.position = originalPosition;
        }

        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0)
                RestoreMaterials();
        }
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHP = Mathf.Max(0, currentHP - amount);

        originalPosition = transform.position;
        shakeTimer = hitShakeDuration;
        FlashMaterial();

        if (onDamaged != null)
            onDamaged.Invoke();

        if (currentHP <= 0)
        {
            if (onDeath != null)
                onDeath.Invoke();
            else
                Destroy(gameObject);
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;
    }

    void FlashMaterial()
    {
        if (hitMaterial == null) return;

        flashTimer = hitFlashDuration;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = hitMaterial;
    }

    void RestoreMaterials()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = originalMaterials[i];
    }
}
