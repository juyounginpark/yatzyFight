using UnityEngine;
using TMPro;

[ExecuteInEditMode]
public class DiceUI : MonoBehaviour
{
    [Header("참조")]
    public Role role;
    public TextMeshProUGUI resultText;

    private bool wasRolling;
    private bool hasRolled; // 최초 롤 여부
    private float rollTickTimer;
    private bool hasYahtzee; // 야치 기록 여부 (보너스용)
    private Choice choice;
    private int[] lockedValues; // lock된 주사위의 고정 숫자
    private int lastScore;

    public int LastScore => lastScore;

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

            // 롤 중 랜덤 숫자 표시
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
            UpdateUI();
            wasRolling = false;
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

            // lock된 주사위는 고정 숫자 유지
            if (choice != null && choice.IsDiceLocked(i))
                values += hasRolled ? lockedValues[i].ToString() : "?";
            else
                values += Random.Range(1, 7);
        }
        resultText.text = values + "\nscore : --";
    }

    void UpdateUI()
    {
        if (role == null || role.DiceObjects == null || resultText == null) return;

        int[] dice = new int[role.diceCount];
        string values = "";

        for (int i = 0; i < role.diceCount; i++)
        {
            dice[i] = role.GetDiceValue(i);
            lockedValues[i] = dice[i]; // 현재 값 저장 (lock용)
            if (i > 0) values += " ";
            values += dice[i];
        }

        string bestName;
        int bestScore;
        GetBestScore(dice, out bestName, out bestScore);

        lastScore = bestScore;
        resultText.text = values + "\nscore : " + bestScore + "\n" + bestName;
    }

    // ===== 점수 계산 =====
    // 우선순위(priority)가 높을수록 동점 시 우선
    void GetBestScore(int[] dice, out string bestName, out int bestScore)
    {
        bestName = "";
        bestScore = 0;
        int bestPriority = -1;

        int[] counts = new int[7]; // index 1~6
        int total = 0;

        for (int i = 0; i < dice.Length; i++)
        {
            counts[dice[i]]++;
            total += dice[i];
        }

        // --- 상단 (Ace ~ Six) - priority 0 ---
        string[] upperNames = { "", "Ace", "Two", "Three", "Four", "Five", "Six" };
        int upperTotal = 0;

        for (int n = 1; n <= 6; n++)
        {
            int score = counts[n] * n;
            upperTotal += score;
            TrySet(score, upperNames[n], 0, ref bestScore, ref bestName, ref bestPriority);
        }

        // 상단 보너스 (63점 이상이면 +35) - priority 1
        if (upperTotal >= 63)
            TrySet(upperTotal + 35, "Upper Bonus", 1, ref bestScore, ref bestName, ref bestPriority);

        // --- One Pair - 같은 숫자 2개, 해당 페어 합 - priority 2 ---
        for (int n = 6; n >= 1; n--)
        {
            if (counts[n] >= 2)
            {
                TrySet(n * 2, "One Pair", 2, ref bestScore, ref bestName, ref bestPriority);
                break; // 가장 높은 페어만
            }
        }

        // --- Two Pairs - 서로 다른 페어 2개, 두 페어 합 - priority 3 ---
        {
            int pairCount = 0;
            int pairSum = 0;
            for (int n = 6; n >= 1; n--)
            {
                if (counts[n] >= 2)
                {
                    pairCount++;
                    pairSum += n * 2;
                }
            }
            if (pairCount >= 2)
                TrySet(pairSum, "Two Pairs", 3, ref bestScore, ref bestName, ref bestPriority);
        }

        // --- Three of a Kind - 전체 합 - priority 4 ---
        for (int n = 1; n <= 6; n++)
        {
            if (counts[n] >= 3)
                TrySet(total, "Three of a Kind", 4, ref bestScore, ref bestName, ref bestPriority);
        }

        // --- Four of a Kind - 전체 합 - priority 5 ---
        for (int n = 1; n <= 6; n++)
        {
            if (counts[n] >= 4)
                TrySet(total, "Four of a Kind", 5, ref bestScore, ref bestName, ref bestPriority);
        }

        // --- Full House - 25점 - priority 6 ---
        if (IsFullHouse(counts))
            TrySet(25, "Full House", 6, ref bestScore, ref bestName, ref bestPriority);

        // --- Small Straight - 30점 - priority 7 ---
        if (IsSmallStraight(counts))
            TrySet(30, "Small Straight", 7, ref bestScore, ref bestName, ref bestPriority);

        // --- Large Straight - 40점 - priority 8 ---
        if (IsLargeStraight(counts))
            TrySet(40, "Large Straight", 8, ref bestScore, ref bestName, ref bestPriority);

        // --- Five of a Kind - 50점 - priority 9 ---
        for (int n = 1; n <= 6; n++)
        {
            if (counts[n] == 5)
            {
                int score = 50;
                string name = "Five of a Kind";

                if (hasYahtzee)
                {
                    score = 150;
                    name = "Five of a Kind + Bonus";
                }

                TrySet(score, name, 9, ref bestScore, ref bestName, ref bestPriority);
                hasYahtzee = true;
            }
        }
    }

    void TrySet(int score, string name, int priority, ref int bestScore, ref string bestName, ref int bestPriority)
    {
        // 점수가 높으면 무조건 채택, 동점이면 priority가 높은 쪽 우선
        if (score > bestScore || (score == bestScore && priority > bestPriority))
        {
            bestScore = score;
            bestName = name;
            bestPriority = priority;
        }
    }

    bool IsFullHouse(int[] counts)
    {
        bool hasThree = false;
        bool hasTwo = false;

        for (int n = 1; n <= 6; n++)
        {
            if (counts[n] == 3) hasThree = true;
            if (counts[n] == 2) hasTwo = true;
        }

        return hasThree && hasTwo;
    }

    bool IsSmallStraight(int[] counts)
    {
        // 1-2-3-4, 2-3-4-5, 3-4-5-6
        if (counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1) return true;
        if (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) return true;
        if (counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1) return true;
        return false;
    }

    bool IsLargeStraight(int[] counts)
    {
        // 1-2-3-4-5 또는 2-3-4-5-6
        if (counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1) return true;
        if (counts[2] >= 1 && counts[3] >= 1 && counts[4] >= 1 && counts[5] >= 1 && counts[6] >= 1) return true;
        return false;
    }
}
