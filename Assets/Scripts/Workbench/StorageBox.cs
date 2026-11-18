using System.Collections.Generic;
using UnityEngine;

public class StorageBox : MonoBehaviour
{
    public static readonly List<StorageBox> All = new List<StorageBox>();

    [Header("Какие детали принимает коробка")]
    [Tooltip("Теги деталей, которые можно положить в эту коробку")]
    public string[] acceptTags = new string[0];

    [Tooltip("Разрешить класть в эту коробку любые детали")]
    public bool acceptAny = false;

    [Header("Угол наклона деталей внутри коробки")]
    public float minTiltAngle = -30f;
    public float maxTiltAngle = 30f;

    public Collider2D BoxCollider { get; private set; }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Awake()
    {
        BoxCollider = GetComponent<Collider2D>();
    }

    /// Проверяет, подходит ли коробка для детали с указанными тегами
    public bool CanAccept(string[] partTags)
    {
        if (acceptAny) return true;
        if (acceptTags == null || acceptTags.Length == 0) return false;
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

    /// Ищет подходящую коробку под указанной точкой на экране
    public static StorageBox FindBoxAt(Vector2 worldPoint, string[] partTags)
    {
        for (int i = 0; i < All.Count; i++)
        {
            StorageBox box = All[i];
            if (box == null || box.BoxCollider == null) continue;

            if (box.BoxCollider.OverlapPoint(worldPoint) && box.CanAccept(partTags))
                return box;
        }

        return null;
    }
}