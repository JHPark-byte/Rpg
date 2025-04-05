using Cinemachine;
using UnityEngine;

public class PlayerParryState : PlayerActionState
{
    public PlayerParryState(PlayerActionStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();

        StartAnimation(stateMachine.player.AnimationsData.DefenseParameterHash);

        stateMachine.player.ParryCollider.enabled = true;

        stateMachine.ReusableData.CanMove = false;
    }

    public override void ExitState()
    {
        base.ExitState();

        StopAnimation(stateMachine.player.AnimationsData.DefenseParameterHash);

        stateMachine.player.ParryCollider.enabled = false;

        stateMachine.ReusableData.CanMove = true;
    }

    public override void OnAnimationExitEvent()
    {
        base.OnAnimationExitEvent();

        stateMachine.SetState(stateMachine.DefenseState);
        stateMachine.player.HitBoxCollider.enabled = true;
        stateMachine.player.ParryCollider.enabled = false;
    }

    protected override void OnHitEnter(Collider collider)
    {
        Debug.Log("패링 성공");
        // 파티클 매니저를 통해 파티클 재생
        if (ParticleManager.instance != null)
        {
            stateMachine.player.ParryCollider.enabled = false;

            stateMachine.player.Animator.SetTrigger("Player_Parry");
            ParticleManager.instance.PlayParticle("Parry", collider.transform.position);
            AudioManager.instance.PlaySFX("ParrySound");

            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.HandleParry();
            }
        }
    }
}
