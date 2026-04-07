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
        //To be set up, idk what settings we need rn
    }

    public void Quit()
    {
        print("Qutting Game");
        Application.Quit();
    }
}