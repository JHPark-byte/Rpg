using System.Collections;
using UnityEngine;
using BehaviorDesigner.Runtime;
using UnityEngine.AI;

public class Enemy : MonoBehaviour, IEnemy
{
    [field: Header("Reference")]
    [SerializeField] private EnemySO enemyData;
    [SerializeField] private KillMoveData killMoveData;

    [field: Header("UI Offset")]
    [SerializeField] private Vector3 hpBarOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector3 postureOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private Vector3 alertOffset = new Vector3(0f, 2.5f, 0f);

    [SerializeField] private ExternalBehavior originalBehaviorAsset;

    public EnemySO EnemyData { get => enemyData; set => enemyData = value; }
    public KillMoveData KillMoveData { get => killMoveData; set => killMoveData = value; }

    [field: Header("Status")]
    public EnemyState currentState;
    private int currentHealth;
    private float currentPosture;
    private float targetHealth;
    private float targetPosture;
    public bool IsParryGuageFull = false;
    private bool isGaugeForced = false;
    private float lerpSpeed = 5f;

    [field: Header("UI")]
    public EnemyGauge enemyGauge;
    public float guageDuration = 3.0f;
    private Coroutine gaugeCoroutine = null;

    [field: Header("Component")]
    public Animator anim;
    public Rigidbody rb;
    public Collider bodyCollider;
    public Collider weaponCollider;
    private FieldOfView fov;
    private BehaviorTree behaviorTree;
    public NavMeshAgent agent;

    [field: Header("External Target")]
    public GameObject itemPrefab;
    private Transform dropPoint;
    public Transform marker;
    public Transform playerTarget { get; private set; }
    public bool canRotateToPlayer = false;

    [field: Header("AI")]
    public Transform waypointRoot;
    public int savedWaypointIndex = 0;
    public bool savedWaypointForward = true;
    private Coroutine alertCoroutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        fov = GetComponent<FieldOfView>();
        behaviorTree = GetComponent<BehaviorTree>();
        agent = GetComponent<NavMeshAgent>();

        anim.applyRootMotion = true;
    }

    void Start()
    {
    }

    void OnEnable()
    {
        GameEventsManager.instance.enemyEvents.onRecoveryPosture += RecoverPosture;
        fov.OnVisibilityChanged += HandleVisibilityChanged;

        currentHealth = enemyData.MaxHealth;
        currentPosture = 0;
        targetHealth = currentHealth;

        enemyGauge = EnemyGaugeManager.instance.GetEnemyGauge();
        
        if (enemyGauge != null)
        {
            enemyGauge.healthBar.maxValue = enemyData.MaxHealth;
            enemyGauge.healthBar.value = currentHealth;
        }
        else
        {
            Debug.LogError("HealthBar�� �Ҵ���� �ʾҽ��ϴ�.");
        }
    }

    void OnDisable()
    {
        GameEventsManager.instance.enemyEvents.onRecoveryPosture -= RecoverPosture;
        fov.OnVisibilityChanged -= HandleVisibilityChanged;
    }

    private void Update()
    {
        if (enemyGauge != null && enemyGauge.healthBar.gameObject.activeSelf)
        {
            enemyGauge.healthBar.transform.position = transform.position + hpBarOffset;
            enemyGauge.postureGauge.transform.position = transform.position + postureOffset;

            enemyGauge.healthBar.transform.LookAt(Camera.main.transform);
            enemyGauge.postureGauge.transform.LookAt(Camera.main.transform);
        }

        if (enemyGauge.healthBar.value != targetHealth)
        {
            enemyGauge.healthBar.value = Mathf.Lerp(enemyGauge.healthBar.value, targetHealth, Time.deltaTime * lerpSpeed);
        }
    }

    void FixedUpdate()
    {
        Debug.Log("���� canRotateToPlayer = " + canRotateToPlayer);

        if (canRotateToPlayer && playerTarget != null && agent.isStopped)
        {
            Debug.Log("�߰���...");
            Vector3 dir = (playerTarget.position - transform.position).normalized;
            dir.y = 0;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                float angleDifference = Quaternion.Angle(rb.rotation, lookRot);

                if (angleDifference > 2f)
                {
                    Quaternion smoothRot = Quaternion.Slerp(rb.rotation, lookRot, Time.deltaTime * 20f);
                    rb.MoveRotation(smoothRot);
                }
            }
        }
    }

    void LateUpdate()
    {
        if (enemyGauge == null) return;

        if (enemyGauge.alertIcon != null && enemyGauge.alertIcon.activeSelf)
        {
            enemyGauge.alertIcon.transform.position = transform.position + alertOffset;
            enemyGauge.alertIcon.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        }
    }

    private void HandleVisibilityChanged(bool isVisible, GameObject player)
    {
        if (isVisible)
        {
            playerTarget = player.transform;

            if (currentState != EnemyState.Battle)
            {
                ChangeState(EnemyState.Battle);
            }
        }
        else
        {
            if (currentState != EnemyState.Idle)
            {
                ChangeState(EnemyState.Idle);
            }

            playerTarget = null;
        }
    }

    private void ChangeState(EnemyState newState, bool immediate = false)
    {
        if (currentState == newState) return;

        currentState = newState;
        Debug.Log($"[Enemy] ���� ��ȯ: {newState}");

        switch (newState)
        {
            case EnemyState.Idle:
                canRotateToPlayer = false;
                GameEventsManager.instance.enemyEvents.ExitBattleMusic();
                fov.SetViewDistance(150, 3f);
                break;

            case EnemyState.Battle:
                if (!immediate)
                {
                    if (alertCoroutine != null) StopCoroutine(alertCoroutine);
                    alertCoroutine = StartCoroutine(AlertSequence());
                }
                // �ǰݽÿ��� ��� �����¼��� ��ȯ
                canRotateToPlayer = true;
                GameEventsManager.instance.enemyEvents.EnterBattleMusic();
                fov.SetViewDistance(360, 15f);
                break;
        }
    }

    public void TakeDamage(int damageAmount)
    {
        ChangeState(EnemyState.Battle, true);

        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        anim.SetTrigger("EnemyHit");

        if (isGaugeForced) StartCoroutine(ResetTreeAfterDelay(1f)); // �������� ��� �ߵ����� ����

        if (gaugeCoroutine != null)
        {
            StopCoroutine(gaugeCoroutine);
        }

        gaugeCoroutine = StartCoroutine(ActivateForSeconds(guageDuration));

        currentHealth -= damageAmount; // ���� ü���� ����
        currentHealth = Mathf.Clamp(currentHealth, 0, enemyData.MaxHealth);

        targetHealth = currentHealth;

        if (currentHealth <= 0)
        {
            Die(this);
        }
    }

    public void TakePostureDamage(int baseDamage)
    {
        StartCoroutine(ResetTreeAfterDelay(1f));

        int actualDamage = Mathf.Max(0, baseDamage - enemyData.PostureResistance);

        targetPosture = Mathf.Clamp(currentPosture + actualDamage, 0, 200);

        StartCoroutine(UpdatePostureBar());

        Debug.Log("targetPosture = " + targetPosture);

        if (targetPosture >= 200)
        {
            IsParryGuageFull = true;

            AudioManager.instance.PlaySFX("KillMoveNotice");

            GameEventsManager.instance.enemyEvents.PostureBreak(this);
        }
    }

    public void HandleParry()
    {
        // �ִϸ��̼� ����
        anim.SetTrigger("Enemy_Parried");

        // ���� �ݶ��̴� ��Ȱ��ȭ
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        // ü�� ������ �϶�
        TakePostureDamage(100);
    }

    private void RecoverPosture()
    {
        Debug.Log("ü�� ȸ�� ����!");
        IsParryGuageFull = false;

        float recoverAmount = 50;
        targetPosture = Mathf.Max(0, targetPosture - recoverAmount);

        StartCoroutine(UpdatePostureBar());

        Debug.Log("ü�� ������ ȸ����!");
    }

    public void Die()
    {
        Die(this);
    }

    // ��� �� ȣ��Ǵ� �޼���
    public void Die(IEnemy enemy)
    {
        // ���� ��� ó�� �ۼ� (��: ���� ȿ��, ���� ��� ��)
        GameEventsManager.instance.enemyEvents.EnemyDie(this);

        if (enemyGauge != null)
        {
            enemyGauge.ShowAlertIcon(false);
        }

        DropItem();

        EnemyGaugeManager.instance.ReturnEnemyGuage(enemyGauge); // ü�¹� ��ȯ
        GameEventsManager.instance.enemyEvents.ExitBattleMusic();
        Destroy(gameObject); // ���� ���� ������Ʈ �ı�
    }

    IEnumerator ActivateForSeconds(float seconds)
    {
        enemyGauge.SetActive(true);

        yield return new WaitForSeconds(seconds);

        enemyGauge.SetActive(false);
        gaugeCoroutine = null;
    }

    public void ActivateGauge()
    {
        isGaugeForced = true;
        enemyGauge.SetActive(true);
    }

    public void DeActivateHealthBar()
    {
        isGaugeForced = false;
        enemyGauge.SetActive(false);
    }

    private IEnumerator UpdatePostureBar()
    {
        float duration = 0.4f; // �ε巯�� �ִϸ��̼� �ð�
        float elapsed = 0f;

        float startValue = currentPosture; // ���� ũ��
        float targetValue = targetPosture;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float newWidth = Mathf.Lerp(startValue, targetValue, elapsed / duration);

            enemyGauge.postureFill.rectTransform.sizeDelta = new Vector2(newWidth, enemyGauge.postureFill.rectTransform.sizeDelta.y);

            UpdatePostureColor(targetPosture / 200);

            yield return null;
        }

        enemyGauge.postureFill.rectTransform.sizeDelta = new Vector2(targetValue, enemyGauge.postureFill.rectTransform.sizeDelta.y);

        currentPosture = targetPosture;
    }

    private void UpdatePostureColor(float fillRatio)
    {
        if (fillRatio < 0.3f)
        {
            enemyGauge.postureFill.color = Color.green;
        }
        else if (fillRatio < 0.7f)
        {
            enemyGauge.postureFill.color = Color.yellow;
        }
        else
        {
            enemyGauge.postureFill.color = Color.red;
        }
    }

    private void DropItem()
    {
        // Raycast�� ����Ͽ� ������ ��ġ�� ���Ѵ�
        Vector3 dropPosition = transform.position;

        RaycastHit hit;
        // �� ��ġ���� �Ʒ��� Ray�� ���� ������ ã��
        if (Physics.Raycast(dropPosition, Vector3.down, out hit, Mathf.Infinity))
        {
            // �������κ��� ������ ���̸� ������
            dropPosition = hit.point + new Vector3(0, 1f, 0); // ���鿡�� 1 ���� ���� ���
        }
        else
        {
            // ���� ������ ã�� ���� ���, �⺻ ��ġ�� ���
            dropPosition = transform.position + new Vector3(0, -2f, 0);
        }

        // ������ ���� �� ���
        GameObject itemObject = Instantiate(itemPrefab, dropPosition, itemPrefab.transform.rotation);

        // ������ ��ü�� �� ������ �ѱ�� (�������� �����ϴ� �κ�)
        ItemObject itemScript = itemObject.GetComponent<ItemObject>();
        if (itemScript != null)
        {
            itemScript.SetDroppedItem(enemyData.DropItems[0]);
        }
    }

    public void TriggerKillmove()
    {
        if (killMoveData != null)
        {
            GetComponent<Animator>().SetTrigger(killMoveData.enemyAnimationTrigger);
        }
    }

    public void MoveToKillMovePosition(Vector3 playerPosition, Quaternion playerRotation)
    {
        StartCoroutine(MoveToPosition(playerPosition, playerRotation, KillMoveData.distance, KillMoveData.duration));
    }

    private IEnumerator MoveToPosition(Vector3 playerPosition, Quaternion playerRotation, float distance, float duration)
    {
        Vector3 startPos = transform.position;

        Vector3 targetPos = playerPosition + (playerRotation * Vector3.forward * distance); // �÷��̾� ���� distance ��ŭ �̵�

        Quaternion startRot = transform.rotation;

        Quaternion targetRot = killMoveData.reverseRotation
            ? playerRotation * Quaternion.Euler(0, 180, 0)
            : playerRotation; // �⺻������ �÷��̾� ���� ����

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / duration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    public void EnableCollider()
    {
        bodyCollider.enabled = true;
    }

    public void DisableBodyCollider()
    {
        bodyCollider.enabled = false;
    }

    void OnAnimatorMove()
    {
        if (currentState == EnemyState.Battle)
        {
            rb.MovePosition(rb.position + anim.deltaPosition);

            if (canRotateToPlayer && playerTarget != null)
            {
                // ����: �÷��̾ ���� �ε巴�� ȸ��
                Vector3 dir = (playerTarget.position - transform.position).normalized;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir);
                    Quaternion smoothRot = Quaternion.Slerp(rb.rotation, lookRot, Time.deltaTime * 5f);
                    rb.MoveRotation(smoothRot);
                }
            }
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.velocity = Vector3.zero;
            }
        }
    }

    private IEnumerator AlertSequence()
    {
        behaviorTree.DisableBehavior();
        behaviorTree.ExternalBehavior = null;

        // 1. ����ǥ UI ���� (�ʿ� �� ���⼭ Instantiate or SetActive)
        enemyGauge.ShowAlertIcon(true);
        AudioManager.instance.PlaySFX("Alerted");

        yield return new WaitForSeconds(1f); // ���� ������ ���

        // 2. ����ǥ �����
        enemyGauge.ShowAlertIcon(false);

        // 3. Alert �ִϸ��̼� ����
        anim.SetTrigger("Alert");

        // 4. �ִϸ��̼� ���� + �����ð���ŭ ���
        yield return WaitForClipLength("Alert", 0.2f);

        // 5. ���� ����
        behaviorTree.ExternalBehavior = originalBehaviorAsset;
        behaviorTree.EnableBehavior();
        fov.SetViewDistance(360, 15f);

        alertCoroutine = null;
    }

    private IEnumerator WaitForClipLength(string clipName, float buffer = 0.1f)
    {
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                yield return new WaitForSeconds(clip.length + buffer);
                yield break;
            }
        }

        Debug.LogWarning($"Ŭ�� '{clipName}' �� ã�� �� �����ϴ�.");
    }

    IEnumerator ResetTreeAfterDelay(float delay)
    {
        behaviorTree.DisableBehavior();
        behaviorTree.ExternalBehavior = null;
        yield return new WaitForSeconds(delay);
        behaviorTree.ExternalBehavior = originalBehaviorAsset;
        behaviorTree.EnableBehavior();
    }
}