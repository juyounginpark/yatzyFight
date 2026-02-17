using UnityEngine;
using System.Collections;

public class PlayerAnimator : MonoBehaviour
{
    private Animator anim;
    private PlayerMove move;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Interact = Animator.StringToHash("Interact");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        move = GetComponent<PlayerMove>();

        Debug.Log($"[PlayerAnimator] anim={anim != null}, move={move != null}");
    }

    void Update()
    {
        if (anim == null || move == null) return;

        anim.SetBool(IsMoving, move.IsMoving);
        anim.SetBool(IsRunning, move.IsRunning);
        anim.SetBool(IsGrounded, move.IsGrounded);

        if (move.IsGrounded && Input.GetKeyDown(move.jumpKey))
            anim.SetTrigger(Jump);

        if (move.Interacted)
        {
            anim.SetTrigger(Interact);
            move.Lock();
            StartCoroutine(WaitInteractEnd());
        }
    }

    IEnumerator WaitInteractEnd()
    {
        // 트랜지션 시작 대기
        yield return null;
        yield return null;

        // Interact 애니메이션이 끝날 때까지 대기
        while (true)
        {
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.normalizedTime >= 0.95f && !anim.IsInTransition(0))
                break;
            yield return null;
        }

        move.Unlock();
    }
}
