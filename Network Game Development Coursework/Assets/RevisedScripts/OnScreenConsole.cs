using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class OnScreenConsole : MonoBehaviour
{
    public Text consoleText;
    private static OnScreenConsole instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
           DontDestroyOnLoad(gameObject);
        }

       
    }

    public static void Log(string message)
    {
        if (SceneManager.GetActiveScene().ToString() != "MainMenu") return;
        if (instance != null && instance.consoleText != null)
        {
            
            instance.consoleText.text += message + "\n";
        }
        else
        {
            // If the instance isn't found, log an error or handle the case as needed
            Debug.LogError("OnScreenConsole instance or consoleText not found.");
        }
    }

    
}
