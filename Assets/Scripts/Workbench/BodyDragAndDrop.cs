using UnityEngine;

/// Перетаскивание основного корпуса прибора (микроволновки, радио и т.д.) по верстаку.
/// При отпускании сам корпус не отскакивает, а мягко расталкивает лежащие под ним свободные детали.
public class BodyDragAndDrop : MonoBehaviour
{
    [Header("Настройки перемещения корпуса")]
    [Tooltip("Ограничивать перемещение корпуса рамками верстака (WorkbenchBounds)")]
    public bool clampToWorkbench = true;

    [Tooltip("Отталкивать свободные детали на столе, если корпус положили поверх них")]
    public bool pushPartsAside = true;

    private bool isDragging = false;
    private Vector3 offset;
    private Camera mainCamera;
    private Collider2D bodyCollider;

    void Start()
    {
        mainCamera = Camera.main;
        bodyCollider = GetComponent<Collider2D>();
    }

    void OnMouseDown()
    {
        isDragging = true;
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        offset = transform.position - mouseWorldPos;
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Vector3 targetPos = GetMouseWorldPosition() + offset;

        if (clampToWorkbench && WorkbenchBounds.Instance != null)
        {
            targetPos = WorkbenchBounds.Instance.ClampInsideTable(targetPos, bodyCollider);
        }

        transform.position = targetPos;
    }

    void OnMouseUp()
    {
        if (!isDragging) return;

        isDragging = false;

        if (pushPartsAside && bodyCollider != null)
        {
            PushOverlappingParts();
        }
    }

    // Находим все свободные детали на столе под корпусом и отправляем их за его края
    private void PushOverlappingParts()
    {
        for (int i = 0; i < TablePartRegistry.All.Count; i++)
        {
            var entry = TablePartRegistry.All[i];
            if (entry.transform == null) continue;

            // Проверяем оба возможных скрипта деталей
            SlotDragAndDrop slotPart = entry.transform.GetComponent<SlotDragAndDrop>();
            if (slotPart != null)
            {
                slotPart.PushOutFromBody(bodyCollider);
                continue;
            }

            ZoneDragAndDrop zonePart = entry.transform.GetComponent<ZoneDragAndDrop>();
            if (zonePart != null)
            {
                zonePart.PushOutFromBody(bodyCollider);
            }
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }
}
