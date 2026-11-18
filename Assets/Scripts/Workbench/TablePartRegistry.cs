using System.Collections.Generic;
using UnityEngine;

public class TablePartRegistry : MonoBehaviour
{
    public struct PartEntry
    {
        public Transform transform;
        public Collider2D collider;
        public System.Func<bool> isInBox; // Ссылка на флаг isInBox конкретной детали
    }

    public static readonly List<PartEntry> All = new List<PartEntry>();

    public static void Register(Transform t, Collider2D col, System.Func<bool> isInBoxGetter)
    {
        Unregister(t);
        All.Add(new PartEntry { transform = t, collider = col, isInBox = isInBoxGetter });
    }

    public static void Unregister(Transform t)
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i].transform == null || All[i].transform == t)
                All.RemoveAt(i);
        }
    }
}
