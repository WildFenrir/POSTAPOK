using UnityEngine;

public class RoomNode : MonoBehaviour
{
    [Header("Название локации (для удобства)")]
    public string roomName = "Верстак";

    [Header("Куда ведут стрелки из этой комнаты (если пусто — стрелка скроется)")]
    public RoomNode leftRoom;
    public RoomNode rightRoom;
    public RoomNode upRoom;
    public RoomNode downRoom;
}
