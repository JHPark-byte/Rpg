using UnityEngine;

public class PlayerKillMoveState : BattleState
{
    private Enemy targetEnemy;

    public PlayerKillMoveState(PlayerActionStateMachine playerActionStateMachine) : base(playerActionStateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();

        stateMachine.player.Rigidbody.velocity = Vector3.zero;
        stateMachine.player.Input.DisableAllPlayerActions();

        targetEnemy.bodyCollider.enabled = false;
        stateMachine.player.HitBoxCollider.enabled = false;

        SetKillMove();

        Time.timeScale = 1f;

        StartAnimation(stateMachine.player.AnimationsData.KillMoveParameterHash);
    }

    public override void ExitState()
    {
        base.ExitState();

        stateMachine.player.Input.EnablePlayerActions();

        stateMachine.player.HitBoxCollider.enabled = true;

        StopAnimation(stateMachine.player.AnimationsData.KillMoveParameterHash);
    }

    public override void OnAnimationExitEvent()
    {
        base.OnAnimationExitEvent();

        stateMachine.SetState(stateMachine.WeaponDrawnState);
    }

    protected override void OnHitEnter(Collider collider)
    {
        
    }

    public void SetTarget(Enemy enemy)
    {
        targetEnemy = enemy;
    }

    private void SetKillMove()
    {
        targetEnemy.TriggerKillmove();
        targetEnemy.MoveToKillMovePosition(stateMachine.player.transform.position, stateMachine.player.transform.rotation);
                                                                   
        stateMachine.player.Animator.SetTrigger(targetEnemy.KillMoveData.playerAnimationTrigger);
    }
}
