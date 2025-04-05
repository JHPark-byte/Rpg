using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleState : PlayerActionState
{
    public override void EnterState()
    {
        
    }

    public override void ExitState() { }

    protected override void OnHitEnter(Collider collider)
    {
        base.OnHitEnter(collider);

        stateMachine.player.Animator.SetTrigger("PlayerHit2");
    }

    public BattleState(PlayerActionStateMachine playerActionStateMachine) : base(playerActionStateMachine)
    {
    }
}
