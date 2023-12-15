using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using UnityEngine.UIElements;

public class BanditScript : MonoBehaviour
{
    public float moveSpeed = 5.0f,jumpForce=10.0f;
    private Rigidbody2D rb;
    private Identity identity;
    public Animator anim;
    public bool isJumping;
    public LayerMask groundLayer;
    public Transform groundCheckPoint;
    public float checkRadius = 0.5f;
    public GameServer gameServer;
    public GameClient gameClient;
    public string playerID=string.Empty;
    public string memberFlag=string.Empty;
    public  bool isHost = false;
    public float hostHorizontalInput,nonHostHorizontalInput;
    private string attackMessage = "Attack";
    public BanditAnimatorController banditAnimController;

    public Vector3 targetPosition; // Target position to interpolate to
    public Quaternion targetRotation; // Target rotation to interpolate to 
    public float smoothingSpeed = 5.0f;
    public enum BanditActionState { Idle, Run, Jump,Attack}

    public BanditActionState banditActionState = BanditActionState.Idle;

  
    private void Awake()
    {
        gameServer=FindObjectOfType<GameServer>();
        gameClient=FindObjectOfType<GameClient>();
        gameServer.inGameBandits.Add(this);

        if(gameClient!=null)
        isHost = playerID == gameClient.localHostClientId;
       
    }
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        identity = GetComponent<Identity>();
        anim = GetComponent<Animator>();
        banditAnimController = GetComponent<BanditAnimatorController>();
             
        Debug.Log($"player id is {playerID}.");


        IdentityChecks();
      
    }

    // Update is called once per frame
    void Update()
    {
       
        HandleMovementAndActions();

        SendMovementMessages();

        //InterpolateMovements();

        SendAnimationMessages();

    }

   
    private void IdentityChecks()// Needs fixing because disabling the BanditScript is not a good idea because the animations can't be played
    {
        if (gameClient.isHost)
        {
            //gameServer.hostBandit.GetComponent<BanditAnimatorController>().enabled = false;
            gameServer.nonHostBandit.GetComponent<BanditScript>().enabled = false;
           
           

            Debug.Log("Identify host!");
        }
        else
        {
            //gameServer.nonHostBandit.GetComponent<BanditAnimatorController>().enabled = false;
            gameServer.hostBandit.GetComponent<BanditScript>().enabled = false;
            
           

            Debug.Log("Identify non-host!");
        }
      
    }
    public void SendMovementMessages()
    {
        if (gameClient.isHost)
        {
            string hostMovementMsg = $"HostMovement:{transform.position.x}:{transform.position.y}:{transform.rotation.eulerAngles.y}:{transform.rotation.eulerAngles.z}";
            gameClient.SendMessageToServer(hostMovementMsg);
        }
        else
        {
            string nonHostMovementMsg = $"NonHostMovement:{transform.position.x}:{transform.position.y}:{transform.rotation.eulerAngles.y}:{transform.rotation.eulerAngles.z}";
            gameClient.SendMessageToServer(nonHostMovementMsg);
        }
    }

    public void SendAnimationMessages() //Sync for running animations
    {
        if (gameClient.isHost)
        {
            string animationMsg = $"HostAnimated:{hostHorizontalInput}";
            gameClient.SendMessageToServer(animationMsg);
        }
        else
        {
            string animationMsg = $"NonHostAnimated:{nonHostHorizontalInput}";
            gameClient.SendMessageToServer(animationMsg);
        }
       
    }

    private void HandleMovementAndActions()
    {
        if (gameClient.isHost)
        {
            BanditMovement();

            // Handle Jumping
            if (Input.GetButtonDown("Jump") && !isJumping)
            {
                Jump();
            }
            if (isJumping) { banditActionState = BanditActionState.Jump; /*SendStateChangeMessage();*/ }
            else if (!isJumping && hostHorizontalInput == 0) { banditActionState = BanditActionState.Idle; /*SendStateChangeMessage();*/ }
            // Ground check
            isJumping = !Physics2D.OverlapCircle(groundCheckPoint.position, checkRadius, groundLayer);
            Debug.Log("Is Jumping?" + isJumping);

            if (Input.GetButtonDown("Fire1"))
            {
                Attack();

            }
        }
        else
        {
            BanditMovement();

            // Handle Jumping
            if (Input.GetButtonDown("Jump") && !isJumping)
            {
                Jump();
            }
            if (isJumping) { banditActionState = BanditActionState.Jump; /*SendStateChangeMessage();*/ }
            else if (!isJumping && nonHostHorizontalInput == 0) { banditActionState = BanditActionState.Idle; /*SendStateChangeMessage();*/ }
            // Ground check
            isJumping = !Physics2D.OverlapCircle(groundCheckPoint.position, checkRadius, groundLayer);
            Debug.Log("Is Jumping?" + isJumping);

            if (Input.GetButtonDown("Fire1"))
            {
                Attack();

            }
        }
        
    }
    void BanditMovement()
    {
        if (gameClient.isHost)
        {
            hostHorizontalInput = Input.GetAxis("Horizontal");
            Vector2 moveDirection = new Vector2(hostHorizontalInput, 0);

            // Move the Bandit
            rb.velocity = new Vector2(moveDirection.x * moveSpeed, rb.velocity.y);


            if (hostHorizontalInput != 0)
            {
                anim.SetBool("Run", true);
                banditActionState = BanditActionState.Run;
                //SendStateChangeMessage();
            }

            else
            {
                anim.SetBool("Run", false);
                banditActionState = BanditActionState.Idle;
                //SendStateChangeMessage();
            }


            /*Handle rotation based on identity*/
            if (identity.heavyBandit)
            {
                if (hostHorizontalInput < 0)
                    transform.rotation = new Quaternion(0, 180f, 0, 0);

                else
                    transform.rotation = new Quaternion(0, 0, 0, 0);
            }
            else if (identity.lightBandit)
            {
                if (hostHorizontalInput <= 0)               
                    transform.rotation = new Quaternion(0, 180f, 0, 0);
                
                else               
                    transform.rotation = new Quaternion(0, 0, 0, 0);
                
            }
        }
        else
        {
            nonHostHorizontalInput = Input.GetAxis("Horizontal");
            Vector2 moveDirection = new Vector2(nonHostHorizontalInput, 0);

            // Move the Bandit
            rb.velocity = new Vector2(moveDirection.x * moveSpeed, rb.velocity.y);


            if (nonHostHorizontalInput != 0)
            {
                anim.SetBool("Run", true);
                banditActionState = BanditActionState.Run;
                //SendStateChangeMessage();
            }

            else
            {
                anim.SetBool("Run", false);
                banditActionState = BanditActionState.Idle;
                //SendStateChangeMessage();
            }


            /*Handle rotation based on identity*/
            if (identity.heavyBandit)
            {
                if (nonHostHorizontalInput < 0)
                    transform.rotation = new Quaternion(0, 180f, 0, 0);

                else
                    transform.rotation = new Quaternion(0, 0, 0, 0);
            }
            else if (identity.lightBandit)
            {
                if (nonHostHorizontalInput <= 0)
                {
                    transform.rotation = new Quaternion(0, 180f, 0, 0);
                }
                else
                {
                    transform.rotation = new Quaternion(0, 0, 0, 0);
                }
            }
        }
                    
    }

    public void SendStateChangeMessage()
    {
        string message = $"StateChange:{playerID}:{banditActionState}";
        gameClient.SendMessageToServer(message);
    }

    
    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        anim.SetTrigger("Jump");
    }

    void Attack()
    {
        //anim.SetTrigger("Attack");
        banditActionState = BanditActionState.Attack;



        if (gameClient.isHost)
        {
            string attackMsg = $"HostAttack:{attackMessage}";
            //gameClient.SendUDPMessage(attackMsg,gameServer.udpEndPoint);          
            gameClient.SendMessageToServer(attackMsg);
            //SendStateChangeMessage();
            gameServer.hostBandit.GetComponent<BanditAnimatorController>().anim.SetTrigger(attackMessage);
        }
        else
        {
            string attackMsg = $"NonHostAttack:{attackMessage}";
            //gameClient.SendUDPMessage(attackMsg,gameServer.udpEndPoint);           
            gameClient.SendMessageToServer(attackMsg);
            gameServer.nonHostBandit.GetComponent<BanditAnimatorController>().anim.SetTrigger(attackMessage);
        }

    }

   

}
