using UnityEngine;
using UnityEngine.SceneManagement; // Обязательная библиотека для работы со сценами

public class MainMenu : MonoBehaviour
{
    [Tooltip("Точное название сцены, которую нужно загрузить")]
    public string gameSceneName = "Level1";

    // Метод для кнопки «Играть»
    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // Метод для кнопки «Выход»
    public void QuitGame()
    {
        Debug.Log("Выход из игры!"); // Проверка в редакторе Unity
        Application.Quit();            // Закроет игру в готовой сборке (.exe)
    }
}
