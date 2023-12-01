using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BanditAnimatorController : MonoBehaviour
{
    public Animator anim;
    public BanditScript banditScript;
    public float horizontalInput = 0;
    // Start is called before the first frame update
    void Start()
    {
        anim=GetComponent<Animator>();
        banditScript=GetComponent<BanditScript>();
        
      
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

        horizontalInput = banditScript.horizontalInput;
        
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
