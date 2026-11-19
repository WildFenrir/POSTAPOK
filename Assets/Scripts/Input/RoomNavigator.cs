using UnityEngine;
using UnityEngine.UI;

public class RoomNavigator : MonoBehaviour
{
    [Header("Стартовая комната")]
    public RoomNode currentRoom;

    [Header("Кнопки стрелок в Canvas")]
    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button upArrowButton;
    public Button downArrowButton;

    [Header("Настройки перехода")]
    [Tooltip("Если 0 — мгновенный переход без задержки. Если 0.15 — быстрый плавный сдвиг")]
    public float transitionDuration = 0f;

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        // Подписываем кнопки стрелок на методы перехода
        if (leftArrowButton != null) leftArrowButton.onClick.AddListener(GoLeft);
        if (rightArrowButton != null) rightArrowButton.onClick.AddListener(GoRight);
        if (upArrowButton != null) upArrowButton.onClick.AddListener(GoUp);
        if (downArrowButton != null) downArrowButton.onClick.AddListener(GoDown);

        if (currentRoom != null)
            MoveToRoomInstant(currentRoom);
    }

    public void GoLeft()  { if (currentRoom != null && currentRoom.leftRoom != null)  SwitchRoom(currentRoom.leftRoom); }
    public void GoRight() { if (currentRoom != null && currentRoom.rightRoom != null) SwitchRoom(currentRoom.rightRoom); }
    public void GoUp()    { if (currentRoom != null && currentRoom.upRoom != null)    SwitchRoom(currentRoom.upRoom); }
    public void GoDown()  { if (currentRoom != null && currentRoom.downRoom != null)  SwitchRoom(currentRoom.downRoom); }

    private void SwitchRoom(RoomNode targetRoom)
    {
        currentRoom = targetRoom;

        if (transitionDuration <= 0.001f)
        {
            MoveToRoomInstant(targetRoom);
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SmoothMoveToRoom(targetRoom));
        }
    }

    private void MoveToRoomInstant(RoomNode room)
    {
        Vector3 pos = room.transform.position;
        mainCam.transform.position = new Vector3(pos.x, pos.y, mainCam.transform.position.z);
        UpdateArrowsUI();
    }

    private System.Collections.IEnumerator SmoothMoveToRoom(RoomNode room)
    {
        // Скрываем стрелки на время короткого перелёта камеры
        SetAllArrowsActive(false);

        Vector3 startPos = mainCam.transform.position;
        Vector3 endPos = new Vector3(room.transform.position.x, room.transform.position.y, startPos.z);
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            mainCam.transform.position = Vector3.Lerp(startPos, endPos, smoothT);
            yield return null;
        }

        mainCam.transform.position = endPos;
        UpdateArrowsUI();
    }

    // Показываем только те стрелки, куда реально есть проход из текущей комнаты
    private void UpdateArrowsUI()
    {
        if (currentRoom == null) return;

        if (leftArrowButton != null)  leftArrowButton.gameObject.SetActive(currentRoom.leftRoom != null);
        if (rightArrowButton != null) rightArrowButton.gameObject.SetActive(currentRoom.rightRoom != null);
        if (upArrowButton != null)    upArrowButton.gameObject.SetActive(currentRoom.upRoom != null);
        if (downArrowButton != null)  downArrowButton.gameObject.SetActive(currentRoom.downRoom != null);
    }

    private void SetAllArrowsActive(bool state)
    {
        if (leftArrowButton != null)  leftArrowButton.gameObject.SetActive(state);
        if (rightArrowButton != null) rightArrowButton.gameObject.SetActive(state);
        if (upArrowButton != null)    upArrowButton.gameObject.SetActive(state);
        if (downArrowButton != null)  downArrowButton.gameObject.SetActive(state);
    }
}