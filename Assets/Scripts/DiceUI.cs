using UnityEngine;
using TMPro;
using System.Collections.Generic;

[ExecuteInEditMode]
[DefaultExecutionOrder(10)]
public class DiceUI : MonoBehaviour
{
    [Header("참조")]
    public Role role;
    public DiceType diceType;
    public TextMeshProUGUI resultText;

    private bool wasRolling;
    private bool hasRolled;
    private float rollTickTimer;
    private bool hasYahtzee;
    private Choice choice;
    private int[] lockedValues;
    private int lastScore;

    // 시너지 플래그 (Attack에서 참조)
    private bool synFire, synWater, synWind, synEarth;

    public int LastScore => lastScore;
    public bool SynergyFire => synFire;
    public bool SynergyWater => synWater;
    public bool SynergyWind => synWind;
    public bool SynergyEarth => synEarth;

    void OnEnable()
    {
        if (!Application.isPlaying)
            ShowPreview();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            ShowPreview();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            if (role == null)
                role = FindObjectOfType<Role>();
            if (diceType == null)
                diceType = FindObjectOfType<DiceType>();

            choice = FindObjectOfType<Choice>();
            lockedValues = new int[role.diceCount];
            ShowPreview();
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (role != null && role.IsRolling)
        {
            wasRolling = true;

            rollTickTimer += Time.deltaTime;
            if (rollTickTimer >= 0.05f)
            {
                rollTickTimer = 0f;
                ShowRandomNumbers();
            }
            return;
        }

        if (wasRolling)
        {
            hasRolled = true;
            wasRolling = false;

            // Earth 4+ 체크 → 해당 시 Standard 변환 + 리롤
            if (CheckEarthOverflow())
                return;

            // Fire Synergy → 이웃 Standard를 Fired로 변환 (리롤 없이 즉시 변환)
            CheckFirePropagation();


            UpdateUI();
        }
    }

    void ShowPreview()
    {
        if (resultText == null) return;

        int count = 5;
        if (role != null) count = role.diceCount;

        string values = "";
        for (int i = 0; i < count; i++)
        {
            if (i > 0) values += " ";
            values += "?";
        }
        resultText.text = values + "\nscore : ?";
    }

    void ShowRandomNumbers()
    {
        if (resultText == null) return;

        int count = 5;
        if (role != null) count = role.diceCount;

        string values = "";
        for (int i = 0; i < count; i++)
        {
            if (i > 0) values += " ";

            if (choice != null && choice.IsDiceLocked(i))
                values += hasRolled ? lockedValues[i].ToString() : "?";
            else
                values += Random.Range(1, 7);
        }
        resultText.text = values + "\nscore : --";
    }

    // Earth 4개 이상 → 모든 Earth를 Normal로 변환, lock 해제, 리롤
    bool CheckEarthOverflow()
    {
        if (diceType == null || role == null || choice == null) return false;

        int earthCnt = 0;
        for (int i = 0; i < role.diceCount; i++)
        {
            if (diceType.GetDiceType(i) == ElementType.Earth)
                earthCnt++;
        }

        if (earthCnt < 4) return false;

        // Earth → Standard 변환 + lock 해제
        for (int i = 0; i < role.diceCount; i++)
        {
            if (diceType.GetDiceType(i) == ElementType.Earth)
                diceType.SetDiceType(i, ElementType.Standard);
        }

        choice.UnlockAll();
        role.RollAllDice();
        return true;
    }

    // Fire Synergy (2개 이상, 모두 짝수) 조건 만족 시
    // Fire 주사위의 양 옆(인덱스 -1, +1)이 Standard라면 Fired로 변경
    void CheckFirePropagation()
    {
        if (diceType == null || role == null) return;

        int fireCnt = 0;
        bool allEven = true;
        List<int> fireIndices = new List<int>();

        for (int i = 0; i < role.diceCount; i++)
        {
            if (diceType.GetDiceType(i) == ElementType.Fire)
            {
                fireCnt++;
                fireIndices.Add(i);
                int val = role.GetDiceValue(i);
                if (val % 2 != 0) allEven = false;
            }
        }

        if (fireCnt >= 2 && allEven)
        {
            // 양옆 Standard → Fired 변환
            foreach (int idx in fireIndices)
            {
                if (idx > 0 && diceType.GetDiceType(idx - 1) == ElementType.Standard)
                    diceType.SetDiceType(idx - 1, ElementType.Fired);
                if (idx < role.diceCount - 1 && diceType.GetDiceType(idx + 1) == ElementType.Standard)
                    diceType.SetDiceType(idx + 1, ElementType.Fired);
            }

            // Fire 주사위 자체에도 Fired 오버레이 추가
            foreach (int idx in fireIndices)
                diceType.SpawnFiredOverlay(idx);
        }
    }

    void UpdateUI()
    {
        if (role == null || role.DiceObjects == null || resultText == null) return;

        int[] dice = new int[role.diceCount];
        ElementType[] types = new ElementType[role.diceCount];
        string values = "";

        for (int i = 0; i < role.diceCount; i++)
        {
            dice[i] = role.GetDiceValue(i);
            lockedValues[i] = dice[i];
            types[i] = (diceType != null) ? diceType.GetDiceType(i) : ElementType.Standard;
            if (i > 0) values += " ";
            values += dice[i];
        }

        List<string> breakdown = new List<string>();
        int totalScore = CalcTotalScore(dice, types, breakdown);

        lastScore = totalScore;

        string breakdownText = "";
        for (int i = 0; i < breakdown.Count; i++)
            breakdownText += "\n" + breakdown[i];

        resultText.text = values + "\nscore : " + totalScore + breakdownText;
    }

    // ===== 점수 계산 =====
    // 야치 조합 + 원소 → 제일 높은 1개만 채택
    int CalcTotalScore(int[] dice, ElementType[] types, List<string> log)
    {
        synFire = synWater = synWind = synEarth = false;

        int n = dice.Length;

        // ── 기본 데이터 ──
        int[] counts = new int[7];
        int total = 0;

        for (int i = 0; i < n; i++)
        {
            counts[dice[i]]++;
            total += dice[i];
        }

        // 타입별 집계
        int fireCnt = 0, waterCnt = 0, windCnt = 0, earthCnt = 0;
        bool fireAllEven = true, waterAllOdd = true;

        for (int i = 0; i < n; i++)
        {
            switch (types[i])
            {
                case ElementType.Fire:
                    fireCnt++;
                    if (dice[i] % 2 != 0) fireAllEven = false;
                    break;
                case ElementType.Water:
                    waterCnt++;
                    if (dice[i] % 2 != 1) waterAllOdd = false;
                    break;
                case ElementType.Wind:
                    windCnt++;
                    break;
                case ElementType.Earth:
                    earthCnt++;
                    break;
                case ElementType.Fired:
                    // Fired는 개수 카운트보다는 점수 계산 시 처리
                    break;
            }
        }

        // 모든 후보를 동일 선상에서 비교
        int bestScore = 0;
        string bestName = "";
        int bestPriority = -1;

        // ── 야치 조합 (priority 0~9) ──
        // 주사위 개수에 맞게 동적 적용

        string[] upperNames = { "", "Ace", "Two", "Three", "Four", "Five", "Six" };
        int upperTotal = 0;
        for (int v = 1; v <= 6; v++)
        {
            int s = counts[v] * v;
            upperTotal += s;
            TrySet(s, upperNames[v], 0, ref bestScore, ref bestName, ref bestPriority);
        }

        // Upper Bonus 기준: 주사위 수 비례 (5개=63, 3개=38, 7개=89 등)
        int upperThreshold = Mathf.CeilToInt(n * 12.6f);
        if (upperTotal >= upperThreshold)
            TrySet(upperTotal + 35, "Upper Bonus", 1, ref bestScore, ref bestName, ref bestPriority);

        if (n >= 2)
        {
            for (int v = 6; v >= 1; v--)
            {
                if (counts[v] >= 2) { TrySet(v * 2, "One Pair", 2, ref bestScore, ref bestName, ref bestPriority); break; }
            }
        }

        if (n >= 4)
        {
            int pairCount = 0, pairSum = 0;
            for (int v = 6; v >= 1; v--) { if (counts[v] >= 2) { pairCount++; pairSum += v * 2; } }
            if (pairCount >= 2) TrySet(pairSum, "Two Pairs", 3, ref bestScore, ref bestName, ref bestPriority);
        }

        if (n >= 3)
        {
            for (int v = 1; v <= 6; v++) { if (counts[v] >= 3) TrySet(total, "Three of a Kind", 4, ref bestScore, ref bestName, ref bestPriority); }
        }

        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++) { if (counts[v] >= 4) TrySet(total, "Four of a Kind", 5, ref bestScore, ref bestName, ref bestPriority); }
        }

        if (n >= 5)
        {
            if (IsFullHouse(counts)) TrySet(25, "Full House", 6, ref bestScore, ref bestName, ref bestPriority);
        }

        if (n >= 4)
        {
            if (IsSmallStraight(counts)) TrySet(30, "Small Straight", 7, ref bestScore, ref bestName, ref bestPriority);
        }

        if (n >= 5)
        {
            if (IsLargeStraight(counts)) TrySet(40, "Large Straight", 8, ref bestScore, ref bestName, ref bestPriority);
        }

        // All of a Kind: 모든 주사위가 같은 값 (4개 이상부터)
        if (n >= 4)
        {
            for (int v = 1; v <= 6; v++)
            {
                if (counts[v] >= n)
                {
                    int s = hasYahtzee ? 50 + n * 20 : 10 * n;
                    string nm = hasYahtzee ? "All of a Kind+" : "All of a Kind";
                    TrySet(s, nm, 9, ref bestScore, ref bestName, ref bestPriority);
                    hasYahtzee = true;
                }
            }
        }

        // ── 원소 (priority 10~13) ──

        // Fire + Fired 합산
        // Fire: 개당 (눈금+2), Fired: 개당 (눈금×2)
        // Fire Synergy(Fire 2+, 전부 짝수) 시 Fire 부분만 ×2, Fired는 그대로 합산
        {
            int fireBase = 0;
            int firedBase = 0;
            int firedCount = 0;

            for (int i = 0; i < n; i++)
            {
                if (types[i] == ElementType.Fire)
                    fireBase += dice[i] + 2;
                else if (types[i] == ElementType.Fired)
                {
                    firedBase += dice[i] * 2;
                    firedCount++;
                }
            }

            bool isSynergy = fireAllEven && fireCnt >= 2;
            int fireScore = isSynergy ? fireBase * 2 : fireBase;

            // Fire 시너지 시 Fire 주사위에도 Fired 보너스 (눈금×2) 추가
            int fireFiredBonus = 0;
            if (isSynergy)
            {
                for (int i = 0; i < n; i++)
                {
                    if (types[i] == ElementType.Fire)
                        fireFiredBonus += dice[i] * 2;
                }
            }

            int combinedScore = fireScore + firedBase + fireFiredBonus;

            if (fireCnt > 0 || firedCount > 0)
            {
                string label = isSynergy ? "Fire Synergy" : (fireCnt > 0 ? "Fire" : "Fired");
                TrySet(combinedScore, label, 10, ref bestScore, ref bestName, ref bestPriority);
            }
        }

        // Water: 개당 (눈금+1), 전부 홀수면 ×2
        if (waterCnt > 0)
        {
            int waterTotal = 0;
            for (int i = 0; i < n; i++)
            {
                if (types[i] == ElementType.Water)
                    waterTotal += dice[i] + 1;
            }
            if (waterAllOdd && waterCnt >= 2)
                waterTotal *= 2;
            TrySet(waterTotal, waterAllOdd && waterCnt >= 2 ? "Water Synergy" : "Water", 11, ref bestScore, ref bestName, ref bestPriority);
        }

        // Wind: 개별 +0, 전부 합산이 시너지 점수
        if (windCnt > 0)
        {
            int windTotal = 0;
            // 시너지(2개 이상)일 때만 점수 합산, 아니면 0
            if (windCnt >= 2)
            {
                for (int i = 0; i < n; i++)
                {
                    if (types[i] == ElementType.Wind)
                        windTotal += dice[i];
                }
            }
            
            // 1개일 때는 0점이므로 사실상 무의미하지만, 시너지 발동 여부는 표기
            TrySet(windTotal, windCnt >= 2 ? "Wind Synergy" : "Wind", 12, ref bestScore, ref bestName, ref bestPriority);
        }

        // Earth: 개당 10 고정
        if (earthCnt > 0)
        {
            int earthTotal = earthCnt * 10;
            TrySet(earthTotal, earthCnt >= 2 ? "Earth Synergy" : "Earth", 13, ref bestScore, ref bestName, ref bestPriority);
        }

        // ── 시너지 플래그 설정 (이긴 시너지만 활성) ──
        synFire  = bestName == "Fire Synergy";
        synWater = bestName == "Water Synergy";
        synWind  = bestName == "Wind Synergy";
        synEarth = bestName == "Earth Synergy";

        log.Add(bestName + " +" + bestScore);
        return bestScore;
    }

    void TrySet(int score, string name, int priority, ref int bestScore, ref string bestName, ref int bestPriority)
    {
        if (score > bestScore || (score == bestScore && priority > bestPriority))
        {
            bestScore = score;
            bestName = name;
            bestPriority = priority;
        }
    }

    // ===== 개별 주사위 기여도 =====
    public int GetDiceContribution(int diceIndex)
    {
        if (role == null || role.DiceObjects == null) return 0;
        if (role.IsRolling || !hasRolled) return 0;

        int n = role.diceCount;
        int[] dice = new int[n];
        ElementType[] types = new ElementType[n];

        for (int i = 0; i < n; i++)
        {
            dice[i] = role.GetDiceValue(i);
            types[i] = (diceType != null) ? diceType.GetDiceType(i) : ElementType.Standard;
        }

        int totalScore = CalcTotalScore(dice, types, new List<string>());
        return Mathf.RoundToInt((float)totalScore / n);
    }

    bool IsFullHouse(int[] counts)
    {
        bool hasThree = false, hasTwo = false;
        for (int v = 1; v <= 6; v++)
        {
            if (counts[v] == 3) hasThree = true;
            if (counts[v] == 2) hasTwo = true;
        }
        return hasThree && hasTwo;
    }

    bool IsSmallStraight(int[] counts)
    {
        if (counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1) return true;
        if (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) return true;
        if (counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1) return true;
        return false;
    }

    bool IsLargeStraight(int[] counts)
    {
        if (counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) return true;
        if (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1) return true;
        return false;
    }
}
