using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyFollow : MonoBehaviour
{
    [Header("감지")]
    public float detectRange = 10f;
    public float stopRange = 2f;

    [Header("이동")]
    public float moveSpeed = 3f;
    public float returnSpeed = 2f;

    [Header("회전")]
    public float rotSpeed = 5f;

    [Header("전투")]
    public GameObject fightPrefab;

    private Transform player;
    private Vector3 homePos;
    private Quaternion homeRot;
    private bool chasing;
    private static bool loading;

    void Start()
    {
        homePos = transform.position;
        homeRot = transform.rotation;
        loading = false;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            player = go.transform;
    }

    void Update()
    {
        if (player == null) return;

        float playerDist = Vector3.Distance(homePos, player.position);

        if (playerDist <= detectRange)
        {
            chasing = true;
            ChasePlayer();
        }
        else
        {
            chasing = false;
            ReturnHome();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (loading) return;
        if (!other.CompareTag("Player")) return;
        if (fightPrefab == null) return;

        loading = true;
        FightSetupData.enemyPrefabs = new GameObject[] { fightPrefab };
        FightSetupData.enemyScales = new Vector3[] { transform.localScale };
        SceneManager.LoadScene("FIGHT");
    }

    void ChasePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > stopRange)
            transform.position += dir.normalized * moveSpeed * Time.deltaTime;
    }

    void ReturnHome()
    {
        Vector3 dir = homePos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.05f)
        {
            transform.position = homePos;
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, rotSpeed * Time.deltaTime);
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
        transform.position += dir.normalized * returnSpeed * Time.deltaTime;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? homePos : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, stopRange);
    }
}
