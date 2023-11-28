using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;


public class BanditScript : MonoBehaviour
{
    public float moveSpeed = 5.0f,jumpForce=10.0f;
    private Rigidbody2D rb;
    private Identity identity;
    private Animator anim;
    private bool isJumping;
    public LayerMask groundLayer;
    public Transform groundCheckPoint;
    public float checkRadius = 0.5f;
    public GameServer gameServer;
    public GameClient gameClient;
    
    public string playerID=string.Empty;
    public string memberFlag=string.Empty;
    private bool isHost = false;
    private void Awake()
    {
        gameServer=FindObjectOfType<GameServer>();
        gameClient=FindObjectOfType<GameClient>();
        gameServer.inGameBandits.Add(this);
        isHost = playerID == gameClient.localHostClientId;
       
    }
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        identity = GetComponent<Identity>();
        anim = GetComponent<Animator>();
        
        Debug.Log($"player id is {playerID}.");


        IdentityChecks();
      
    }

    // Update is called once per frame
    void Update()
    {

        HandleMovementAndActions();
      
    }
    private void IdentityChecks()
    {
        if (isHost)
        {
           
            gameServer.nonHostBandit.GetComponent<BanditScript>().enabled = false;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            string hostMovementMsg = $"HostMovement:{position.x},{position.y},{position.z}:{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
            gameClient.SendUDPMessage(hostMovementMsg);
            Debug.Log("Identify host!");
        }
        else
        {
           
            gameServer.hostBandit.GetComponent<BanditScript>().enabled = false;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            string NonHostMovementMsg = $"NonHostMovement:{position.x},{position.y},{position.z}:{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
            gameClient.SendUDPMessage(NonHostMovementMsg);
            Debug.Log("Identify non-host!");
        }

       
    }

    private void HandleMovementAndActions()
    {
        
        BanditMovement();

        // Handle Jumping
        if (Input.GetButtonDown("Jump") && !isJumping)
        {
            Jump();
        }

        // Ground check
        isJumping = !Physics2D.OverlapCircle(groundCheckPoint.position, checkRadius, groundLayer);
        Debug.Log("Is Jumping?" + isJumping);

        if (Input.GetButtonDown("Fire1"))
        {
            Attack();
        }
    }
    void BanditMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        Vector2 moveDirection = new Vector2(horizontalInput, 0);

        // Move the Bandit
        rb.velocity = new Vector2(moveDirection.x * moveSpeed, rb.velocity.y);



        if(horizontalInput != 0) 
            anim.SetBool("Run", true);
        else
            anim.SetBool("Run", false);



        /*Handle rotation based on identity*/
        if (identity.heavyBandit)
        {
            if (horizontalInput < 0)
                transform.rotation = new Quaternion(0, 180f, 0, 0);

            else
                transform.rotation = new Quaternion(0, 0, 0, 0);
        }
        else if (identity.lightBandit)
        {
            if (horizontalInput <= 0)
            {
                transform.rotation = new Quaternion(0, 180f, 0, 0);
            }
            else
            {
                transform.rotation = new Quaternion(0, 0, 0, 0);
            }
        }

        //SendPositionUpdate();
    }
  
    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        anim.SetTrigger("Jump");
    }

    void Attack()
    {
        anim.SetTrigger("Attack");
    }
     
}
