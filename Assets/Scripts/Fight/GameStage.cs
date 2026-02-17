using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameStage : MonoBehaviour
{
    [Header("스폰 설정")]
    public Transform spawnPoint;
    public float enemySpacing = 2f;

    [Header("전투 종료")]
    public float clearDelay = 1.5f;

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private GameFlow gameFlow;
    private bool checkingClear;

    void Start()
    {
        gameFlow = FindObjectOfType<GameFlow>();
        StartCoroutine(SpawnFromSetupData());
    }

    void Update()
    {
        if (!checkingClear) return;

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

    IEnumerator SpawnFromSetupData()
    {
        ClearSpawnedEnemies();

        GameObject[] prefabs = FightSetupData.enemyPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("[GameStage] FightSetupData에 적 데이터가 없음!");
            yield break;
        }

        Vector3 center = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        int count = prefabs.Length;

        // 카메라 방향으로 회전
        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            if (prefabs[i] == null) continue;

            float x = (i - (count - 1) / 2f) * enemySpacing;
            Vector3 pos = center + new Vector3(x, 0f, 0f);

            // 카메라 방향으로 Y축 회전
            Vector3 lookDir = camPos - pos;
            lookDir.y = 0f;
            Quaternion rot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir)
                : Quaternion.identity;

            GameObject enemy = Instantiate(prefabs[i], pos, rot);

            if (FightSetupData.enemyScales != null && i < FightSetupData.enemyScales.Length)
                enemy.transform.localScale = FightSetupData.enemyScales[i];

            spawnedEnemies.Add(enemy);
        }

        FightSetupData.Clear();
        checkingClear = true;
    }

    void OnStageClear()
    {
        StartCoroutine(ReturnToMain());
    }

    IEnumerator ReturnToMain()
    {
        yield return new WaitForSeconds(clearDelay);
        SceneManager.LoadScene("Main");
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
