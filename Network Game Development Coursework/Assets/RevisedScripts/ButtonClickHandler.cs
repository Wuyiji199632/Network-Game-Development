using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Net.Sockets;
using System.Net;




public class ButtonClickHandler : MonoBehaviour
{
    public Button quitGameBtn, mainMenuBtn;
    public Button heavyBanditBtn, lightBanditBtn;
    public GameServer gameServer;
    public GameClient gameClient;
    private void Awake()
    {
       gameServer=FindObjectOfType<GameServer>();
       gameClient=FindObjectOfType<GameClient>();
    }
    // Start is called before the first frame update
    void Start()
    {
       
        quitGameBtn.onClick.AddListener(gameClient.QuitGame);
        mainMenuBtn.onClick.AddListener(gameClient.GoBackToMainMenu);
        mainMenuBtn.onClick.AddListener(gameClient.BackToMenuUIChange);
        heavyBanditBtn.onClick.AddListener(() => gameClient.SelectCharacter("HeavyBandit"));
        lightBanditBtn.onClick.AddListener(() => gameClient.SelectCharacter("LightBandit"));
    }

   
}
