using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
 
public class MainMenu : MonoBehaviour
{
    [Header("Start Game")]
    public string gameplaySceneName = "Rooms";
    public bool useFadeTransition = true;
    public ScreenFader fader;

    public void Play()
    {
        if (useFadeTransition)
        {
            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>();

            if (fader != null)
            {
                StartCoroutine(PlayWithFade());
                return;
            }
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator PlayWithFade()
    {
        if (fader != null)
            yield return StartCoroutine(fader.FadeToBlack());

        SceneManager.LoadScene(gameplaySceneName);
    }
    
    public void OpenSettings()
    {
        PauseMenuUI pauseMenu = FindFirstObjectByType<PauseMenuUI>();
        if (pauseMenu == null)
        {
            GameObject root = new GameObject("PauseMenuUI");
            pauseMenu = root.AddComponent<PauseMenuUI>();
        }

        if (pauseMenu != null)
            pauseMenu.OpenSettingsFullscreenFromMainMenu();
    }

    public void Quit()
    {
        print("Qutting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}