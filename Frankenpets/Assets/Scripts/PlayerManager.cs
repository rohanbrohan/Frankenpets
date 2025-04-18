using UnityEngine;
using System.Diagnostics;
using Unity.Cinemachine;
// using UnityEditor.VersionControl;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using System;
using System.Collections;
using System.Reflection;

public class PlayerManager : MonoBehaviour
{

    [Header("Player Input References")]
    public InputHandler player1Input;
    public InputHandler player2Input;

    [Header("Pet Info")]
    public GameObject P1Half; // Only use for initializing
    public GameObject P2Half; // Only use for initializing
    public GameObject P1Magnet; // Only use for initializing
    public GameObject P2Magnet; // Only use for initializing
    private FixedJoint fixedJoint;
    public Player P1 = new Player();
    public Player P2 = new Player();

    [Header("Movement Variables")]
    public float walkSpeed = 0.6f;

    private float immutableWalkSpeed;
    private float immutableTurnSpeed;
    public float turnSpeed = 1.0f;

    public bool altMovement = false; // whether to use the alternative movement scheme
    // private bool isFrozen = false; // whether the half's RigidBody's position is frozen in place 

    [Header("Splitting Variables")]
    public float reconnectionDistance = 0.3f;
    public float splitTime = 1.2f;
    public KeyCode reconnectToggleKey = KeyCode.Space;
    private Stopwatch splitStopwatch = new Stopwatch();
    private Quaternion initialRelativeRotation;
    private GameObject frontHalf;
    private GameObject backHalf;
    private GameObject frontMagnet;
    private GameObject backMagnet;
    private bool splitCondition = false; // stretching rig listens for this
    


    [Header("Switching Variables")]
    public float switchTime = 1.2f;
    public GameObject catFront;
    public GameObject dogFront;
    public GameObject catBack;
    public GameObject dogBack;
    private bool player1SwitchPressed = false;
    private bool player2SwitchPressed = false;

    public PetStationManager stationManager;

    [Header("Cameras")]
    public CinemachineCamera player1Camera;
    public CinemachineCamera player2Camera;
    public CameraMovement cameraMovement;
    public CinemachineCamera mainCamera;

    private Stopwatch switchStopwatch = new Stopwatch();
    
    // Others
    private MessageManager messageManager;

    [Header("Tutorial Variables")]
    public bool canReconnect = true;
    public bool canSwitch = true;
    public bool canSplit = true;

    // Tutorial overlay
    [Header("Tutorial Overlay")]
    public TutorialText tutorialTextScript;

    // check whether is currently climbing
    private bool isClimbing = false;
    private bool isMoving = false;

    void Awake()
    {
        Screen.SetResolution(1920, 1080, true); // force their resolution to be 1920x1080 lol

        immutableWalkSpeed = walkSpeed;
        immutableTurnSpeed = turnSpeed;

        // Initialize the players
        P1.PlayerNumber = 1;
        P1.IsFront = true;
        P1.Half = P1Half;
        P1.Magnet = P1Magnet;

        if (P1.Half == catFront)
        {
            P1.Species = "cat";
        } else if (P1.Half == dogFront) 
        {
            P1.Species = "dog";
        } else 
        {
            UnityEngine.Debug.LogError("Player 1 set to invalid half.");
        }

        P2.PlayerNumber = 2;   
        P2.IsFront = false;
        P2.Species = "dog";
        P2.Half = P2Half;
        P2.Magnet = P2Magnet;

        if (P2.Half == catBack)
        {
            P2.Species = "cat";
        } else if (P2.Half == dogBack) 
        {
            P2.Species = "dog";
        } else 
        {
            UnityEngine.Debug.LogError("Player 2 set to invalid half.");
        }

        // Initialize splitting variables 
        frontMagnet = getFrontMagnet();
        backMagnet = getBackMagnet();
        frontHalf = getFrontHalf();
        backHalf = getBackHalf();
        initialRelativeRotation = Quaternion.Inverse(frontHalf.transform.rotation) * backHalf.transform.rotation;

        alignHalves();
        setJoint();
        // updatePlayerIcons();

        GameObject messageObject = GameObject.Find("Messages");
        if (messageObject != null)
        {
            messageManager = messageObject.GetComponent<MessageManager>();
            if (messageManager != null)
            {
                UnityEngine.Debug.Log("MessageManager component found!");
            }
        } else
        {
            UnityEngine.Debug.LogError("GameObject 'Messages' not found in the scene.");
        }
    }
    
    bool tutOverlayDone()
    {
        // if (tutorialTextScript == null) return true;
        
        return tutorialTextScript.overlayDone();

    }

    // Update is called once per frame
    void Update()
    {
        // if (!tutOverlayDone()){
        //     return;
        // }

        if (fixedJoint != null)
        {
            if (isClimbing){
                runClimbMovementLogic();
            } else{
                runMovementLogic();
            }
        }
        else {
            runSeparatedMovementLogic();
        }

        CheckSwitchInput();
        CheckReconnectInput();
        
        runSplitLogic();
        runSwitchLogic();
        runCameraLogic();
        EnsureUpright();

    }
    void FixedUpdate()
    {
        if (fixedJoint != null)
        {
            if (isClimbing){
                runClimbMovementLogic();
                Rigidbody frontRb = frontHalf.GetComponent<Rigidbody>();
                if (isMoving){
                    frontRb.linearVelocity = Vector3.up * walkSpeed;
                } else{
                    frontRb.linearVelocity = Vector3.zero;
                }
            } else{
                runMovementLogic();
            }
            
        }
        else {
            runSeparatedMovementLogic();
        }        
    }

    // ADVANCED GETTERS/SETTERS ////////////////////////////////////////////
    public GameObject getFrontHalf()
    {
        if (P1.IsFront) return P1.Half;
        else return P2.Half;
    }

    public GameObject getBackHalf()
    {
        if (!P1.IsFront) return P1.Half;
        else return P2.Half;
    }

    public GameObject getFrontMagnet()
    {
        if (P1.IsFront) return P1.Magnet;
        else return P2.Magnet;
    }

    public GameObject getBackMagnet()
    {
        if (!P1.IsFront) return P1.Magnet;
        else return P2.Magnet;
    }
    public void setJoint(){

            frontHalf.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationX;
            frontHalf.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationZ;
            backHalf.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationX;
            backHalf.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationZ;

            // Create a new FixedJoint
            fixedJoint = frontHalf.AddComponent<FixedJoint>();
            fixedJoint.connectedBody = backHalf.GetComponent<Rigidbody>();

            // Set anchor points
            fixedJoint.anchor = frontMagnet.transform.localPosition;
            fixedJoint.connectedAnchor = backMagnet.transform.localPosition;

            // Apply Rigidbody constraints to prevent tilting
            frontHalf.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public FixedJoint getJoint()
    {
        return getFrontHalf().GetComponent<FixedJoint>();
    }
    // ADVANCED GETTERS/SETTERS ////////////////////////////////////////////


    // GETTERS/SETTERS FOR ALLOWING PET ACTIONS /////////////////////////////
    // Used to determine when a pet can split/switch/reconnect in specific points of the tutorial
    // Also used in stretch and shrink animation
    public void setCanReconnect(bool canReconnect)
    {
        this.canReconnect = canReconnect;
    }
    public void setCanSplit(bool canSplit)
    {
        this.canSplit = canSplit;
    }
    public void setCanSwitch(bool canSwitch)
    {
        this.canSwitch = canSwitch;
    }
    public bool getCanReconnect()
    {
        return this.canReconnect;
    }
    public bool getCanSplit()
    {
        return this.canSplit;
    }
    public bool getCanSwitch()
    {
        return this.canSwitch;
    }
    public bool getIsSplit()
    {
        return this.isSplit;
    }
    public void setIsClimb(bool climbing)
    {
        isClimbing = climbing;
    }
    // GETTERS/SETTERS FOR ALLOWING PET ACTIONS /////////////////////////////


    // PLAYER INPUT CHECKERS ////////////////////////////////////////////
    // private bool isHolding; 
    private Stopwatch player1Stopwatch = new Stopwatch();
    private Stopwatch player2Stopwatch = new Stopwatch();   
    private bool player1HoldingSwitch = false; 
    private bool player2HoldingSwitch = false; 
    public bool CheckSwitchInput() {
        player1SwitchPressed = player1Input.GetSwitchPressed();
        player2SwitchPressed = player2Input.GetSwitchPressed();

        if (player1SwitchPressed && !player2SwitchPressed && canSwitch)
        {
            if (!player1Stopwatch.IsRunning)
            {
                player1Stopwatch.Start();
            }
      
            if (player1Stopwatch.Elapsed.TotalSeconds >= 0.75f && !player1HoldingSwitch)
            {
                player1HoldingSwitch = true;

                messageManager.endP2WantsToSwitch();
                messageManager.startP1WantsToSwitch();
                AudioManager.Instance.PlayUIMeowSFX();
            } 
        } 
        else
        {
            player1Stopwatch.Reset();
            if (player1HoldingSwitch)
            {
                player1HoldingSwitch = false;
                messageManager.endP1WantsToSwitch(); // Hide P1's switch message
            }
        }
        
        if (!player1SwitchPressed && player2SwitchPressed && canSwitch)
        {   
            if (!player2Stopwatch.IsRunning)
            {
                player2Stopwatch.Start();
            }

            if (player2Stopwatch.Elapsed.TotalSeconds >= 0.75f && !player2HoldingSwitch)
            {
                player2HoldingSwitch = true;

                messageManager.endP1WantsToSwitch();
                messageManager.startP2WantsToSwitch();
                AudioManager.Instance.PlayUIBarkSFX();
            } 
        } 
        else
        {
            player2Stopwatch.Reset();
            if (player2HoldingSwitch)
            {
                player2HoldingSwitch = false;
                messageManager.endP2WantsToSwitch(); // Hide P2's switch message
            }
        }

        return player1SwitchPressed && player2SwitchPressed;
    }

    private bool player1HoldingReconnect = false; 
    private bool player2HoldingReconnect = false; 
    private void CheckReconnectInput() {
        bool player1ReconnectPressed = player1Input.GetReconnectPressed();
        bool player2ReconnectPressed = player2Input.GetReconnectPressed();

        if (player1ReconnectPressed && player2ReconnectPressed && fixedJoint == null && canReconnect)
        {
            tryReconnect();
        }

        // Error messages
        if (player1ReconnectPressed && !player2ReconnectPressed && fixedJoint == null && canReconnect)
        {
            if (!player1HoldingReconnect)
            {
                player1HoldingReconnect = true;

                messageManager.endP2WantsToReconnect();
                messageManager.startP1WantsToReconnect();
                AudioManager.Instance.PlayUIMeowSFX();
            }
        }
        else
        {
            if (player1HoldingReconnect)
            {
                player1HoldingReconnect = false;
                messageManager.endP1WantsToReconnect(); 
            }
        }
        
        if (!player1Input.GetReconnectPressed() && player2Input.GetReconnectPressed() && fixedJoint == null && canReconnect)
        {
            if (!player2HoldingReconnect)
            {
                player2HoldingReconnect = true;
                messageManager.endP1WantsToReconnect();
                messageManager.startP2WantsToReconnect();
                AudioManager.Instance.PlayUIBarkSFX();
            }
        }
        else
        {
            if (player2HoldingReconnect)
            {
                player2HoldingReconnect = false;
                messageManager.endP2WantsToReconnect(); 
            }
        }
    }

    private void CheckSplitConditions(float player1YInput, float player2YInput)
    {
        // Determine if players are pulling in opposite directions based on their positions
        bool frontPullingForward = false;
        bool backPullingBackward = false;

        float inputThreshold = 0.5f;
        
        if (P1.IsFront)
        {
            frontPullingForward = player1YInput > inputThreshold;
            backPullingBackward = player2YInput < -inputThreshold;
        }
        else // P2 is front
        {
            frontPullingForward = player2YInput > inputThreshold;
            backPullingBackward = player1YInput < -inputThreshold;
        }

        splitCondition = frontPullingForward && backPullingBackward;
        
        // Start or reset split timer based on pull direction
        if (splitCondition)
        {
            if (!splitStopwatch.IsRunning && canSplit)
            {
                splitStopwatch.Start();
                player1Input.StartContinuousRumble(0.2f, 0.2f);
                player2Input.StartContinuousRumble(0.2f, 0.2f);

                AudioManager.Instance.PlayStretchSFX();
            }
        }
        else
        {
            if (splitStopwatch.IsRunning)
            {
                player1Input.StopRumble();
                player2Input.StopRumble();
                AudioManager.Instance.StopStretchSFX();
                splitStopwatch.Reset();
            }
        }
    }
    // PLAYER INPUT CHECKERS ////////////////////////////////////////////


    // MOVEMENT METHODS ////////////////////////////////////////////
    private void runMovementLogic()
    {
        // Get player 1's input
        Vector2 player1MoveInput = player1Input.GetMoveInput();
        float turnInputP1 = player1MoveInput.x * turnSpeed;
        float moveInputP1 = player1MoveInput.y * walkSpeed;
        
        // Get player 2's input
        Vector2 player2MoveInput = player2Input.GetMoveInput();
        float turnInputP2 = player2MoveInput.x * turnSpeed;
        float moveInputP2 = player2MoveInput.y * walkSpeed;
        
        // Apply the rotation depending on scheme
        float combinedTurn = 0f;
        float combinedMove = 0f;
        if (!altMovement) {
            combinedTurn = turnInputP1 + turnInputP2;
            combinedMove = moveInputP1 + moveInputP2;
        }
        else {
            combinedTurn = P1.IsFront ? turnInputP1 : turnInputP2;
            combinedMove = !P1.IsFront ? moveInputP1 : moveInputP2;
        }

        frontHalf.transform.Rotate(0.0f, combinedTurn, 0.0f, Space.Self);
        backHalf.transform.Rotate(0.0f, combinedTurn, 0.0f, Space.Self);

        // Reset Camera when movement is detected 
        if (Math.Abs(combinedMove) > 0 || Math.Abs(combinedTurn) > 0)
        {
            ResetCamera(mainCamera);
        }

        Rigidbody frontRb = frontHalf.GetComponent<Rigidbody>();
        Rigidbody backRb = backHalf.GetComponent<Rigidbody>();

        if (!splitCondition) {
            Vector3 moveVector = frontHalf.transform.forward * (combinedMove * Time.deltaTime);
            frontRb.MovePosition(frontRb.position + moveVector);
            backRb.MovePosition(backRb.position + moveVector);
        }
        

        // Update split condition checking for pulling opposite directions
        CheckSplitConditions(player1MoveInput.y, player2MoveInput.y);

        // Update rig movement directionality
        RiggingMovement[] frontRigs = frontHalf.GetComponentsInChildren<RiggingMovement>();
        RiggingMovement[] backRigs = backHalf.GetComponentsInChildren<RiggingMovement>();
        RiggingMovement[] allRigs = new RiggingMovement[frontRigs.Length + backRigs.Length];
        frontRigs.CopyTo(allRigs, 0);
        backRigs.CopyTo(allRigs, frontRigs.Length);

        foreach (RiggingMovement rigging in allRigs)
        {
            rigging.changeTargetDirection(combinedMove);
        }
    }

    private void runClimbMovementLogic()
    {
        // Get player 1's input
        Vector2 player1MoveInput = player1Input.GetMoveInput();
        float horizontalInputP1 = player1MoveInput.x * 0.1f;
        float moveInputP1 = player1MoveInput.y * 0.1f;
        
        // Get player 2's input
        Vector2 player2MoveInput = player2Input.GetMoveInput();
        float horizontalInputP2 = player2MoveInput.x * 0.1f;
        float moveInputP2 = player2MoveInput.y * 0.1f;
        
        // Apply the combined rotation to both halves:
        // float combinedTurn = horizontalInputP1 + horizontalInputP2;
        // frontHalf.transform.Rotate(0.0f, combinedTurn, 0.0f, Space.Self);
        // backHalf.transform.Rotate(0.0f, combinedTurn, 0.0f, Space.Self);
        
        // Apply the combined translation (forward/back) to both halves:
        float combinedHorizontalMove = horizontalInputP1 + horizontalInputP2;
        float combinedMove = moveInputP1 + moveInputP2;
        // frontHalf.transform.Translate(Vector3.forward * combinedMove * Time.deltaTime, Space.Self);
        // backHalf.transform.Translate(Vector3.forward * combinedMove * Time.deltaTime, Space.Self);

        // Reset Camera when movement is detected 
        if (Math.Abs(combinedMove) > 0 || Math.Abs(combinedHorizontalMove) > 0)
        {
            ResetCamera(mainCamera);
        }

        Rigidbody frontRb = frontHalf.GetComponent<Rigidbody>();
        Rigidbody backRb = backHalf.GetComponent<Rigidbody>();

        if (combinedMove == 0)
        {
            isMoving = false;
        } else{
            isMoving = true;
        }

        Vector3 moveVector = frontHalf.transform.up * (combinedMove * Time.deltaTime);
        frontRb.MovePosition(frontRb.position + moveVector);
        backRb.MovePosition(backRb.position + moveVector);
        
        
        Vector3 moveHorizontalVector = frontHalf.transform.right* (combinedHorizontalMove * Time.deltaTime);
        frontRb.MovePosition(frontRb.position + moveHorizontalVector);
        backRb.MovePosition(backRb.position + moveHorizontalVector);

        // Update split condition checking for pulling opposite directions
        CheckSplitConditions(player1MoveInput.y, player2MoveInput.y);
    }

    private void runSeparatedMovementLogic()
    {
        // Get player inputs
        Vector2 player1MoveInput = player1Input.GetMoveInput();
        Vector2 player2MoveInput = player2Input.GetMoveInput();

        Rigidbody rb1 = P1.Half.GetComponent<Rigidbody>();
        Rigidbody rb2 = P2.Half.GetComponent<Rigidbody>();

        float turnInputP1 = player1MoveInput.x * turnSpeed;
        float moveInputP1 = player1MoveInput.y * walkSpeed;
        float turnInputP2 = player2MoveInput.x * turnSpeed;
        float moveInputP2 = player2MoveInput.y * walkSpeed;
        
        // Movement for Player 1's half
        if (P1.Species == "dog") {
            rb1.MovePosition(rb1.position + P1.Half.transform.forward * player1MoveInput.y * walkSpeed * Time.deltaTime);
            P2.Half.transform.Rotate(0.0f, player1MoveInput.x * turnSpeed, 0.0f, Space.Self);
        }
        else {
            rb1.MovePosition(rb1.position + P1.Half.transform.forward * player1MoveInput.y * immutableWalkSpeed * Time.deltaTime);
            P1.Half.transform.Rotate(0.0f, player1MoveInput.x * immutableTurnSpeed, 0.0f, Space.Self);   
        }
        
        // Reset Camera when movement is detected 
        if (Math.Abs(moveInputP1) > 0 || Math.Abs(turnInputP1) > 0)
        {
            ResetCamera(player1Camera);
        }
        
        // Movement for Player 2's half
        
        if (P2.Species == "dog") {
            rb2.MovePosition(rb2.position + P2.Half.transform.forward * player2MoveInput.y * walkSpeed * Time.deltaTime);
            P2.Half.transform.Rotate(0.0f, player2MoveInput.x * turnSpeed, 0.0f, Space.Self);
        }
        else {
            rb2.MovePosition(rb2.position + P2.Half.transform.forward * player2MoveInput.y * immutableWalkSpeed * Time.deltaTime);
            P2.Half.transform.Rotate(0.0f, player2MoveInput.x * immutableTurnSpeed, 0.0f, Space.Self);
        }

        // Reset Camera when movement is detected 
        if (Math.Abs(moveInputP2) > 0 || Math.Abs(turnInputP2) > 0)
        {
            ResetCamera(player2Camera);
        }
        
    }
    // MOVEMENT METHODS ////////////////////////////////////////////
    

    // SPLITTING METHODS ////////////////////////////////////////////
    public void DestroyJoint()
    {
        Destroy(fixedJoint); // Split the halves
        fixedJoint = null;

        ResetCamera(mainCamera);

        // Add rotation constraints when split
        frontHalf.GetComponent<Rigidbody>().constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        backHalf.GetComponent<Rigidbody>().constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        messageManager.cameraIndicatorMessage(); // Will have split camera, so temporarily display the "P1" / "P2" labels
            
        isSplit = true;
    }
    
    private void runSplitLogic() 
    {
        // If the split timer exceeds the threshold and the halves are still connected, split them.
        if (splitStopwatch.IsRunning && splitStopwatch.Elapsed.TotalSeconds > splitTime && fixedJoint != null && canSplit)
        {
            splitStopwatch.Reset();

            player1Input.StopRumble();
            player2Input.StopRumble();

            DestroyJoint();
        
            AudioManager.Instance.PlaySplitSFX();
            
            UnityEngine.Debug.Log("Halves disconnected due to opposing pull.");
        }
    }

    private void tryReconnect()
    {
        float distance = Vector3.Distance(frontMagnet.transform.position, backMagnet.transform.position);
        if (distance < reconnectionDistance)
        {
            UnityEngine.Debug.Log("Trying to reconnect.");
            alignHalves();
            setJoint();
            ResetCamera(player1Camera);
            ResetCamera(player2Camera);


            // TODO: Apply animation


            AudioManager.Instance.PlayReconnectSFX();
    

            UnityEngine.Debug.Log("Halves reconnected.");
            isSplit = false;
        
            // Reset the relative rotation
            initialRelativeRotation = Quaternion.Inverse(frontHalf.transform.rotation) * backHalf.transform.rotation;
        }
        else
        {
            messageManager.reconnectFailMessage();
        }
    }

    private void alignHalves()
    {
        Rigidbody bottomRb = backHalf.GetComponent<Rigidbody>();
        bool originalKinematic = bottomRb.isKinematic;
        bottomRb.isKinematic = true;

        // Align orientation and position
        backHalf.transform.rotation = frontHalf.transform.rotation * initialRelativeRotation;

        Vector3 positionOffset = frontMagnet.transform.position - backMagnet.transform.position;
        backHalf.transform.position += positionOffset;

        // Re-enable physics
        bottomRb.isKinematic = originalKinematic;
    }

    public bool shouldStretch()
    {
        return splitCondition;
    }
    // SPLITTING METHODS ////////////////////////////////////////////


    // SWITCHING METHODS ////////////////////////////////////////////
    // runSwitchLogic(), tryStartSwitch(), cancelSwitch(), tryFinishSwitch() correspond to front <-> back switching (NOT SWITCHING SPECIES!)
    private bool shrinkIsPlaying = false; // stop shrink sound from playing multiple times when holding down button for >1.5s
    private bool isSplit = false;
    private void runSwitchLogic()
    {
        if (CheckSwitchInput() && canSwitch) 
        { 
            player1HoldingSwitch = false;
            player2HoldingSwitch = false;
            messageManager.endP1WantsToSwitch();
            messageManager.endP2WantsToSwitch();

            tryStartSwitch();
        }
        else
        {
            messageManager.switchFailMessageDeactivate();
            cancelSwitch();

            hasJustSwitched = false;
        }

        tryFinishSwitch();
    }

    private void tryStartSwitch()
    {
        if (fixedJoint == null)
        {
            // not allowed; show text to players saying they must be together 
            messageManager.switchFailMessageActivate();
        }
        else
        {
            switchStopwatch.Start();
            if (!shrinkIsPlaying && !hasJustSwitched) {
                AudioManager.Instance.PlayShrinkSFX();
                shrinkIsPlaying = true;
            }
        }
    }

    private void cancelSwitch()
    {
        switchStopwatch.Reset();
        if (shrinkIsPlaying)
        {
            AudioManager.Instance.StopShrinkSFX();
            shrinkIsPlaying = false;
        }
    }

    private bool hasJustSwitched = false; // stop player from constantly switching when holding down button for >1.5s
    private void tryFinishSwitch()
    {
        if ((switchStopwatch.Elapsed.TotalSeconds > switchTime) && (fixedJoint != null) && !hasJustSwitched)
        {
            switchStopwatch.Reset();

            shrinkIsPlaying = false;

            // Switch which half the players are controlling
            P1.IsFront = !P1.IsFront;
            P2.IsFront = !P2.IsFront;

            Destroy(frontHalf.GetComponent<FixedJoint>());

            catFront.SetActive(false);
            catBack.SetActive(false);
            dogFront.SetActive(false);
            dogBack.SetActive(false);

            // Switch the half to the player's species
            if (P1.IsFront)
            {
                if (P1.Species == "cat")
                {
                    // make sure all halves have no parents before setting their position
                    // o/w it will use world space instead of local (relative to parent) space!!!
                    catBack.transform.SetParent(null);
                    dogFront.transform.SetParent(null);

                    catFront.transform.position = frontHalf.transform.position; 
                    catFront.transform.rotation = frontHalf.transform.rotation;
                    dogBack.transform.position = backHalf.transform.position;
                    dogBack.transform.rotation = backHalf.transform.rotation;

                    // set the correct halves as children under P1 and P2
                    catFront.transform.SetParent(transform.GetChild(0));
                    // catBack.transform.SetParent(null);
                    // dogFront.transform.SetParent(null);
                    dogBack.transform.SetParent(transform.GetChild(1));

                    // update players and variables
                    P1.Half = catFront;
                    P2.Half = dogBack;
                    P1.Magnet = catFront.transform.GetChild(2).gameObject;
                    P2.Magnet = dogBack.transform.GetChild(2).gameObject;
                }
                else
                {
                    // front half = dog = P1
                    // back half = cat = P2
                    catFront.transform.SetParent(null);
                    dogBack.transform.SetParent(null);

                    catBack.transform.position = backHalf.transform.position + transform.TransformDirection(Vector3.up * 0.215f);
                    catBack.transform.rotation = backHalf.transform.rotation;
                    dogFront.transform.position = frontHalf.transform.position + transform.TransformDirection(Vector3.up * 0.215f);
                    dogFront.transform.rotation = frontHalf.transform.rotation;

                    // catFront.transform.SetParent(null);
                    catBack.transform.SetParent(transform.GetChild(1));
                    dogFront.transform.SetParent(transform.GetChild(0));
                    // dogBack.transform.SetParent(null);
                    
                    P1.Half = dogFront;
                    P2.Half = catBack;
                    P1.Magnet = dogFront.transform.GetChild(2).gameObject;
                    P2.Magnet = catBack.transform.GetChild(2).gameObject;
                }
            }
            else // if P2.IsFront
            {
                if (P2.Species == "cat")
                {
                    // front half = cat = P2
                    // back half = dog = P1
                    catBack.transform.SetParent(null);
                    dogFront.transform.SetParent(null);

                    catFront.transform.position = frontHalf.transform.position;
                    catFront.transform.rotation = frontHalf.transform.rotation;
                    dogBack.transform.position = backHalf.transform.position;
                    dogBack.transform.rotation = backHalf.transform.rotation;

                    catFront.transform.SetParent(transform.GetChild(1));
                    // catBack.transform.SetParent(null);
                    // dogFront.transform.SetParent(null);
                    dogBack.transform.SetParent(transform.GetChild(0));

                    P2.Half = catFront;
                    P1.Half = dogBack;
                    P2.Magnet = catFront.transform.GetChild(2).gameObject;
                    P1.Magnet = dogBack.transform.GetChild(2).gameObject;
                }
                else
                {
                    // front half = dog = P2
                    // back half = cat = P1
                    catFront.transform.SetParent(null);
                    dogBack.transform.SetParent(null);

                    catBack.transform.position = backHalf.transform.position + transform.TransformDirection(Vector3.up * 0.215f);
                    catBack.transform.rotation = backHalf.transform.rotation;
                    dogFront.transform.position = frontHalf.transform.position + transform.TransformDirection(Vector3.up * 0.215f);
                    dogFront.transform.rotation = frontHalf.transform.rotation;
                    
                    // catFront.transform.SetParent(null);
                    catBack.transform.SetParent(transform.GetChild(0));
                    dogFront.transform.SetParent(transform.GetChild(1));
                    // dogBack.transform.SetParent(null);

                    P2.Half = dogFront;
                    P1.Half = catBack;
                    P2.Magnet = dogFront.transform.GetChild(2).gameObject;
                    P1.Magnet = catBack.transform.GetChild(2).gameObject;
                }
            }
           
            player1Camera.Follow = P1.Half.transform;
            player1Camera.LookAt = P1.Half.transform;
            player2Camera.Follow = P2.Half.transform;
            player2Camera.LookAt = P2.Half.transform;

            refreshHalves();
            
            P1.Half.SetActive(true);
            P2.Half.SetActive(true);

            alignHalves();

            if (getJoint() == null)
            {
                setJoint();
            }
            
            // updatePlayerIcons();

            AudioManager.Instance.PlaySwitchSFX();

            hasJustSwitched = true;
        }
    }

    private void tryFinishSwitchV2() 
    {
        if ((switchStopwatch.Elapsed.TotalSeconds > switchTime) && (fixedJoint != null))
        {
            switchStopwatch.Reset();

            // Switch which half the players are controlling
            P1.IsFront = !P1.IsFront;
            P2.IsFront = !P2.IsFront;

            string P1PrevSpecies = P1.Species;
            P1.Species = P2.Species;
            P2.Species = P1PrevSpecies;

            if (P1.IsFront)
            {
                P1.Half = frontHalf;
                P2.Half = backHalf;
                P1.Magnet = frontHalf.transform.GetChild(2).gameObject;
                P2.Magnet = backHalf.transform.GetChild(2).gameObject;
            }
            else
            {
                P1.Half = backHalf;
                P2.Half = frontHalf;
                P1.Magnet = backHalf.transform.GetChild(2).gameObject;
                P2.Magnet = frontHalf.transform.GetChild(2).gameObject;
            }

            player1Camera.Follow = P1.Half.transform;
            player1Camera.LookAt = P1.Half.transform;
            player2Camera.Follow = P2.Half.transform;
            player2Camera.LookAt = P2.Half.transform;

            refreshHalves();
            // updatePlayerIcons();

            if (getJoint() == null)
            {
                setJoint();
            }

            UnityEngine.Debug.Log("Switched!");
        }
    }

    public void TransferControl(GameObject dockedHalf, GameObject counterpart)
    {
        // First determine which player owns this half
        bool isPlayer1Docked = false;
        
        // Check under Player1's children
        foreach (Transform child in transform.GetChild(0))
        {
            if (child.gameObject == dockedHalf)
            {
                isPlayer1Docked = true;
                break;
            }
        }

        Player controllingPlayer = isPlayer1Docked ? P1 : P2;
        Player otherPlayer = isPlayer1Docked ? P2 : P1;

        // Update the controlling player's half reference
        controllingPlayer.Half = counterpart;
        
        // Update magnet reference - make sure magnet exists
        GameObject petMagnet = null;

        foreach (Transform child in counterpart.GetComponentsInChildren<Transform>())
        {
            if (child.CompareTag("PetMagnet"))
            {
                petMagnet = child.gameObject;
                break;
            }
        }

        if (petMagnet != null)
        {
            controllingPlayer.Magnet = petMagnet;
        }
        else
        {
            UnityEngine.Debug.LogError($"No magnet found on {counterpart.name}");
        }

        // Update front/back status if needed
        bool wasControllingFront = dockedHalf.name.Contains("Front");
        bool willControlFront = counterpart.name.Contains("Front");
        
        if (wasControllingFront != willControlFront)
        {
            controllingPlayer.IsFront = willControlFront;
            otherPlayer.IsFront = !willControlFront;
        }

        // Make sure the Rigidbody is not kinematic after transfer
        Rigidbody rb = counterpart.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        bool isDocked = dockedHalf.GetComponent<Rigidbody>().isKinematic;
        if (isDocked && counterpart != null) {
            if (controllingPlayer.PlayerNumber == 1) {
                player1Camera.Follow = counterpart.transform;
                player1Camera.LookAt = counterpart.transform;
            } else {
                player2Camera.Follow = counterpart.transform;
                player2Camera.LookAt = counterpart.transform;
            }
        }

        refreshHalves();
        // updatePlayerIcons();
    }

    public void refreshHalves()
    {
        frontHalf = getFrontHalf();
        backHalf = getBackHalf();
        frontMagnet = getFrontMagnet();
        backMagnet = getBackMagnet();

        cameraMovement.frontHalf = frontHalf.transform;
        cameraMovement.backHalf = backHalf.transform;
        mainCamera.Follow = frontHalf.transform;
        mainCamera.LookAt = frontHalf.transform;

        ControlsCornerUI.Instance.StartBounce(); // Make the controls corner bounce a few times to catch players' attention

        PlayerActions playerActions = frontHalf.GetComponent<PlayerActions>();
        if (playerActions != null)
        {
            playerActions.RefreshAfterSwitch(this);
        }
    }
    // SWITCHING METHODS ////////////////////////////////////////////

    // CAMERA MOVEMENT METHODS ////////////////////////////////////////////
    private void runCameraLogic()
    {

        if (fixedJoint != null)
        {
            UpdateMainCamera();
        } else
        {
            UpdatePlayerCameras();
        }

    }

    private void UpdateMainCamera()
    {
        Vector2 player1CameraInput = player1Input.GetCameraInput();
        Vector2 player2CameraInput = player2Input.GetCameraInput();

        if (mainCamera != null)
        {
            RotateCamera(mainCamera, player1CameraInput + player2CameraInput);
        }
    }

    private void UpdatePlayerCameras()
    {
        Vector2 player1CameraInput = player1Input.GetCameraInput();
        Vector2 player2CameraInput = player2Input.GetCameraInput();

        if (player1Camera != null)
        {
            RotateCamera(player1Camera, player1CameraInput);
        }
        if (player2Camera != null)
        {
            RotateCamera(player2Camera, player2CameraInput);
        }
    }

    private void RotateCamera(CinemachineCamera camera, Vector2 playerInput)
    {
        var composer = camera.GetComponent<CinemachineRotationComposer>();

        if (composer != null)
        {
            composer.Composition.ScreenPosition.x = Mathf.Clamp(composer.Composition.ScreenPosition.x - playerInput.x, -0.3f, 0.3f);
            composer.Composition.ScreenPosition.y = Mathf.Clamp(composer.Composition.ScreenPosition.y + playerInput.y, 0, 0.5f);

        }
        else
        {
            UnityEngine.Debug.LogError("Missing Cinemachine Rotation Composer");
        }
    }

    private void ResetCamera(CinemachineCamera camera)
    {
        var composer = camera.GetComponent<CinemachineRotationComposer>();

        if (composer != null)
        {
            composer.Composition.ScreenPosition = new Vector2(0f, 0.24f);
        }
    }
    // CAMERA MOVEMENT METHODS ////////////////////////////////////////////


    // COLLISION METHODS ////////////////////////////////////////////
    void EnsureUpright() 
    {
        if (fixedJoint != null) {  // Only enforce when connected
            // Lock rotation around X and Z axes while preserving Y rotation
            Quaternion frontRotation = frontHalf.transform.rotation;
            Quaternion backRotation = backHalf.transform.rotation;
            
            // Keep only the Y rotation
            Vector3 frontEuler = frontRotation.eulerAngles;
            Vector3 backEuler = backRotation.eulerAngles;
            
            frontHalf.transform.rotation = Quaternion.Euler(0, frontEuler.y, 0);
            backHalf.transform.rotation = Quaternion.Euler(0, backEuler.y, 0);
        }
    }
    // COLLISION METHODS ////////////////////////////////////////////


    // COROUTINES //////////////////////////////////////
    private IEnumerator waitForSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }

}
