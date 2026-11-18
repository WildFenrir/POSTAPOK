using System.Collections.Generic;
using UnityEngine;

/// Свободная зона установки. Не привязана к конкретной детали:
/// принимает ЛЮБУЮ деталь, чьи теги пересекаются с acceptTags.
/// Родитель не меняется — деталь остаётся в мире и всегда кликабельна.
public class PartZone : MonoBehaviour
{
    /// Список всех активных зон в сцене (для быстрого поиска из ZoneDragAndDrop)
    public static readonly List<PartZone> All = new List<PartZone>();

    [Header("Что принимает")]
    [Tooltip("Теги деталей, которые зона принимает. Пустой список = зона НЕ принимает ничего (см. acceptAny)")]
    public string[] acceptTags = new string[0];

    [Tooltip("Явно разрешить зоне принимать ВСЕ детали без проверки тегов")]
    public bool acceptAny = false;

    [Header("Поведение")]
    [Tooltip("Одна деталь = true. Несколько = false (мульти-слот)")]
    public bool exclusive = true;
    public bool canRemove = true;

    [Tooltip("Запретить ставить детали друг на друга в мульти-слоте")]
    public bool preventOverlap = true;

    [Tooltip("Ставить деталь строго в центр зоны")]
    public bool centerInZone = false;
    [Tooltip("Клампать позицию в границы зоны (если false — свободно)")]
    public bool clampToBounds = false;
    [Tooltip("0 = использовать snapDistance самой детали")]
    public float snapDistance = 0f;

    [Header("Визуальный отклик")]
    public Color highlightColor = new Color(1f, 1f, 0.4f, 0.55f);

    /// Коллайдер зоны (кэшируется)
    public Collider2D ZoneCollider { get; private set; }

    /// Прикреплённые детали (при exclusive их максимум одна)
    public IReadOnlyList<ZoneDragAndDrop> Occupants => occupants;
    private readonly List<ZoneDragAndDrop> occupants = new List<ZoneDragAndDrop>();

    // Деталь, которую сейчас тащат мышью (для подсветки)
    private static ZoneDragAndDrop previewPart;

    private SpriteRenderer spriteRenderer;
    private Color defaultColor;

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            defaultColor = spriteRenderer.color;
        }
        ZoneCollider = GetComponent<Collider2D>();

        if (ZoneCollider == null)
            ZoneCollider = GetComponentInChildren<Collider2D>();
    }

    /// Может ли зона принять деталь (теги + занятость)
    public bool CanAccept(ZoneDragAndDrop part)
    {
        if (part == null) return false;

        // Нельзя ставить объект в зону, которая является его ребёнком
        // (иначе корпус "присоединяет сам себя" и повисает на якоре)
        if (part.transform == transform || part.transform.IsChildOf(transform)) return false;

        // Занятая exclusive-зона не принимает чужие детали,
        // но свою (снятую и возвращаемую обратно) — может
        if (exclusive && IsOccupied && !Contains(part)) return false;

        // Явное "принимать всё"
        if (acceptAny) return true;

        // Пустой список тегов = зона не принимает ничего.
        // Так зона с незаполненными тегами не превращается в "подходит всем".
        if (acceptTags == null || acceptTags.Length == 0) return false;

        string[] partTags = part.partTags;
        if (partTags == null || partTags.Length == 0) return false;

        for (int i = 0; i < acceptTags.Length; i++)
        {
            string tag = acceptTags[i].Trim();
            if (tag.Length == 0) continue;

            for (int j = 0; j < partTags.Length; j++)
            {
                if (string.Equals(partTags[j].Trim(), tag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// Расстояние от точки до зоны (ближайшая точка коллайдера)
    public float DistanceTo(Vector3 point)
    {
        if (ZoneCollider != null)
        {
            Vector2 closest = ZoneCollider.ClosestPoint(point);
            return Vector2.Distance(point, closest);
        }
        return Vector2.Distance(point, transform.position);
    }

    /// Пометить зону как занятую этой деталью
    public void Occupy(ZoneDragAndDrop part)
    {
        if (!occupants.Contains(part))
            occupants.Add(part);

        RefreshColor();
    }

    /// Освободить зону от детали
    public void Release(ZoneDragAndDrop part)
    {
        occupants.Remove(part);
        RefreshColor();
    }

    /// Занята ли зона
    public bool IsOccupied => occupants.Count > 0;

    /// Деталь прикреплена к этой зоне?
    public bool Contains(ZoneDragAndDrop part) => occupants.Contains(part);

    // Подсветка всех зон, подходящих для тащимой детали
    public static void BeginPreview(ZoneDragAndDrop part)
    {
        previewPart = part;
        UpdatePreview();
    }

    // Обновляет подсветку при перемещении детали (вызывается из ZoneDragAndDrop)
    public static void UpdatePreview()
    {
        for (int i = 0; i < All.Count; i++)
        {
            PartZone zone = All[i];
            if (zone != null)
                zone.RefreshColor();
        }
    }

    public static void EndPreview()
    {
        previewPart = null;
        UpdatePreview();
    }

    // Цвет зоны: подсветка при перетаскивании, иначе исходный цвет из SpriteRenderer
    private void RefreshColor()
    {
        if (spriteRenderer == null) return;

        bool highlighted = previewPart != null && CanAccept(previewPart);

        if (highlighted)
            spriteRenderer.color = highlightColor;
        else
            spriteRenderer.color = defaultColor;
    }

    /// Проверяет, не наложится ли деталь на уже установленные в этой зоне
    public bool WouldOverlap(ZoneDragAndDrop part, Collider2D partCol, Vector3 targetWorldPos)
    {
        if (!preventOverlap || partCol == null) return false;

        Vector3 delta = targetWorldPos - part.transform.position;

        // Микродопуск (0.02), чтобы детали можно было ставить вплотную край к краю
        float margin = 0.02f;
        Vector2 minA = (Vector2)(partCol.bounds.min + delta) + Vector2.one * margin;
        Vector2 maxA = (Vector2)(partCol.bounds.max + delta) - Vector2.one * margin;

        for (int i = 0; i < occupants.Count; i++)
        {
            ZoneDragAndDrop other = occupants[i];
            if (other == null || other == part) continue;

            Collider2D otherCol = other.GetComponent<Collider2D>();
            if (otherCol == null) continue;

            Bounds b = otherCol.bounds;
            bool overlapX = minA.x < b.max.x && maxA.x > b.min.x;
            bool overlapY = minA.y < b.max.y && maxA.y > b.min.y;

            if (overlapX && overlapY) return true;
        }

        return false;
    }

    /// Ищет ближайшее свободное место внутри зоны рядом с занятой деталью
    public bool TryFindFreeSpot(ZoneDragAndDrop part, Collider2D partCol, Vector3 desiredPos, out Vector3 freePos)
    {
        freePos = desiredPos;
        if (partCol == null || ZoneCollider == null) return false;

        // Находим первую деталь, с которой произошло пересечение
        Bounds otherBounds = new Bounds();
        bool foundBlocker = false;

        Vector3 delta = desiredPos - part.transform.position;
        Vector2 minA = (Vector2)(partCol.bounds.min + delta);
        Vector2 maxA = (Vector2)(partCol.bounds.max + delta);

        for (int i = 0; i < occupants.Count; i++)
        {
            ZoneDragAndDrop other = occupants[i];
            if (other == null || other == part) continue;

            Collider2D otherCol = other.GetComponent<Collider2D>();
            if (otherCol == null) continue;

            Bounds b = otherCol.bounds;
            if (minA.x < b.max.x && maxA.x > b.min.x && minA.y < b.max.y && maxA.y > b.min.y)
            {
                otherBounds = b;
                foundBlocker = true;
                break;
            }
        }

        if (!foundBlocker) return true;

        Bounds zoneB = ZoneCollider.bounds;
        Vector2 halfPart = partCol.bounds.extents;
        float gap = 0.04f; // Небольшой зазор между деталями

        // 4 варианта смещения: слева, справа, снизу и сверху от занятой детали
        Vector3[] candidates = new Vector3[]
        {
            new Vector3(otherBounds.min.x - halfPart.x - gap, desiredPos.y, desiredPos.z),
            new Vector3(otherBounds.max.x + halfPart.x + gap, desiredPos.y, desiredPos.z),
            new Vector3(desiredPos.x, otherBounds.min.y - halfPart.y - gap, desiredPos.z),
            new Vector3(desiredPos.x, otherBounds.max.y + halfPart.y + gap, desiredPos.z)
        };

        float bestDist = float.MaxValue;
        bool foundSpot = false;

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 c = candidates[i];

            // Зажимаем позицию строго внутри границ зоны
            float minX = zoneB.min.x + halfPart.x;
            float maxX = zoneB.max.x - halfPart.x;
            float minY = zoneB.min.y + halfPart.y;
            float maxY = zoneB.max.y - halfPart.y;

            if (minX > maxX || minY > maxY) continue;

            c.x = Mathf.Clamp(c.x, minX, maxX);
            c.y = Mathf.Clamp(c.y, minY, maxY);

            // Проверяем, свободно ли это место от всех остальных деталей
            if (!WouldOverlap(part, partCol, c))
            {
                float d = Vector2.Distance(desiredPos, c);
                if (d < bestDist)
                {
                    bestDist = d;
                    freePos = c;
                    foundSpot = true;
                }
            }
        }

        return foundSpot;
    }
}
