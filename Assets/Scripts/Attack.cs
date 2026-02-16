using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixPlays.ElementalVFX;

[System.Serializable]
public class AttackVfxData
{
    public ElementType type;
    public GameObject bulletPrefab;
    public GameObject beamPrefab;
}

public class Attack : MonoBehaviour
{
    [Header("타입별 Bullet / Beam")]
    public AttackVfxData[] vfxDatabase;

    [Header("Bullet 설정")]
    public float bulletSpeed = 20f;

    [Header("Beam 설정")]
    public float beamDuration = 0.5f;

    [Header("공격 후 딜레이")]
    public float postDamageDelay = 0.8f;

    private Role role;
    private DiceType diceType;
    private DiceUI diceUI;
    private Choice choice;
    private RetryUI retryUI;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    void Start()
    {
        role = FindObjectOfType<Role>();
        diceType = FindObjectOfType<DiceType>();
        diceUI = FindObjectOfType<DiceUI>();
        choice = FindObjectOfType<Choice>();
        retryUI = FindObjectOfType<RetryUI>();
    }

    public void Execute(GameObject target, int score)
    {
        if (isAttacking) return;
        StartCoroutine(AttackSequence(target, score));
    }

    IEnumerator AttackSequence(GameObject target, int score)
    {
        isAttacking = true;

        if (choice != null)
            choice.Deselect();

        List<GameObject> spawned = new List<GameObject>();
        bool anyFired = false;
        float longestWait = 0f;

        if (diceType != null && role != null && role.DiceObjects != null && target != null)
        {
            Vector3 targetPos = target.transform.position;

            for (int i = 0; i < role.diceCount; i++)
            {
                ElementType t = diceType.GetDiceType(i);
                // Fired → Fire의 VFX 사용
                ElementType lookupType = (t == ElementType.Fired) ? ElementType.Fire : t;
                AttackVfxData data = GetVfxData(lookupType);
                if (data == null) continue;

                Vector3 origin = role.DiceObjects[i].transform.position;
                bool isSynergy = diceUI != null && IsSynergyType(t);

                if (isSynergy && data.beamPrefab != null)
                {
                    // 시너지 활성 → beam
                    anyFired = true;
                    if (beamDuration > longestWait) longestWait = beamDuration;
                    FireVfx(data.beamPrefab, origin, targetPos, beamDuration, spawned);
                }
                else if (data.bulletPrefab != null)
                {
                    // 비시너지 → 해당 원소 bullet
                    anyFired = true;
                    float dist = Vector3.Distance(origin, targetPos);
                    float travelTime = dist / bulletSpeed;
                    if (travelTime > longestWait) longestWait = travelTime;
                    FireVfx(data.bulletPrefab, origin, targetPos, travelTime, spawned);
                }
            }

            if (anyFired)
                yield return new WaitForSeconds(longestWait);

            foreach (GameObject obj in spawned)
            {
                if (obj != null) Destroy(obj);
            }
        }

        if (target != null)
        {
            HP hp = target.GetComponent<HP>();
            if (hp != null)
                hp.TakeDamage(score);
        }

        yield return new WaitForSeconds(postDamageDelay);

        // 리롤 횟수 초기화
        if (retryUI != null)
            retryUI.ResetRetries();

        if (choice != null)
            choice.UnlockAll();

        if (role != null && !role.IsRolling)
            role.RollAllDice();

        isAttacking = false;
    }

    void FireVfx(GameObject prefab, Vector3 origin, Vector3 targetPos, float duration, List<GameObject> spawned)
    {
        BaseVfx vfx = prefab.GetComponent<BaseVfx>();
        if (vfx != null)
        {
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            VfxData vd = new VfxData(origin, targetPos, duration, 1.0f);
            obj.GetComponent<BaseVfx>().Play(vd);
        }
        else
        {
            Vector3 direction = targetPos - origin;
            float distance = direction.magnitude;
            Vector3 midPoint = (origin + targetPos) * 0.5f;
            GameObject obj = Instantiate(prefab, midPoint, Quaternion.LookRotation(direction));
            Vector3 scale = obj.transform.localScale;
            scale.z = distance;
            obj.transform.localScale = scale;
            spawned.Add(obj);
        }
    }

    bool IsSynergyType(ElementType t)
    {
        if ((t == ElementType.Fire || t == ElementType.Fired) && diceUI.SynergyFire) return true;
        if (t == ElementType.Water && diceUI.SynergyWater) return true;
        if (t == ElementType.Wind && diceUI.SynergyWind) return true;
        if (t == ElementType.Earth && diceUI.SynergyEarth) return true;
        return false;
    }

    AttackVfxData GetVfxData(ElementType type)
    {
        if (vfxDatabase == null) return null;

        for (int i = 0; i < vfxDatabase.Length; i++)
        {
            if (vfxDatabase[i].type == type)
                return vfxDatabase[i];
        }
        return null;
    }
}
