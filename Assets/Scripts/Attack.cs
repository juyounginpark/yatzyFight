using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixPlays.ElementalVFX;

[System.Serializable]
public class BeamData
{
    public ElementType type;
    public GameObject beamPrefab;
}

public class Attack : MonoBehaviour
{
    [Header("빔 설정")]
    public BeamData[] beamDatabase;
    public float beamDuration = 0.5f;

    private Role role;
    private DiceType diceType;
    private Choice choice;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    void Start()
    {
        role = FindObjectOfType<Role>();
        diceType = FindObjectOfType<DiceType>();
        choice = FindObjectOfType<Choice>();
    }

    public void Execute(GameObject target, int score)
    {
        if (isAttacking) return;
        StartCoroutine(AttackSequence(target, score));
    }

    [Header("공격 후 딜레이")]
    public float postDamageDelay = 0.8f;

    IEnumerator AttackSequence(GameObject target, int score)
    {
        isAttacking = true;

        // 각 주사위에서 해당 타입의 빔 발사
        List<GameObject> beams = new List<GameObject>();
        bool anyBeamFired = false;

        if (diceType != null && role != null && role.DiceObjects != null && target != null)
        {
            Vector3 targetPos = target.transform.position;

            for (int i = 0; i < role.diceCount; i++)
            {
                ElementType t = diceType.GetDiceType(i);
                if (t == ElementType.Normal) continue;

                GameObject prefab = GetBeamPrefab(t);
                if (prefab == null) continue;

                Vector3 origin = role.DiceObjects[i].transform.position;
                Vector3 direction = targetPos - origin;
                float distance = direction.magnitude;

                anyBeamFired = true;

                // BeamVfx 컴포넌트 확인
                BeamVfx beamVfx = prefab.GetComponent<BeamVfx>();
                if (beamVfx != null)
                {
                    GameObject beam = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    VfxData data = new VfxData(origin, targetPos, beamDuration, 1.0f);
                    beam.GetComponent<BeamVfx>().Play(data);
                }
                else
                {
                    Vector3 midPoint = (origin + targetPos) * 0.5f;
                    GameObject beam = Instantiate(prefab, midPoint, Quaternion.LookRotation(direction));
                    Vector3 scale = beam.transform.localScale;
                    scale.z = distance;
                    beam.transform.localScale = scale;
                    beams.Add(beam);
                }
            }

            // 빔이 하나라도 발사됐으면 beamDuration만큼 대기
            if (anyBeamFired)
            {
                yield return new WaitForSeconds(beamDuration);
            }

            // 단순 빔 정리
            foreach (GameObject beam in beams)
            {
                if (beam != null)
                    Destroy(beam);
            }
        }

        // 데미지
        if (target != null)
        {
            HP hp = target.GetComponent<HP>();
            if (hp != null)
                hp.TakeDamage(score);
        }

        // 데미지 연출 끝날 때까지 대기
        yield return new WaitForSeconds(postDamageDelay);

        // select 해제 + lock 해제 + 롤
        if (choice != null)
        {
            choice.Deselect();
            choice.UnlockAll();
        }

        if (role != null && !role.IsRolling)
            role.RollAllDice();

        isAttacking = false;
    }

    GameObject GetBeamPrefab(ElementType type)
    {
        if (beamDatabase == null) return null;

        for (int i = 0; i < beamDatabase.Length; i++)
        {
            if (beamDatabase[i].type == type)
                return beamDatabase[i].beamPrefab;
        }
        return null;
    }
}
