using UnityEngine;

public class PlayerDefenseState : BattleState
{
    public PlayerDefenseState(PlayerActionStateMachine playerActionStateMachine) : base(playerActionStateMachine)
    {

    }

    public override void EnterState()
    {
        base.EnterState();

        StartAnimation(stateMachine.player.AnimationsData.DefenseParameterHash);

        stateMachine.ReusableData.CanMove = false;

        stateMachine.player.Rigidbody.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();
    }

    public override void ExitState()
    {
        base.ExitState();

        stateMachine.ReusableData.CanMove = true;

        StopAnimation(stateMachine.player.AnimationsData.DefenseParameterHash);
    }

    protected override void OnHitEnter(Collider collider)
    {
        stateMachine.player.Animator.CrossFade("HitOnDefense", 0.1f);
        ApplyDamge(CalculateDamage(collider));
        ApplyKnockback(collider.transform, 1.2f);

        AudioManager.instance.PlaySFX("BlockSound");

        Debug.Log("현재 체력 + " + stateMachine.player.Stat.stats.Health.CurrentValue);
    }

    protected override int CalculateDamage(Collider collider)
    {
        int defendDamge = base.CalculateDamage(collider) / 2;
        return defendDamge;
    }

    public override void OnAnimationEnterEvent()
    {
        base.OnAnimationEnterEvent();

        stateMachine.SetState(stateMachine.ParryState);
        stateMachine.player.HitBoxCollider.enabled = false;
    }

    void ApplyKnockback(Transform enemyTransform, float knockbackForce)
    {
        Vector3 knockbackDirection = -enemyTransform.forward;

        knockbackDirection.y = 0;
        knockbackDirection.Normalize();

        stateMachine.player.Rigidbody.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
    }
}
