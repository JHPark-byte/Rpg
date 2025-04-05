using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

public class LockOn : MonoBehaviour
{
    public GameObject marker; // ���� ��Ŀ
    public GameObject normalMarker; // ���� Ȱ��ȭ�� ��Ŀ ������Ʈ
    public GameObject parryMarker;

    public CinemachineVirtualCamera playerCamera;
    public CinemachineVirtualCamera lockOnCamera;
    private CinemachinePOV pov;
    public Camera mainCamera;

    public bool isLockOn = false;
    public LayerMask enemyLayer;

    public Transform playerTransform;

    private List<Enemy> detectedEnemies = new List<Enemy>();
    private int currentLockOnIndex = -1; // ���� ���� ��� �ε���

    private PlayerContext playerContext;

    public float detectionRadius = 10f;
    public float frontOffset = 5f;

    private Enemy lockOnEnemy; // ���� ����� �����ϴ� ����

    private void Awake()
    {
        playerContext = GetComponent<PlayerContext>();
    }

    public void Initialize(Camera mainCamera, CinemachineVirtualCamera playerCam, CinemachineVirtualCamera lockOnCam,
                                    GameObject marker, GameObject normalMarker, GameObject parryMarker)
    {
        if (playerCam == null || lockOnCam == null)
        {
            Debug.LogError("LockOn �ʱ�ȭ ����: ī�޶� null�Դϴ�.");
            return;
        }

        this.mainCamera = mainCamera;
        this.playerCamera = playerCam;
        this.lockOnCamera = lockOnCam;
        this.marker = marker;
        this.normalMarker = normalMarker;
        this.parryMarker = parryMarker;

        pov = playerCamera.GetCinemachineComponent<CinemachinePOV>();
    }

    private void OnEnable()
    {
        GameEventsManager.instance.enemyEvents.onEnemyDie += EnemyDeath;
        GameEventsManager.instance.enemyEvents.onPostureBreak += UpdateParryMarker;
        GameEventsManager.instance.enemyEvents.onRecoveryPosture += UpdateParryMarker;
    }

    private void OnDisable()
    {
        GameEventsManager.instance.enemyEvents.onEnemyDie -= EnemyDeath;
        GameEventsManager.instance.enemyEvents.onPostureBreak -= UpdateParryMarker;
        GameEventsManager.instance.enemyEvents.onRecoveryPosture -= UpdateParryMarker;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(2)) // ���� ���
        {
            if (isLockOn)
            {
                DeactivateLockOn(lockOnEnemy);
            }
            else
            {
                UpdateDetectedEnemies();
                if (detectedEnemies.Count > 0)
                {
                    currentLockOnIndex = 0; // ù ��° ������ �ʱ�ȭ
                    ActivateLockOn(detectedEnemies[currentLockOnIndex]);
                }
            }
        }

        if (isLockOn && detectedEnemies.Count > 0)
        {

            // �ֱ������� �� Ž�� ������Ʈ
            UpdateDetectedEnemies();

            // ���콺 �ٷ� ���� ��� ����
            if (Input.mouseScrollDelta.y > 0) // �� ����
            {
                CycleLockOnTarget(1);
            }
            else if (Input.mouseScrollDelta.y < 0) // �� �Ʒ���
            {
                CycleLockOnTarget(-1);
            }

            Transform markerPosition = lockOnEnemy.marker;
            marker.transform.position = markerPosition.position;

            marker.transform.LookAt(mainCamera.transform.position); // ��Ŀ�� ī�޶� �ٶ󺸵��� ����
        }
    }

    private void FixedUpdate()
    {
        if (isLockOn)
        {
            RotatePlayer();
        }
    }

    private void RotatePlayer()
    {
        Vector3 direction = lockOnEnemy.transform.position - transform.position;
        direction.y = 0; // ���� ȸ�� ����

        if (direction.sqrMagnitude < 0.0001f) return; // ���� ���� ����: ����

        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // ���� ȸ���� Ÿ�� ȸ���� ����
        float angleDifference = Quaternion.Angle(playerContext.Rigidbody.rotation, lookRotation);

        // ���� ���̰� Ŭ ���� ȸ��
        if (angleDifference > 3f) // 3�� �̻��� ���� ȸ��
        {
            playerContext.Rigidbody.MoveRotation(Quaternion.Slerp(
                playerContext.Rigidbody.rotation,
                lookRotation,
                Time.deltaTime * 10f  // ȸ�� �ӵ� ����
            ));
        }
    }

    private void CycleLockOnTarget(int direction)
    {
        if (detectedEnemies.Count == 0) return;

        // ���� �ε����� ��ȯ
        currentLockOnIndex = (currentLockOnIndex + direction + detectedEnemies.Count) % detectedEnemies.Count;

        // ���ο� ���� ��� ����
        ActivateLockOn(lockOnEnemy);
    }

    public void UpdateDetectedEnemies()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position + mainCamera.transform.forward * frontOffset, detectionRadius, enemyLayer);
        List<Enemy> newDetectedEnemies = new List<Enemy>();

        foreach (Collider col in colliders)
        {
            Enemy enemyHealth = col.GetComponent<Enemy>();
            if (enemyHealth != null)
            {
                newDetectedEnemies.Add(enemyHealth);
            }
        }

        // �Ÿ� �������� ����
       newDetectedEnemies.Sort((enemy1, enemy2) =>
        {
            float distance1 = Vector3.Distance(transform.position, enemy1.transform.position);
            float distance2 = Vector3.Distance(transform.position, enemy2.transform.position);
            return distance1.CompareTo(distance2); // ����� ������ ����
        });

        // ���� ����� 3�� ����Ʈ�� �߰�
        detectedEnemies = newDetectedEnemies.Take(3).ToList();
    }

    public Enemy FindClosestEnemy()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position + mainCamera.transform.forward * frontOffset, detectionRadius, enemyLayer);
        lockOnEnemy = null;
        float closestDistance = Mathf.Infinity;
        float halfFieldOfView = mainCamera.fieldOfView * 0.5f;

        foreach (Collider col in colliders)
        {
            Enemy enemyHealth = col.GetComponent<Enemy>(); // Collider ��� EnemyHealth ������Ʈ�� ������
            if (enemyHealth != null)
            {
                Transform enemyTransform = col.transform;
                Vector3 cameraToEnemy = enemyTransform.position - mainCamera.transform.position;
                float angle = Vector3.Angle(mainCamera.transform.forward, cameraToEnemy);

                if (angle <= halfFieldOfView)
                {
                    float distanceToEnemy = Vector3.Distance(mainCamera.transform.position, enemyTransform.position);

                    if (distanceToEnemy < closestDistance)
                    {
                        lockOnEnemy = enemyHealth;
                        closestDistance = distanceToEnemy;
                    }
                }
            }
        }

        return lockOnEnemy;
    }

    public Vector3 GetTargetDirection()
    {
        // ���� ���� ����� ������ (0, 0, 0) ��ȯ
        if (lockOnEnemy == null)
            return Vector3.zero;

        // ���� ���� �÷��̾� ��ġ ���� ���� ���
        Vector3 direction = lockOnEnemy.transform.position - playerTransform.position;

        // y�� ���� �����Ͽ� ���� ���⸸ ���
        direction.y = 0;

        // ���� ���͸� ����ȭ�Ͽ� ��ȯ
        return direction.normalized;
    }

    void ActivateLockOn(Enemy enemy)
    {
        lockOnEnemy = enemy;

        lockOnCamera.Priority = 20;
        playerCamera.Priority = 10;

        isLockOn = true;
        marker.SetActive(true);
        enemy.ActivateGauge();

        UpdateParryMarker(enemy);

        playerContext.Animator.SetBool("isLockOn", true);

        lockOnCamera.LookAt = enemy.transform;
    }

    void DeactivateLockOn(IEnemy enemy)
    {
        RotatePlayer();

        lockOnEnemy = null;

        lockOnCamera.Priority = 10;
        playerCamera.Priority = 20;

        lockOnCamera.LookAt = null;

        isLockOn = false;
        marker.SetActive(false);
        marker.transform.SetParent(null);
        enemy.DeActivateHealthBar();

        playerContext.Animator.SetBool("isLockOn", false);

        // ���� ī�޶� ���� ������ ȸ������ ����ȭ
        Quaternion lockOnRotation = lockOnCamera.transform.rotation;
        Vector3 lockOnEulerAngles = lockOnRotation.eulerAngles;

        pov.m_HorizontalAxis.Value = lockOnEulerAngles.y; 
        pov.m_VerticalAxis.Value = lockOnEulerAngles.x;
    }

    void UpdateParryMarker(Enemy enemy)
    {
        if (lockOnEnemy.IsParryGuageFull)
        {
            normalMarker.SetActive(false);
            parryMarker.SetActive(true);
        }
        else
        {
            normalMarker.SetActive(true);
            parryMarker.SetActive(false);
        }
    }

    void UpdateParryMarker()
    {
        normalMarker.SetActive(true);
        parryMarker.SetActive(false);
    }

    private void EnemyDeath(IEnemy enemy)
    {
        if (lockOnEnemy != null)
        {
            DeactivateLockOn(enemy);
        }
    }

    void OnDrawGizmos()
    {
        // ĳ���� ��ġ���� ī�޶��� ���� �������� frontOffset �Ÿ���ŭ ������ ������ ����մϴ�.
        Vector3 sphereCenter = transform.position + mainCamera.transform.forward * frontOffset;

        Gizmos.color = Color.red; // Gizmos ������ ���������� ����
                                  // ���� ��ġ�� ���Ǿ �׸��ϴ�.
        Gizmos.DrawWireSphere(sphereCenter, detectionRadius);
    }
}
