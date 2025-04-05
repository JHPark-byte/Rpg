using UnityEngine;
using TMPro;

public class Interactions : MonoBehaviour
{
    public PlayerContext playerContext;
    public TextMeshProUGUI dialogueText;
    public Camera mainCamera;
    private BoxCollider boxCollider;

    private bool isDialogueActive = false; // �ؽ�Ʈ�� ī�޶� �ٶ󺸴� �Լ��� Ʈ����

    IInteractable interactObj; // ��ȣ�ۿ� ��ü�� ��� ����

    private void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    private void OnEnable()
    {
        GameEventsManager.instance.dialogueEvents.onDialogueEnd += HandleDialogueEnd;
    }

    private void OnDisable()
    {
        GameEventsManager.instance.dialogueEvents.onDialogueEnd -= HandleDialogueEnd;
    }

    private void Update()
    {
        if (isDialogueActive) // ��ȭ ���°� Ȱ��ȭ�� ��쿡�� ó��
        {
            Vector3 directionToCamera = mainCamera.transform.position - dialogueText.transform.position;
            directionToCamera.y = 0;
            Quaternion rotationToCamera = Quaternion.LookRotation(directionToCamera);

            rotationToCamera = Quaternion.Euler(rotationToCamera.eulerAngles.x, rotationToCamera.eulerAngles.y + 180f, rotationToCamera.eulerAngles.z);
            dialogueText.transform.rotation = rotationToCamera;
        }

        InteractPlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();

        if (interactable != null && playerContext.actionStateMachine.CurrentState == playerContext.actionStateMachine.SheathedState)
        {
            interactObj = interactable;

            switch (interactable.InteractionType)
            {
                case InteractionType.NPC:
                    dialogueText.transform.position = other.transform.position + Vector3.up * 2;
                    dialogueText.text = "��ȭ�ϱ�";
                    UpdateDialogueState(true);
                    break;

                case InteractionType.Item:
                    ItemSO item = other.GetComponent<ItemObject>().dropedItem;
                    if (item != null)
                    {
                        GameEventsManager.instance.playerEvents.DiscoverItem(item, true);
                    }
                    break;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("NPC") || other.CompareTag("Item"))
        {
            DisableInteraction();

            if (other.CompareTag("Item"))
            {
                GameEventsManager.instance.playerEvents.DiscoverItem(null, false);
            }
        }
    }

    public void DisableInteraction()
    {
        dialogueText.text = "";
        interactObj = null;
        UpdateDialogueState(false);
    }

    public void HandleDialogueEnd()
    {
        Debug.Log("��ȭ�� �����ϴ�");
        playerContext.actionStateMachine.SetState(playerContext.actionStateMachine.SheathedState); // ��ȭ�� ����Ǹ� �÷��̾ ������ ���·� �ǵ���
        UpdateDialogueState(false); // ��ȭ ���� ��Ȱ��ȭ
        interactObj = null;

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
            boxCollider.enabled = true;
        }
    }

    // E Ű�� ������ ��ȣ�ۿ�
    private void InteractPlayer()
    {
        if (Input.GetKeyDown(KeyCode.E) && interactObj != null)
        {
            if (interactObj.InteractionType == InteractionType.NPC)
            {
                playerContext.actionStateMachine.SetState(playerContext.actionStateMachine.ConversationState); // �÷��̾��� ���¸� ������Ʈ
                dialogueText.text = "";
            }

            interactObj.Interact();
        }
    }

    // ��ȭ ���� ������Ʈ �޼���
    public void UpdateDialogueState(bool active)
    {
        if (isDialogueActive != active)
        {
            isDialogueActive = active;
        }
    }

    private string GetInteractionText(InteractionType type)
    {
        switch (type)
        {
            case InteractionType.NPC:
                return "��ȭ�ϱ�";
            case InteractionType.Item:
                return "�ݱ�";
            case InteractionType.Chest:
                return "����";
            default:
                return "";
        }
    }
}
