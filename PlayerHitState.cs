using Unity.VisualScripting;
using UnityEngine;

public class PlayerHitState : PlayerActionState
{
    private bool isInvincible = false;

    public PlayerHitState(PlayerActionStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void EnterState()
    {
        stateMachine.player.Animator.SetTrigger("isHit");

        stateMachine.ReusableData.CanMove = false;

        stateMachine.player.Rigidbody.velocity = Vector3.zero;

        isInvincible = true;
    }

    public override void ExitState()
    {
        isInvincible = false;

        stateMachine.ReusableData.CanMove = true;
    }

    public override void OnAnimationTransitionEvent()
    {
        isInvincible = false;
    }

    protected override void OnHitEnter(Collider collider)
    {
        if (isInvincible)
        {
            return;
        }

        base.OnHitEnter(collider);
    }
}
