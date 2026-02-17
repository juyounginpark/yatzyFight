using UnityEngine;

public static class FightSetupData
{
    public static GameObject[] enemyPrefabs;
    public static Vector3[] enemyScales;

    public static void Clear()
    {
        enemyPrefabs = null;
        enemyScales = null;
    }
}
