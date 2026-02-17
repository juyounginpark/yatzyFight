using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class EnemySpawnData
{
    public GameObject enemyPrefab;
    public Transform spawnPoint;
}

[System.Serializable]
public class StageData
{
    public string stageName;
    public EnemySpawnData[] enemies;
}

public class GameStage : MonoBehaviour
{
    [Header("스테이지 데이터베이스")]
    public StageData[] stages;

    [Header("스테이지 전환")]
    public float nextStageDelay = 1.5f;

    private int currentStageIndex = -1;
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private GameFlow gameFlow;
    private bool checkingClear;

    public int CurrentStageIndex => currentStageIndex;
    public int TotalStages => stages != null ? stages.Length : 0;

    void Start()
    {
        gameFlow = FindObjectOfType<GameFlow>();
        StartNextStage();
    }

    void Update()
    {
        if (!checkingClear) return;

        // 모든 적이 죽었는지 체크
        bool allDead = true;
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            if (spawnedEnemies[i] != null)
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
        {
            checkingClear = false;
            OnStageClear();
        }
    }

    void StartNextStage()
    {
        currentStageIndex++;

        if (stages == null || currentStageIndex >= stages.Length)
        {
            // 모든 스테이지 클리어
            OnAllStagesClear();
            return;
        }

        StartCoroutine(SpawnStage(stages[currentStageIndex]));
    }

    IEnumerator SpawnStage(StageData stage)
    {
        // 기존 적 정리
        ClearSpawnedEnemies();

        yield return new WaitForSeconds(0.5f);

        // 적 스폰
        for (int i = 0; i < stage.enemies.Length; i++)
        {
            EnemySpawnData data = stage.enemies[i];
            if (data.enemyPrefab == null) continue;

            Vector3 pos = data.spawnPoint != null ? data.spawnPoint.position : Vector3.zero;
            GameObject enemy = Instantiate(data.enemyPrefab, pos, Quaternion.identity);
            spawnedEnemies.Add(enemy);
        }

        checkingClear = true;
    }

    void OnStageClear()
    {
        StartCoroutine(NextStageSequence());
    }

    IEnumerator NextStageSequence()
    {
        yield return new WaitForSeconds(nextStageDelay);
        StartNextStage();
    }

    void OnAllStagesClear()
    {
        // 게임 클리어 (추후 확장)
        Debug.Log("All stages cleared!");
    }

    void ClearSpawnedEnemies()
    {
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            if (spawnedEnemies[i] != null)
                Destroy(spawnedEnemies[i]);
        }
        spawnedEnemies.Clear();
    }
}
