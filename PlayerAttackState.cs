using UnityEngine;

public class PlayerAttackState : BattleState
{
    private int attackCount = 0;

    public PlayerAttackState(PlayerActionStateMachine playerActionStateMachine) : base(playerActionStateMachine)
    {
    }

    #region IState Methods

    public override void EnterState()
    {
        base.EnterState();

        stateMachine.ReusableData.CanMove = false;

        stateMachine.player.Rigidbody.velocity = Vector3.zero;

        stateMachine.player.Animator.SetTrigger("OnCloseAttackCombo");

        stateMachine.player.inputBuffer.ClearLastInput();

        stateMachine.player.Animator.applyRootMotion = true;
    }

    public override void ExitState()
    {
        base.ExitState();

        stateMachine.ReusableData.CanMove = true;

        stateMachine.player.Animator.ResetTrigger("OnCloseAttackCombo");

        stateMachine.ReusableData.Direction = Vector3.zero;

        stateMachine.player.Animator.applyRootMotion = false;

        stateMachine.player.WeaponCollider.enabled = false;
    }

    public override void Update()
    {
        ReadMovementInput();
    }

    public override void PhysicsUpdate()
    {
        RotatePlayer();
    }

    #endregion

    #region Main Methods

    private void RotatePlayer()
    {
        Vector3 movementDirection = GetMovementInputDirection();

        stateMachine.ReusableData.Direction = movementDirection;
    }

    protected Vector3 GetMovementInputDirection()
    {
        Vector2 input = stateMachine.ReusableData.MovementInput;
        Vector3 forward = stateMachine.player.MainCameraTransform.forward;
        Vector3 right = stateMachine.player.MainCameraTransform.right;

        forward.y = 0; // 카메라의 수평 방향만 고려
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        return forward * input.y + right * input.x;
    }

    void ReadMovementInput()
    {
        stateMachine.ReusableData.MovementInput = stateMachine.player.Input.MovementActions.Movement.ReadValue<Vector2>();
    }
    #endregion
}
