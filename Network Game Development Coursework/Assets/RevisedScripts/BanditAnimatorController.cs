using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
public class BanditAnimatorController : MonoBehaviour //This class syncs the animations and collision logics between players`
{
    public Animator anim;
    public BanditScript banditScript;
    public float horizontalInput = 0;
    public bool isLocalPlayer = false;
    public string attackMsg=string.Empty;
    public float health = 100;
    public float damageAmount = 10,localDamageAmount=6f;
    public float predictedDamageInterpolationAmount = 0.5f;
    public SpriteRenderer sprite;
    public SpriteRenderer opponentSprite;
    public float colorChangeDuration = 1f;
    public Slider healthSlider;
    public BanditAnimatorController opponentBandit;
    public float smoothingSpeed = 3.0f;
    public Collider2D opponentCollider=null;
    public float distanceToOpponent = 0;
    // Start is called before the first frame update
    void Start()
    {
        anim=GetComponent<Animator>();
        banditScript=GetComponent<BanditScript>();
        sprite=GetComponent<SpriteRenderer>();
        if (banditScript.gameClient.isHost)
        {
            isLocalPlayer = banditScript.playerID == banditScript.gameClient.localHostClientId;
        }
        else
        {
           
            isLocalPlayer = banditScript.playerID == banditScript.gameClient.localNonHostClientId;
        }

        if (banditScript.gameClient.isHost)
        {
            opponentBandit=banditScript.gameServer.nonHostBandit.GetComponentInParent<BanditAnimatorController>();

        }
        else
        {
            opponentBandit = banditScript.gameServer.hostBandit.GetComponentInParent<BanditAnimatorController>();
        }
      
    }

    // Update is called once per frame
    void Update()
    {
        MovementAnimPlayLogicsForSync();

        UpdateHealth();

        CalculateDistanceToOpponent();


    }
    private void CalculateDistanceToOpponent()
    {
        distanceToOpponent=Vector2.Distance(this.transform.position,opponentBandit.transform.position);
    }
   

    private void MovementAnimPlayLogicsForSync()
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
    }
    #endregion


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.GetComponent<BanditAnimatorController>()!=null)
        {
            opponentCollider = collision;
            opponentSprite=collision.gameObject.GetComponent<BanditAnimatorController>().sprite;

            opponentCollider.GetComponent<BanditAnimatorController>().health +=predictedDamageInterpolationAmount;
            if (distanceToOpponent <= 150.0f)
            {
                
                if (gameObject.GetComponent<BanditScript>().gameClient.isHost)
                {
                    string damageMsgFromHost = $"HostApplyDamage:{damageAmount}";
                    banditScript.gameClient.SendMessageToServer(damageMsgFromHost);
                }
                else
                {
                    string damageMsgFromNonHost = $"NonHostApplyDamage:{damageAmount}";
                    banditScript.gameClient.SendMessageToServer(damageMsgFromNonHost);
                }

                opponentSprite.color = Color.red;

                StartCoroutine(RecoverColorAfterTakingDamage());

                UpdateHealth();
            }
                    
        }
    }

    public  IEnumerator RecoverColorAfterTakingDamage()
    {
        if(opponentSprite!=null)
        {
            float elapsed = 0;
            while (elapsed < colorChangeDuration)
            {
                // Gradually interpolate from red to white
                opponentSprite.color = Color.Lerp(Color.red, Color.white, elapsed / colorChangeDuration);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure the final color is white
            opponentSprite.color = Color.white;
        }
       
    }

    public void UpdateHealth()
    {
        if (healthSlider)
        {
            healthSlider.value = health/100.0f;
        }
        
        if (health <= 0)
        {
            anim.SetTrigger("Die");
            banditScript.enabled = false;
            string endMsg = $"GameEnds:";
            banditScript.gameClient.SendMessageToServer(endMsg);
           
        }
    }


}
