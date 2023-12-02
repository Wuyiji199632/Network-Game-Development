using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BanditAnimatorController : MonoBehaviour
{
    public Animator anim;
    public BanditScript banditScript;
    public float horizontalInput = 0;
    public bool isLocalPlayer = false;
    public string attackMsg=string.Empty;
    // Start is called before the first frame update
    void Start()
    {
        anim=GetComponent<Animator>();
        banditScript=GetComponent<BanditScript>();
        if (banditScript.gameClient.isHost)
        {
            isLocalPlayer = banditScript.playerID == banditScript.gameClient.localHostClientId;
        }
        else
        {
           
            isLocalPlayer = banditScript.playerID == banditScript.gameClient.localNonHostClientId;
        }
      
    }

    // Update is called once per frame
    void Update()
    {
        AnimPlayLogicsForSync();
    }

    private void StateChanges()
    {
        switch (banditScript.banditActionState)
        {
            case BanditScript.BanditActionState.Idle:
                banditScript.anim.SetBool("Run", false); break;
            case BanditScript.BanditActionState.Run:
                banditScript.anim.SetBool("Run", true); break;
            case BanditScript.BanditActionState.Jump:
                banditScript.anim.SetTrigger("Jump"); break;
            case BanditScript.BanditActionState.Attack:
                banditScript.anim.SetTrigger("Attack"); break;
        }
    }

    private void AnimPlayLogicsForSync()
    {
        #region Running Animation Playing Logics
        // Determine the horizontal input based on whether the bandit is local or non-local
        if (isLocalPlayer)
        {
            // For local player, use the local input
            horizontalInput = Input.GetAxis("Horizontal");
        }
        else
        {
            // For non-local players, use the input received from the network
            horizontalInput = banditScript.isHost ? banditScript.gameClient.remoteHostHorizontalInput : banditScript.gameClient.remoteNonHostHorizontalInput;

            Debug.Log($"remote player's horizontal input is {horizontalInput}");
           
        }

        // Set the animation state based on the horizontal input
        anim.SetBool("Run", horizontalInput != 0);
        #endregion

        #region Attack Animation Playing Logics
       

        #endregion
    }

}
