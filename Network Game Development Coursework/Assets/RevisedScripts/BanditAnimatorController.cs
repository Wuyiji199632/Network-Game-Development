using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BanditAnimatorController : MonoBehaviour
{
    public Animator anim;
    public BanditScript banditScript;
    public float horizontalInput = 0;
    public bool isLocalPlayer = false;
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
        AnimPlayLogics();
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

    private void AnimPlayLogics()
    {
        if(banditScript.gameClient.isHost)
        horizontalInput = banditScript.hostHorizontalInput;
        else
        horizontalInput = banditScript.nonHostHorizontalInput;

        if (horizontalInput != 0)
        {
            anim.SetBool("Run", true);


        }
        else
        {
            anim.SetBool("Run", false);
        }


    }
}
