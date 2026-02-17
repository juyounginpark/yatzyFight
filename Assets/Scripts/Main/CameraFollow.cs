using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("타겟")]
    public Transform target;

    [Header("오프셋")]
    public Vector3 offset = new Vector3(0f, 10f, -6f);

    [Header("따라가기")]
    public float posSmooth = 0.1f;

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
    private Vector3 pivotVel;
    private Vector3 smoothPivot;

    void Start()
    {
        if (target == null)
            return;

        dist = offset.magnitude;
        Vector3 dir = offset.normalized;
        pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;

        smoothPivot = target.position;
        ApplyOrbit();
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

        // 피벗(플레이어 위치)만 부드럽게 따라감
        smoothPivot = Vector3.SmoothDamp(smoothPivot, target.position, ref pivotVel, posSmooth);

        // 궤도 회전은 피벗 기준으로 즉시 적용
        ApplyOrbit();
    }

    void ApplyOrbit()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 orbitOffset = rot * new Vector3(0f, 0f, -dist);

        transform.position = smoothPivot + orbitOffset;
        transform.LookAt(smoothPivot);
    }
}
