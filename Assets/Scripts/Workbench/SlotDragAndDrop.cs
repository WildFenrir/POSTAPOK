using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// Перетаскивание детали в жёсткий слот (PartSlot), указанный в Target Slot.
/// Для свободных зон по тегам используйте ZoneDragAndDrop.
public class SlotDragAndDrop : MonoBehaviour
{


    [Header("Теги детали")]
    public string[] partTags = new string[0];



    [Header("Настройки сборки")]
    [Tooltip("Жёсткий слот (PartSlot), куда должна встать эта деталь")]
    public Transform targetSlot;

    [Tooltip("Насколько близко к границам слота нужно поднести деталь для установки")]
    public float snapDistance = 0.1f;

    [Tooltip("Можно ли снимать деталь после установки")]
    public bool canRemove = true;

    [Tooltip("Ставить деталь строго в центр слота (если выключено — выравнивает по краям паза)")]
    public bool centerInSlot = false;

    [Tooltip("При установке поворачивать деталь по углу слота")]
    public bool matchSlotRotation = false;



    [Header("Правила стола")]
    [Tooltip("Запретить накладываться на другие детали на рабочем столе")]
    public bool preventTableOverlap = true;

    [Tooltip("Находится ли деталь сейчас внутри коробки")]
    public bool isInBox = false;



    [Header("Настройки отскакивания от корпуса")]
    [Tooltip("Перетащите сюда коллайдер синего корпуса")]
    public Collider2D bodyCollider;

    [Tooltip("Отступ от края корпуса после отскока")]
    public float ejectPadding = 0.2f;

    [Tooltip("Длительность плавного отскока в секундах")]
    public float bounceDuration = 0.15f;



    [Header("Выравнивание при взятии")]
    [Tooltip("Плавно поворачивать наклонённую деталь в 0 градусов при клике")]
    public bool straightenOnPick = true;

    [Tooltip("Длительность плавного выравнивания в секундах")]
    public float straightenDuration = 0.2f;


    private bool isDragging = false;
    private bool isInstalled = false;
    private Vector3 offset;
    private Camera mainCamera;
    private Collider2D partCollider;
    private Collider2D slotCollider;
    private Coroutine bounceCoroutine;
    private Coroutine straightenCoroutine;

    // Крепление логическое: иерархию (родителя) не меняем, иначе повёрнутый
    // родитель даёт shear по scale ("растяжение") и сдвиг по Z.
    private PartSlot attachedSlot;

    // Якорь, за которым деталь следует после установки. Позиция пересчитывается
    // из локальной точки якоря в LateUpdate — деталь "приварена" к объекту
    // без смены родителя и без риска исказить scale.
    private Transform anchor;
    private Vector3 anchorLocalPoint;
    private float anchorLocalAngle;
    private float anchorWorldZ;

    void Start()
    {
        mainCamera = Camera.main;
        partCollider = GetComponent<Collider2D>();

        StorageBox startBox = StorageBox.FindBoxAt(transform.position, partTags);
        if (startBox != null)
            isInBox = true;

        if (targetSlot != null)
        {
            slotCollider = targetSlot.GetComponent<Collider2D>();

            if (slotCollider == null)
                slotCollider = targetSlot.GetComponentInChildren<Collider2D>();
        }
    }

    // Деталь следует за якорем, пока прикреплена и не тянется мышью
    void LateUpdate()
    {
        if (!isInstalled || isDragging || anchor == null) return;

        Vector3 world = anchor.TransformPoint(anchorLocalPoint);
        transform.position = new Vector3(world.x, world.y, anchorWorldZ);

        if (matchSlotRotation)
        {
            Vector3 e = transform.eulerAngles;
            transform.eulerAngles = new Vector3(e.x, e.y, anchor.eulerAngles.z + anchorLocalAngle);
        }
    }

    void OnMouseDown()
    {
        if (isInstalled && !canRemove) return;

        if (bounceCoroutine != null)
        {
            StopCoroutine(bounceCoroutine);
            bounceCoroutine = null;
        }

        if (isInstalled)
            ReleaseAttachment();

        isDragging = true;
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        offset = transform.position - mouseWorldPos;

        if (straightenOnPick)
        {
            if (straightenCoroutine != null)
                StopCoroutine(straightenCoroutine);
        }

    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Vector3 targetPos = GetMouseWorldPosition() + offset;

        transform.position = targetPos;
    }


    void OnMouseUp()
    {
        if (!isDragging) return;

        isDragging = false;
        CheckPlacement();
    }

    void OnEnable()
    {
        TablePartRegistry.Register(transform, GetComponent<Collider2D>(), () => isInBox);
    }

    void OnDisable()
    {
        TablePartRegistry.Unregister(transform);
    }

        private IEnumerator SmoothRotateTo(float targetAngle)
    {
        float startAngle = transform.eulerAngles.z;
        float elapsed = 0f;

        if (Mathf.Abs(Mathf.DeltaAngle(startAngle, targetAngle)) < 0.05f)
        {
            Vector3 exact = transform.eulerAngles;
            transform.eulerAngles = new Vector3(exact.x, exact.y, targetAngle);
            straightenCoroutine = null;
            yield break;
        }

        while (elapsed < straightenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / straightenDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            // LerpAngle всегда крутит по кратчайшему пути (и влево, и вправо)
            float currentAngle = Mathf.LerpAngle(startAngle, targetAngle, smoothT);
            Vector3 e = transform.eulerAngles;
            transform.eulerAngles = new Vector3(e.x, e.y, currentAngle);

            yield return null;
        }

        Vector3 finalEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(finalEuler.x, finalEuler.y, targetAngle);
        straightenCoroutine = null;
    }

    // Снимает логическое крепление. Родителя не трогаем.
    private void ReleaseAttachment()
    {
        isInstalled = false;
        anchor = null;

        if (attachedSlot != null)
        {
            attachedSlot.OnPartRemoved();
            attachedSlot = null;
        }
        else
        {
            NotifySlot(false);
        }
    }

    private void OnDestroy()
    {
        if (isInstalled)
            ReleaseAttachment();
    }

    private void CheckPlacement()
    {
        // 0. Проверяем, находится ли деталь сейчас над подходящей коробкой
        StorageBox boxUnderPart = StorageBox.FindBoxAt(transform.position, partTags);
        bool nowInBox = (boxUnderPart != null);

        if (nowInBox != isInBox)
        {
            isInBox = nowInBox;

            if (straightenOnPick)
            {
                if (straightenCoroutine != null)
                    StopCoroutine(straightenCoroutine);

                // Если положили в коробку — случайный наклон, если достали на стол — ровно 0°
                float targetAngle = isInBox
                    ? Random.Range(boxUnderPart.minTiltAngle, boxUnderPart.maxTiltAngle)
                    : 0f;

                straightenCoroutine = StartCoroutine(SmoothRotateTo(targetAngle));
            }
        }

        else if (!isInBox && straightenOnPick && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, 0f)) > 0.05f)
        {
            if (straightenCoroutine != null)
                StopCoroutine(straightenCoroutine);

            straightenCoroutine = StartCoroutine(SmoothRotateTo(0f));
        }

        // Если деталь оставили в коробке — не проверяем слоты, корпус и наложение на столе
        if (isInBox) return;

        // 1. Проверяем попадание в целевой слот
        if (targetSlot != null && DistanceTo(targetSlot, slotCollider) <= snapDistance)
        {
            InstallInSlot();
            return;
        }

        // 2. Не попали — проверяем пересечение с корпусом (игнорируя Z)
        if (TryBounceOffBody())
        {
            return;
        }


        // 3. Проверяем, не бросили ли деталь за пределами верстака (и не в ящик)
        if (!isInBox && WorkbenchBounds.Instance != null)
        {
            Vector3 clampedPos = WorkbenchBounds.Instance.ClampInsideTable(transform.position, partCollider);
            if (Vector2.Distance(transform.position, clampedPos) > 0.01f)
            {
                bounceCoroutine = StartCoroutine(SmoothBounceTo(clampedPos));
                return;
            }
        }

        // 4. Если деталь осталась на столе — проверяем наложение на другие детали
        if (!isInBox && preventTableOverlap)
        {
            TrySeparateFromOtherParts();
        }
    }

    // Расстояние от центра детали до ближайшей точки слота
    private float DistanceTo(Transform target, Collider2D targetCollider)
    {
        if (targetCollider != null)
        {
            Vector2 closest = targetCollider.ClosestPoint(transform.position);
            return Vector2.Distance(transform.position, closest);
        }

        return Vector2.Distance(transform.position, target.position);
    }

    private void InstallInSlot()
    {
        if (straightenCoroutine != null)
        {
            StopCoroutine(straightenCoroutine);
            straightenCoroutine = null;
        }

        if (matchSlotRotation)
            transform.rotation = targetSlot.rotation;
        else if (straightenOnPick)
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0f);

        float keepZ = transform.position.z;

        // Зажим по границам слота теперь работает всегда по умолчанию
        Vector3 pos = (centerInSlot || slotCollider == null)
            ? targetSlot.position
            : ClampToWorld(targetSlot, slotCollider);


        transform.position = new Vector3(pos.x, pos.y, keepZ);

        isInstalled = true;
        AttachTo(targetSlot);

        attachedSlot = targetSlot.GetComponent<PartSlot>();

        if (attachedSlot != null)
            attachedSlot.OnPartInstalled();
        else
            NotifySlot(true);
    }

    // Запоминаем, где деталь лежит внутри якоря, чтобы повторять это
    // смещение за движением/поворотом объекта.
    private void AttachTo(Transform newAnchor)
    {
        anchor = newAnchor;
        anchorWorldZ = transform.position.z;
        anchorLocalPoint = anchor.InverseTransformPoint(transform.position);
        anchorLocalAngle = transform.eulerAngles.z - anchor.eulerAngles.z;
    }

    private bool TryBounceOffBody()
    {
        if (bodyCollider == null || partCollider == null) return false;

        Bounds partB = partCollider.bounds;
        Bounds bodyB = bodyCollider.bounds;

        bool overlapX = partB.min.x <= bodyB.max.x && partB.max.x >= bodyB.min.x;
        bool overlapY = partB.min.y <= bodyB.max.y && partB.max.y >= bodyB.min.y;

        if (overlapX && overlapY)
        {
            Vector3 ejectTarget = CalculateNearestEdgePosition();
            bounceCoroutine = StartCoroutine(SmoothBounceTo(ejectTarget));
            return true;
        }

        return false;
    }


    // Позиция внутри слота с учётом его поворота.
    // Считаем в ЛОКАЛЬНЫХ координатах слота (там он не повёрнут), а результат
    // переводим обратно в мировые. Scale не участвует — растяжения нет.
    private Vector3 GetClampedPositionInSlot()
    {
        return ClampToWorld(targetSlot, slotCollider);
    }

    private Vector3 ClampToWorld(Transform slot, Collider2D slotCol)
    {
        Vector3 worldPos = transform.position;

        if (partCollider == null) return worldPos;

        // Берём точные локальные границы и слота, и самой детали без задержек физики
        if (!TryGetLocalBounds(slotCol, out Vector2 slotMin, out Vector2 slotMax))
            return worldPos;

        if (!TryGetLocalBounds(partCollider, out Vector2 partMin, out Vector2 partMax))
            return worldPos;

        Vector3 slotScale = slot.lossyScale;
        float sx = Mathf.Abs(slotScale.x) < 0.0001f ? 1f : Mathf.Abs(slotScale.x);
        float sy = Mathf.Abs(slotScale.y) < 0.0001f ? 1f : Mathf.Abs(slotScale.y);

        // Реальная половина ширины и высоты детали с учётом её масштаба
        Vector3 partScale = transform.lossyScale;
        float realHalfPartW = ((partMax.x - partMin.x) * 0.5f) * Mathf.Abs(partScale.x);
        float realHalfPartH = ((partMax.y - partMin.y) * 0.5f) * Mathf.Abs(partScale.y);

        // Переводим размеры детали в локальные единицы слота
        float halfX = realHalfPartW / sx;
        float halfY = realHalfPartH / sy;

        Vector3 local = slot.InverseTransformPoint(worldPos);

        float minX = slotMin.x + halfX;
        float maxX = slotMax.x - halfX;
        float minY = slotMin.y + halfY;
        float maxY = slotMax.y - halfY;

        float cx = (minX > maxX) ? (slotMin.x + slotMax.x) * 0.5f : Mathf.Clamp(local.x, minX, maxX);
        float cy = (minY > maxY) ? (slotMin.y + slotMax.y) * 0.5f : Mathf.Clamp(local.y, minY, maxY);

        return slot.TransformPoint(new Vector3(cx, cy, local.z));
    }

    // Границы коллайдера в локальных координатах его объекта
    private static bool TryGetLocalBounds(Collider2D col, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (col is BoxCollider2D box)
        {
            Vector2 center = box.offset;
            Vector2 half = box.size * 0.5f;
            min = center - half;
            max = center + half;
            return true;
        }

        if (col is CircleCollider2D circle)
        {
            Vector2 center = circle.offset;
            Vector2 half = Vector2.one * circle.radius;
            min = center - half;
            max = center + half;
            return true;
        }

        return false;
    }

        private Vector3 CalculateNearestEdgePosition()
    {
        Bounds bodyBounds = bodyCollider.bounds;
        Bounds partBounds = partCollider.bounds;

        Vector3 currentPos = transform.position;
        float halfW = partBounds.extents.x;
        float halfH = partBounds.extents.y;

        // 4 возможные точки отскока: влево, вправо, вниз, вверх от корпуса
        Vector3 leftPos = new Vector3(bodyBounds.min.x - halfW - ejectPadding, currentPos.y, currentPos.z);
        Vector3 rightPos = new Vector3(bodyBounds.max.x + halfW + ejectPadding, currentPos.y, currentPos.z);
        Vector3 bottomPos = new Vector3(currentPos.x, bodyBounds.min.y - halfH - ejectPadding, currentPos.z);
        Vector3 topPos = new Vector3(currentPos.x, bodyBounds.max.y + halfH + ejectPadding, currentPos.z);

        Vector3[] candidates = new Vector3[] { leftPos, rightPos, bottomPos, topPos };

        Vector3 bestTarget = currentPos;
        float bestValidDist = float.MaxValue;
        bool foundValidSide = false;

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 c = candidates[i];

            // Проверяем, влезет ли эта точка внутрь стола без сдвига обратно на корпус
            if (WorkbenchBounds.Instance != null)
            {
                Vector3 clamped = WorkbenchBounds.Instance.ClampInsideTable(c, partCollider);
                // Если точка вылезла за край стола (после зажима сдвинулась больше чем на 0.02) — эта сторона заблокирована!
                if (Vector2.Distance(c, clamped) > 0.02f)
                    continue;
            }

            float dist = Vector2.Distance(currentPos, c);
            if (dist < bestValidDist)
            {
                bestValidDist = dist;
                bestTarget = c;
                foundValidSide = true;
            }
        }

        // Если вдруг все 4 стороны зажаты — берём стандартный ближайший край с зажимом по столу
        if (!foundValidSide)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                float d = Vector2.Distance(currentPos, candidates[i]);
                if (d < minDist)
                {
                    minDist = d;
                    bestTarget = candidates[i];
                }
            }
        }

        return bestTarget;
    }

    private IEnumerator SmoothBounceTo(Vector3 targetPosition, bool checkOverlapAfter = false)
    {
        if (WorkbenchBounds.Instance != null)
            targetPosition = WorkbenchBounds.Instance.ClampInsideTable(targetPosition, partCollider);

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            yield return null;
        }

        transform.position = targetPosition;
        bounceCoroutine = null;

        // Если деталь оттолкнули корпусом — после приземления проверяем, не легла ли она на соседа
        if (checkOverlapAfter && !isInBox && preventTableOverlap)
        {
            TrySeparateFromOtherParts();
        }
    }

    private void NotifySlot(bool installed)
    {
        if (targetSlot == null) return;

        PartSlot slotScript = targetSlot.GetComponent<PartSlot>();
        if (slotScript != null)
        {
            if (installed)
                slotScript.OnPartInstalled();
            else
                slotScript.OnPartRemoved();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }

    private void TrySeparateFromOtherParts()
    {
        if (partCollider == null) return;

        Bounds myBounds = partCollider.bounds;
        float gap = 0.05f;

        for (int i = 0; i < TablePartRegistry.All.Count; i++)
        {
            var other = TablePartRegistry.All[i];
            if (other.transform == null || other.transform == transform) continue;
            if (other.isInBox != null && other.isInBox()) continue;

            Collider2D otherCol = other.collider;
            if (otherCol == null) continue;

            Bounds otherB = otherCol.bounds;

            bool overlapX = myBounds.min.x < otherB.max.x && myBounds.max.x > otherB.min.x;
            bool overlapY = myBounds.min.y < otherB.max.y && myBounds.max.y > otherB.min.y;

            if (overlapX && overlapY)
            {
                Vector3 currentPos = transform.position;
                float halfW = myBounds.extents.x;
                float halfH = myBounds.extents.y;

                float distLeft = Mathf.Abs(currentPos.x - otherB.min.x);
                float distRight = Mathf.Abs(otherB.max.x - currentPos.x);
                float distBottom = Mathf.Abs(currentPos.y - otherB.min.y);
                float distTop = Mathf.Abs(otherB.max.y - currentPos.y);

                float minDist = Mathf.Min(distLeft, distRight, distBottom, distTop);
                Vector3 targetPos = currentPos;

                if (minDist == distLeft)
                    targetPos.x = otherB.min.x - halfW - gap;
                else if (minDist == distRight)
                    targetPos.x = otherB.max.x + halfW + gap;
                else if (minDist == distBottom)
                    targetPos.y = otherB.min.y - halfH - gap;
                else
                    targetPos.y = otherB.max.y + halfH + gap;

                // Если сдвиг от соседа заталкивает деталь обратно на корпус — сдвигаем вдоль корпуса (по перпендикулярной оси)
                if (bodyCollider != null)
                {
                    Bounds bodyB = bodyCollider.bounds;
                    bool hitsBodyX = (targetPos.x - halfW) < bodyB.max.x && (targetPos.x + halfW) > bodyB.min.x;
                    bool hitsBodyY = (targetPos.y - halfH) < bodyB.max.y && (targetPos.y + halfH) > bodyB.min.y;

                    if (hitsBodyX && hitsBodyY)
                    {
                        // Если сдвигали по X и попали в корпус — обходим соседа сверху или снизу по Y
                        if (minDist == distLeft || minDist == distRight)
                        {
                            targetPos.x = currentPos.x;
                            targetPos.y = (currentPos.y >= otherB.center.y)
                                ? otherB.max.y + halfH + gap
                                : otherB.min.y - halfH - gap;
                        }
                        else
                        {
                            targetPos.y = currentPos.y;
                            targetPos.x = (currentPos.x >= otherB.center.x)
                                ? otherB.max.x + halfW + gap
                                : otherB.min.x - halfW - gap;
                        }
                    }
                }

                bounceCoroutine = StartCoroutine(SmoothBounceTo(targetPos));
                break;
            }
        }
    }

    /// Вызывается корпусом, если корпус положили поверх этой детали на столе
    public void PushOutFromBody(Collider2D bodyCol)
    {
        if (isInstalled || isInBox || isDragging || bodyCol == null || partCollider == null) return;

        Bounds partB = partCollider.bounds;
        Bounds bodyB = bodyCol.bounds;

        bool overlapX = partB.min.x <= bodyB.max.x && partB.max.x >= bodyB.min.x;
        bool overlapY = partB.min.y <= bodyB.max.y && partB.max.y >= bodyB.min.y;

        if (overlapX && overlapY)
        {
            if (bounceCoroutine != null)
            {
                StopCoroutine(bounceCoroutine);
                bounceCoroutine = null;
            }

            // Временно используем переданный коллайдер корпуса для расчёта ближайшего края
            Collider2D prevBody = bodyCollider;
            bodyCollider = bodyCol;
            Vector3 ejectTarget = CalculateNearestEdgePosition();
            bodyCollider = prevBody;

            bounceCoroutine = StartCoroutine(SmoothBounceTo(ejectTarget, true));

        }
    }
}
