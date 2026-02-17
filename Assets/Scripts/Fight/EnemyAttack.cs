using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixPlays.ElementalVFX;

public class EnemyAttack : MonoBehaviour
{
    [Header("주사위 설정")]
    public GameObject dicePrefab;
    public int diceCount = 3;
    public float diceSpacing = 1.5f;
    public float diceHeightPadding = 0.5f;
    public Vector3 diceRotationOffset = new Vector3(315f, 0f, 0f);

    [Header("굴림 애니메이션")]
    public float rollDuration = 1.2f;
    public float rollSpeed = 900f;

    [Header("주사위 커브 배치")]
    public float curveHeight = 0.6f;

    [Header("Idle 애니메이션")]
    public float idleAmplitude = 0.15f;
    public float idleSpeedMin = 1.0f;
    public float idleSpeedMax = 1.8f;

    [Header("공격 VFX")]
    public float projectileSpeed = 15f;
    public float dotFireDelay = 0.08f;

    [Header("타이밍")]
    public float preRollDelay = 0.5f;
    public float postRollDelay = 0.5f;
    public float postAttackDelay = 0.5f;

    private HP hp;
    private GameFlow gameFlow;
    private DiceUI diceUI;
    private Attack attack;
    private Animator anim;
    private GameObject[] diceObjects;
    private bool isAttacking;
    private bool isRolling;
    private float rollTickTimer;

    // idle용
    private float[] idleBaseY;
    private float[] idleSpeeds;
    private float[] idleOffsets;

    public bool IsAttacking => isAttacking;
    public bool IsDead => hp != null && hp.IsDead;

    public int CurrentDiceCount
    {
        get
        {
            if (hp == null) return diceCount;
            return Mathf.Max(1, Mathf.CeilToInt(diceCount * hp.Ratio));
        }
    }

    void Start()
    {
        hp = GetComponent<HP>();
        gameFlow = FindObjectOfType<GameFlow>();
        diceUI = FindObjectOfType<DiceUI>();
        attack = FindObjectOfType<Attack>();
        anim = GetComponentInChildren<Animator>();

        if (gameFlow != null)
            gameFlow.RegisterEnemy(this);

        if (hp != null)
            hp.onDeath.AddListener(OnDeath);
    }

    void OnDestroy()
    {
        if (gameFlow != null)
            gameFlow.UnregisterEnemy(this);

        DestroyDice();
    }

    /// <summary>
    /// 카메라 하단 중앙을 타겟 위치로 계산
    /// </summary>
    Vector3 GetAttackTargetPos()
    {
        Camera cam = Camera.main;
        float dist = Vector3.Distance(cam.transform.position, transform.position);
        return cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, dist));
    }

    public IEnumerator ExecuteAttack()
    {
        if (isAttacking || IsDead) yield break;

        isAttacking = true;

        int activeDice = CurrentDiceCount;

        // 1. 주사위 스폰 + DiceUI에 프리뷰 표시
        SpawnDice(activeDice);
        if (diceUI != null && diceUI.resultText != null)
        {
            string preview = "";
            for (int i = 0; i < activeDice; i++)
            {
                if (i > 0) preview += " ";
                preview += "?";
            }
            diceUI.resultText.text = preview + "\nscore : ?";
        }

        yield return new WaitForSeconds(preRollDelay);

        // 2. 주사위 롤 (롤링 중 DiceUI에 랜덤 숫자 표시)
        isRolling = true;
        rollTickTimer = 0f;
        yield return RollAllDice();
        isRolling = false;

        // 3. 데미지 계산 + DiceUI에 결과 표시
        int damage = CalculateDamage();
        string bestHand = GetBestHandName();
        if (diceUI != null && diceUI.resultText != null)
        {
            string values = "";
            for (int i = 0; i < diceObjects.Length; i++)
            {
                if (i > 0) values += " ";
                values += (diceObjects[i] != null) ? GetDiceValue(diceObjects[i]).ToString() : "?";
            }
            diceUI.resultText.text = values + "\nscore : " + damage + "\n" + bestHand + " +" + damage;
        }

        yield return new WaitForSeconds(postRollDelay);

        // 4. 공격 애니메이션
        if (anim != null)
            anim.SetTrigger("Attack");

        // 5. VFX 발사 (카메라 하단으로)
        yield return FireProjectiles();

        // 6. 데미지 적용 (GameFlow.playerHP)
        if (gameFlow != null && gameFlow.playerHP != null)
            gameFlow.playerHP.TakeDamage(damage);

        yield return new WaitForSeconds(postAttackDelay);

        // 7. 주사위 제거
        DestroyDice();

        isAttacking = false;
    }

    void SpawnDice(int count)
    {
        DestroyDice();

        diceObjects = new GameObject[count];
        idleBaseY = new float[count];
        idleSpeeds = new float[count];
        idleOffsets = new float[count];

        // enemy 콜라이더 기준 상단 오프셋 계산
        float topY = 0f;
        Collider col = GetComponentInChildren<Collider>();
        if (col != null)
            topY = col.bounds.max.y - transform.position.y;
        float spawnHeight = topY + diceHeightPadding;

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * diceSpacing;

            // 무지개 커브: 중앙이 높고 양끝이 낮은 포물선
            float t = (count > 1) ? (i - (count - 1) / 2f) / ((count - 1) / 2f) : 0f;
            float curve = (1f - t * t) * curveHeight;

            Vector3 pos = transform.position + new Vector3(x, spawnHeight + curve, 0f);

            int face = Random.Range(0, Role.faceRotations.Length);
            diceObjects[i] = Instantiate(dicePrefab, pos, Quaternion.Euler(Role.faceRotations[face]));
            diceObjects[i].name = "EnemyDice_" + (i + 1);

            // 플레이어 상호작용 방지
            SetLayerRecursive(diceObjects[i], LayerMask.NameToLayer("Ignore Raycast"));

            // Rigidbody 비활성화 (물리 간섭 방지)
            Rigidbody rb = diceObjects[i].GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            idleBaseY[i] = pos.y;
            idleSpeeds[i] = Random.Range(idleSpeedMin, idleSpeedMax);
            idleOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        UpdateIdle();

        // 롤링 중 DiceUI에 랜덤 숫자 표시
        if (isRolling && diceUI != null && diceUI.resultText != null)
        {
            rollTickTimer += Time.deltaTime;
            if (rollTickTimer >= 0.05f)
            {
                rollTickTimer = 0f;
                string values = "";
                for (int i = 0; i < diceObjects.Length; i++)
                {
                    if (i > 0) values += " ";
                    values += Random.Range(1, 7);
                }
                diceUI.resultText.text = values + "\nscore : --";
            }
        }
    }

    void UpdateIdle()
    {
        if (diceObjects == null || isAttacking) return;

        for (int i = 0; i < diceObjects.Length; i++)
        {
            if (diceObjects[i] == null) continue;

            Vector3 pos = diceObjects[i].transform.position;
            float target = idleBaseY[i] + Mathf.Sin(Time.time * idleSpeeds[i] + idleOffsets[i]) * idleAmplitude;
            pos.y = Mathf.Lerp(pos.y, target, Time.deltaTime * 5f);
            diceObjects[i].transform.position = pos;
        }
    }

    IEnumerator RollAllDice()
    {
        if (diceObjects == null) yield break;

        int rollCount = diceObjects.Length;
        int remaining = rollCount;

        for (int i = 0; i < rollCount; i++)
        {
            Vector3 randomAxis = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;

            float speedVar = rollSpeed + Random.Range(-150f, 150f);
            float durationVar = rollDuration + Random.Range(-0.2f, 0.2f);

            StartCoroutine(RollSingleDie(diceObjects[i], randomAxis, speedVar, durationVar, () => remaining--));
        }

        // 전부 끝날 때까지 대기
        while (remaining > 0)
            yield return null;
    }

    IEnumerator RollSingleDie(GameObject die, Vector3 axis, float speed, float duration, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float speedMultiplier;

            if (t < 0.15f)
            {
                float accelT = t / 0.15f;
                speedMultiplier = accelT * accelT;
            }
            else if (t < 0.5f)
            {
                speedMultiplier = 1f;
            }
            else
            {
                float decelT = (t - 0.5f) / 0.5f;
                speedMultiplier = 1f - (decelT * decelT * decelT);
            }

            die.transform.Rotate(axis, speed * speedMultiplier * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 1단계: 랜덤 면으로 스냅 (기본 faceRotation)
        Quaternion currentRot = die.transform.rotation;
        int randomFace = Random.Range(0, Role.faceRotations.Length);
        Quaternion faceRot = Quaternion.Euler(Role.faceRotations[randomFace]);

        float snapTime = 0f;
        while (snapTime < 0.15f)
        {
            snapTime += Time.deltaTime;
            float snapT = Mathf.Clamp01(snapTime / 0.15f);
            die.transform.rotation = Quaternion.Slerp(currentRot, faceRot, snapT);
            yield return null;
        }
        die.transform.rotation = faceRot;

        // 2단계: 화면 쪽으로 틸트 (오프셋 회전)
        Quaternion tiltTarget = Quaternion.Euler(diceRotationOffset) * faceRot;
        float tiltTime = 0f;
        while (tiltTime < 0.2f)
        {
            tiltTime += Time.deltaTime;
            float tiltT = Mathf.Clamp01(tiltTime / 0.2f);
            // EaseOut
            float eased = 1f - (1f - tiltT) * (1f - tiltT);
            die.transform.rotation = Quaternion.Slerp(faceRot, tiltTarget, eased);
            yield return null;
        }
        die.transform.rotation = tiltTarget;

        onComplete?.Invoke();
    }

    int GetDiceValue(GameObject die)
    {
        // 오프셋 역회전으로 원래 faceRotation 기준으로 비교
        Quaternion offsetInv = Quaternion.Inverse(Quaternion.Euler(diceRotationOffset));
        Quaternion rot = offsetInv * die.transform.rotation;
        float minAngle = float.MaxValue;
        int bestFace = 1;

        for (int i = 0; i < Role.faceRotations.Length; i++)
        {
            float angle = Quaternion.Angle(rot, Quaternion.Euler(Role.faceRotations[i]));
            if (angle < minAngle)
            {
                minAngle = angle;
                bestFace = i + 1;
            }
        }

        return bestFace;
    }

    int CalculateDamage()
    {
        if (diceObjects == null) return 0;

        int n = diceObjects.Length;
        int[] dice = new int[n];
        int[] counts = new int[7];
        int total = 0;

        for (int i = 0; i < n; i++)
        {
            dice[i] = (diceObjects[i] != null) ? GetDiceValue(diceObjects[i]) : 1;
            counts[dice[i]]++;
            total += dice[i];
        }

        int best = 0;

        // Upper (각 숫자 합)
        for (int v = 1; v <= 6; v++)
        {
            int s = counts[v] * v;
            if (s > best) best = s;
        }

        // One Pair (2개 이상)
        if (n >= 2)
        {
            for (int v = 6; v >= 1; v--)
            {
                if (counts[v] >= 2) { int s = v * 2; if (s > best) best = s; break; }
            }
        }

        // Two Pairs (4개 이상)
        if (n >= 4)
        {
            int pairCount = 0, pairSum = 0;
            for (int v = 6; v >= 1; v--)
            {
                if (counts[v] >= 2) { pairCount++; pairSum += v * 2; }
            }
            if (pairCount >= 2 && pairSum > best) best = pairSum;
        }

        // Three of a Kind (3개 이상)
        if (n >= 3)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= 3 && total > best) best = total;
            }
        }

        // Four of a Kind (4개 이상)
        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= 4 && total > best) best = total;
            }
        }

        // Full House (5개 이상)
        if (n >= 5)
        {
            bool hasThree = false, hasTwo = false;
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] == 3) hasThree = true;
                if (counts[v] == 2) hasTwo = true;
            }
            if (hasThree && hasTwo && 25 > best) best = 25;
        }

        // Small Straight (4개 이상)
        if (n >= 4)
        {
            if ((counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1) ||
                (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) ||
                (counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1))
            {
                if (30 > best) best = 30;
            }
        }

        // Large Straight (5개 이상)
        if (n >= 5)
        {
            if ((counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) ||
                (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1))
            {
                if (40 > best) best = 40;
            }
        }

        // All of a Kind (4개 이상부터)
        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= n)
                {
                    int s = 10 * n;
                    if (s > best) best = s;
                }
            }
        }

        return best;
    }

    IEnumerator FireProjectiles()
    {
        if (diceObjects == null) yield break;

        // Attack의 vfxDatabase에서 bullet 자동 가져오기
        if (attack == null)
            attack = FindObjectOfType<Attack>();

        GameObject bulletPrefab = null;
        if (attack != null && attack.vfxDatabase != null)
        {
            // Standard bullet 우선
            for (int i = 0; i < attack.vfxDatabase.Length; i++)
            {
                if (attack.vfxDatabase[i].type == ElementType.Standard && attack.vfxDatabase[i].bulletPrefab != null)
                {
                    bulletPrefab = attack.vfxDatabase[i].bulletPrefab;
                    break;
                }
            }

            // Standard 없으면 아무 bullet이라도
            if (bulletPrefab == null)
            {
                for (int i = 0; i < attack.vfxDatabase.Length; i++)
                {
                    if (attack.vfxDatabase[i].bulletPrefab != null)
                    {
                        bulletPrefab = attack.vfxDatabase[i].bulletPrefab;
                        break;
                    }
                }
            }
        }

        if (bulletPrefab == null) yield break;

        // 카메라 하단 중앙을 타겟으로
        Vector3 targetPos = GetAttackTargetPos();

        List<GameObject> spawned = new List<GameObject>();
        float longestWait = 0f;

        bool isBaseVfx = bulletPrefab.GetComponent<BaseVfx>() != null;

        for (int i = 0; i < diceObjects.Length; i++)
        {
            if (diceObjects[i] == null) continue;

            int faceValue = GetDiceValue(diceObjects[i]);
            Vector3 origin = diceObjects[i].transform.position;
            float dist = Vector3.Distance(origin, targetPos);
            float travelTime = dist / projectileSpeed;

            for (int d = 0; d < faceValue; d++)
            {
                float totalTime = d * dotFireDelay + travelTime;
                if (totalTime > longestWait) longestWait = totalTime;

                if (d == 0)
                    FireEnemyVfx(bulletPrefab, isBaseVfx, origin, targetPos, travelTime, spawned);
                else
                    StartCoroutine(DelayedEnemyVfx(bulletPrefab, isBaseVfx, origin, targetPos, travelTime, d * dotFireDelay, spawned));
            }
        }

        if (longestWait > 0f)
            yield return new WaitForSeconds(longestWait);

        foreach (GameObject obj in spawned)
        {
            if (obj != null) Destroy(obj);
        }
    }

    void FireEnemyVfx(GameObject prefab, bool isBaseVfx, Vector3 origin, Vector3 targetPos, float duration, List<GameObject> spawned)
    {
        if (isBaseVfx)
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

    IEnumerator DelayedEnemyVfx(GameObject prefab, bool isBaseVfx, Vector3 origin, Vector3 targetPos, float duration, float delay, List<GameObject> spawned)
    {
        yield return new WaitForSeconds(delay);
        FireEnemyVfx(prefab, isBaseVfx, origin, targetPos, duration, spawned);
    }

    void DestroyDice()
    {
        if (diceObjects == null) return;

        for (int i = 0; i < diceObjects.Length; i++)
        {
            if (diceObjects[i] != null)
                Destroy(diceObjects[i]);
        }
        diceObjects = null;
    }

    void OnDeath()
    {
        if (gameFlow != null)
            gameFlow.UnregisterEnemy(this);

        DestroyDice();
        Destroy(gameObject);
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ===== 족보 이름 =====

    string GetBestHandName()
    {
        if (diceObjects == null) return "";

        int n = diceObjects.Length;
        int[] dice = new int[n];
        int[] counts = new int[7];
        int total = 0;

        for (int i = 0; i < n; i++)
        {
            dice[i] = (diceObjects[i] != null) ? GetDiceValue(diceObjects[i]) : 1;
            counts[dice[i]]++;
            total += dice[i];
        }

        int best = 0;
        string bestName = "";

        // Upper
        string[] upperNames = { "", "Ace", "Two", "Three", "Four", "Five", "Six" };
        for (int v = 1; v <= 6; v++)
        {
            int s = counts[v] * v;
            if (s > best) { best = s; bestName = upperNames[v]; }
        }

        // One Pair
        if (n >= 2)
        {
            for (int v = 6; v >= 1; v--)
            {
                if (counts[v] >= 2) { int s = v * 2; if (s > best) { best = s; bestName = "One Pair"; } break; }
            }
        }

        // Two Pairs
        if (n >= 4)
        {
            int pc = 0, ps = 0;
            for (int v = 6; v >= 1; v--)
            {
                if (counts[v] >= 2) { pc++; ps += v * 2; }
            }
            if (pc >= 2 && ps > best) { best = ps; bestName = "Two Pairs"; }
        }

        // Three of a Kind
        if (n >= 3)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= 3 && total > best) { best = total; bestName = "Three of a Kind"; }
            }
        }

        // Four of a Kind
        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= 4 && total > best) { best = total; bestName = "Four of a Kind"; }
            }
        }

        // Full House
        if (n >= 5)
        {
            bool hasThree = false, hasTwo = false;
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] == 3) hasThree = true;
                if (counts[v] == 2) hasTwo = true;
            }
            if (hasThree && hasTwo && 25 > best) { best = 25; bestName = "Full House"; }
        }

        // Small Straight
        if (n >= 4)
        {
            if ((counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1) ||
                (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) ||
                (counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1))
            {
                if (30 > best) { best = 30; bestName = "Small Straight"; }
            }
        }

        // Large Straight
        if (n >= 5)
        {
            if ((counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) ||
                (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1))
            {
                if (40 > best) { best = 40; bestName = "Large Straight"; }
            }
        }

        // All of a Kind (4개 이상부터)
        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= n)
                {
                    int s = 10 * n;
                    if (s > best) { best = s; bestName = "All of a Kind"; }
                }
            }
        }

        return bestName;
    }
}
