using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("타겟")]
    public Transform target;

    [Header("오프셋")]
    public Vector3 offset = new Vector3(0f, 10f, -6f);

    [Header("따라가기")]
    public float followSpeed = 8f;

    [Header("우클릭 시점 회전")]
    public float mouseSensX = 3f;
    public float mouseSensY = 1.5f;
    public float minPitch = 10f;
    public float maxPitch = 80f;

    [Header("줌")]
    public float zoomSpeed = 3f;
    public float minDist = 4f;
    public float maxDist = 20f;

    private float yaw;
    private float pitch;
    private float dist;

    void Start()
    {
        if (target == null)
            return;

        // 초기 오프셋에서 yaw, pitch, dist 계산
        dist = offset.magnitude;
        Vector3 dir = offset.normalized;
        pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // 우클릭 홀드 시 시점 회전
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensX;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // 마우스 휠 줌
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            dist = Mathf.Clamp(dist - scroll * zoomSpeed, minDist, maxDist);

        // yaw, pitch, dist로 오프셋 재계산
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredOffset = rot * new Vector3(0f, 0f, -dist);

        Vector3 desiredPos = target.position + desiredOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
        transform.LookAt(target.position);
    }
}
