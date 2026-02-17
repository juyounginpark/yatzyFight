using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 5f;
    public float rotSpeed = 10f;

    [Header("달리기")]
    public float runSpeed = 9f;

    [Header("점프")]
    public float jumpForce = 14f;
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.1f;

    [Header("키 설정")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode interactKey = KeyCode.E;

    private Rigidbody rb;
    private bool isGrounded;
    private Transform cam;
    private bool interacted;
    private bool locked;
    private bool jumpRequest;

    private bool holdRun;
    private Vector3 moveDir;

    public bool IsGrounded => isGrounded;
    public bool IsMoving => moveDir.sqrMagnitude > 0.01f;
    public bool IsRunning => IsMoving && holdRun;
    public bool Interacted => interacted;
    public bool IsLocked => locked;

    public void Lock() => locked = true;
    public void Unlock() => locked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        cam = Camera.main.transform;
    }

    void Update()
    {
        CheckGround();

        interacted = false;

        if (locked)
        {
            moveDir = Vector3.zero;
            return;
        }

        float inputH = Input.GetAxisRaw("Horizontal");
        float inputV = Input.GetAxisRaw("Vertical");
        holdRun = Input.GetKey(runKey);

        Vector3 camF = cam.forward;
        Vector3 camR = cam.right;
        camF.y = 0f;
        camR.y = 0f;
        camF.Normalize();
        camR.Normalize();

        moveDir = (camF * inputV + camR * inputH).normalized;

        if (Input.GetKeyDown(jumpKey))
        {
            Debug.Log($"[Jump] Space pressed. isGrounded={isGrounded}");
            if (isGrounded)
                jumpRequest = true;
        }

        interacted = Input.GetKeyDown(interactKey);
    }

    void FixedUpdate()
    {
        if (locked) return;

        float speed = holdRun ? runSpeed : moveSpeed;
        Vector3 vel = moveDir * speed;
        vel.y = rb.linearVelocity.y;
        rb.linearVelocity = vel;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotSpeed * Time.fixedDeltaTime);
        }

        if (jumpRequest)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequest = false;
            Debug.Log("[Jump] Force applied!");
        }
    }

    void CheckGround()
    {
        // 단순하게: transform.position에서 아래로 레이캐스트
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.down * 0.2f);
    }
}
