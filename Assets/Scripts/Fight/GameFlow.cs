using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum TurnPhase
{
    PlayerTurn,
    EnemyTurn
}

public class GameFlow : MonoBehaviour
{
    [Header("참조")]
    public HP playerHP;

    [Header("타이밍")]
    public float enemyPhaseDelay = 0.5f;

    private TurnPhase currentPhase = TurnPhase.PlayerTurn;
    private List<EnemyAttack> enemies = new List<EnemyAttack>();
    private Role role;
    private Choice choice;
    private RetryUI retryUI;

    public TurnPhase CurrentPhase => currentPhase;
    public bool CanPlayerAct => currentPhase == TurnPhase.PlayerTurn;

    void Start()
    {
        role = FindObjectOfType<Role>();
        choice = FindObjectOfType<Choice>();
        retryUI = FindObjectOfType<RetryUI>();
    }

    public void RegisterEnemy(EnemyAttack enemy)
    {
        if (!enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyAttack enemy)
    {
        enemies.Remove(enemy);
    }

    public void OnPlayerAttackCompleted()
    {
        StartCoroutine(EnemyPhase());
    }

    IEnumerator EnemyPhase()
    {
        currentPhase = TurnPhase.EnemyTurn;

        yield return new WaitForSeconds(enemyPhaseDelay);

        // 살아있는 적만, 주사위 많은 순서대로
        var alive = enemies
            .Where(e => e != null && !e.IsDead)
            .OrderByDescending(e => e.CurrentDiceCount)
            .ToList();

        foreach (var enemy in alive)
        {
            if (enemy == null || enemy.IsDead) continue;
            yield return enemy.ExecuteAttack();
        }

        StartPlayerPhase();
    }

    void StartPlayerPhase()
    {
        currentPhase = TurnPhase.PlayerTurn;

        if (retryUI != null)
            retryUI.ResetRetries();

        if (choice != null)
            choice.UnlockAll();

        if (role != null && !role.IsRolling)
            role.RollAllDice();
    }
}
