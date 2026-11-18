using UnityEngine;

public class PartSlot : MonoBehaviour
{
    [HideInInspector]
    public bool isOccupied = false;

    public void OnPartInstalled()
    {
        isOccupied = true;
        Debug.Log("Деталь установлена в слот: " + gameObject.name);
    }

    public void OnPartRemoved()
    {
        isOccupied = false;
        Debug.Log("Деталь снята со слота: " + gameObject.name);
    }
}