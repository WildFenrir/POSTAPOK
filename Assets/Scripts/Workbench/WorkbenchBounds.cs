using UnityEngine;

public class WorkbenchBounds : MonoBehaviour
{
    public static WorkbenchBounds Instance { get; private set; }

    [Tooltip("Коллайдер, задающий рабочую область стола (если пусто — возьмётся с этого же объекта)")]
    public BoxCollider2D tableArea;

    [Tooltip("Небольшой внутренний отступ от краёв стола")]
    public float edgeMargin = 0.1f;

    void Awake()
    {
        Instance = this;
        if (tableArea == null)
            tableArea = GetComponent<BoxCollider2D>();
    }

    /// Возвращает позицию, зажатую строго внутри границ стола с учётом размера детали
    public Vector3 ClampInsideTable(Vector3 desiredWorldPos, Collider2D partCollider)
    {
        if (tableArea == null) return desiredWorldPos;

        Bounds tableB = tableArea.bounds;

        float halfW = 0f;
        float halfH = 0f;

        if (partCollider != null)
        {
            halfW = partCollider.bounds.extents.x;
            halfH = partCollider.bounds.extents.y;
        }

        float minX = tableB.min.x + halfW + edgeMargin;
        float maxX = tableB.max.x - halfW - edgeMargin;
        float minY = tableB.min.y + halfH + edgeMargin;
        float maxY = tableB.max.y - halfH - edgeMargin;

        float clampedX = (minX > maxX) ? tableB.center.x : Mathf.Clamp(desiredWorldPos.x, minX, maxX);
        float clampedY = (minY > maxY) ? tableB.center.y : Mathf.Clamp(desiredWorldPos.y, minY, maxY);

        return new Vector3(clampedX, clampedY, desiredWorldPos.z);
    }
}
