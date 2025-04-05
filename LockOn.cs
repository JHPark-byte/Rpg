using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

public class LockOn : MonoBehaviour
{
    public GameObject marker; // 락온 마커
    public GameObject normalMarker; // 현재 활성화된 마커 오브젝트
    public GameObject parryMarker;

    public CinemachineVirtualCamera playerCamera;
    public CinemachineVirtualCamera lockOnCamera;
    private CinemachinePOV pov;
    public Camera mainCamera;

    public bool isLockOn = false;
    public LayerMask enemyLayer;

    public Transform playerTransform;

    private List<Enemy> detectedEnemies = new List<Enemy>();
    private int currentLockOnIndex = -1; // 현재 락온 대상 인덱스

    private PlayerContext playerContext;

    public float detectionRadius = 10f;
    public float frontOffset = 5f;

    private Enemy lockOnEnemy; // 락온 대상을 저장하는 변수

    private void Awake()
    {
        playerContext = GetComponent<PlayerContext>();
    }

    public void Initialize(Camera mainCamera, CinemachineVirtualCamera playerCam, CinemachineVirtualCamera lockOnCam,
                                    GameObject marker, GameObject normalMarker, GameObject parryMarker)
    {
        if (playerCam == null || lockOnCam == null)
        {
            Debug.LogError("LockOn 초기화 실패: 카메라가 null입니다.");
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
        if (Input.GetMouseButtonDown(2)) // 락온 토글
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
                    currentLockOnIndex = 0; // 첫 번째 적으로 초기화
                    ActivateLockOn(detectedEnemies[currentLockOnIndex]);
                }
            }
        }

        if (isLockOn && detectedEnemies.Count > 0)
        {

            // 주기적으로 적 탐지 업데이트
            UpdateDetectedEnemies();

            // 마우스 휠로 락온 대상 변경
            if (Input.mouseScrollDelta.y > 0) // 휠 위로
            {
                CycleLockOnTarget(1);
            }
            else if (Input.mouseScrollDelta.y < 0) // 휠 아래로
            {
                CycleLockOnTarget(-1);
            }

            Transform markerPosition = lockOnEnemy.marker;
            marker.transform.position = markerPosition.position;

            marker.transform.LookAt(mainCamera.transform.position); // 마커가 카메라를 바라보도록 설정
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
        direction.y = 0; // 수직 회전 제거

        if (direction.sqrMagnitude < 0.0001f) return; // 방향 거의 없음: 무시

        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // 현재 회전과 타겟 회전의 차이
        float angleDifference = Quaternion.Angle(playerContext.Rigidbody.rotation, lookRotation);

        // 각도 차이가 클 때만 회전
        if (angleDifference > 3f) // 3도 이상일 때만 회전
        {
            playerContext.Rigidbody.MoveRotation(Quaternion.Slerp(
                playerContext.Rigidbody.rotation,
                lookRotation,
                Time.deltaTime * 10f  // 회전 속도 조절
            ));
        }
    }

    private void CycleLockOnTarget(int direction)
    {
        if (detectedEnemies.Count == 0) return;

        // 현재 인덱스를 순환
        currentLockOnIndex = (currentLockOnIndex + direction + detectedEnemies.Count) % detectedEnemies.Count;

        // 새로운 락온 대상 설정
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

        // 거리 기준으로 정렬
       newDetectedEnemies.Sort((enemy1, enemy2) =>
        {
            float distance1 = Vector3.Distance(transform.position, enemy1.transform.position);
            float distance2 = Vector3.Distance(transform.position, enemy2.transform.position);
            return distance1.CompareTo(distance2); // 가까운 순으로 정렬
        });

        // 가장 가까운 3명만 리스트에 추가
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
            Enemy enemyHealth = col.GetComponent<Enemy>(); // Collider 대신 EnemyHealth 컴포넌트를 가져옴
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
        // 현재 락온 대상이 없으면 (0, 0, 0) 반환
        if (lockOnEnemy == null)
            return Vector3.zero;

        // 락온 대상과 플레이어 위치 간의 방향 계산
        Vector3 direction = lockOnEnemy.transform.position - playerTransform.position;

        // y축 값을 고정하여 수평 방향만 계산
        direction.y = 0;

        // 방향 벡터를 정규화하여 반환
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

        // 기존 카메라를 락온 상태의 회전값에 동기화
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
        // 캐릭터 위치에서 카메라의 전방 방향으로 frontOffset 거리만큼 떨어진 지점을 계산합니다.
        Vector3 sphereCenter = transform.position + mainCamera.transform.forward * frontOffset;

        Gizmos.color = Color.red; // Gizmos 색상을 빨간색으로 설정
                                  // 계산된 위치에 스피어를 그립니다.
        Gizmos.DrawWireSphere(sphereCenter, detectionRadius);
    }
}
