using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        if (instance != null && instance.consoleText != null)
        {
            instance.consoleText.text += message + "\n";
        }
    }
}
