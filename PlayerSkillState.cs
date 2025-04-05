using UnityEngine;

public class PlayerSkillState : BattleState
{
    public PlayerSkillState(PlayerActionStateMachine playerActionStateMachine) : base(playerActionStateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();

        StopAnimation(stateMachine.player.AnimationsData.MovingParameterHash);

        StartAnimation(stateMachine.player.AnimationsData.SkillParameterHash);

        stateMachine.player.Input.DisablePlayerActions();

        stateMachine.player.Animator.applyRootMotion = true;
    }

    public override void ExitState()
    {
        base.ExitState();

        StopAnimation(stateMachine.player.AnimationsData.SkillParameterHash);

        stateMachine.player.Input.EnablePlayerActions();

        stateMachine.player.Animator.applyRootMotion = false;
    }

    protected override void OnHitEnter(Collider collider)
    {
        int damage = CalculateDamage(collider);

        ApplyDamge(damage);
    }

    public override void OnAnimationExitEvent()
    {
        base.OnAnimationExitEvent();

        stateMachine.SetState(stateMachine.WeaponDrawnState);
    }
}
