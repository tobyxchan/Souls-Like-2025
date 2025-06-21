using System.Security;
using UnityEngine;

public class PlayerLocomotion : MonoBehaviour
{
    PlayerManager playerManager;
    InputManager inputManager;
    AnimatorManager animatorManager;

    Vector3 moveDirection;
    Transform cameraObject;
    Rigidbody playerRigidbody;

    [Header("Falling")]
    private bool isAirborne;
    public float leapingVelocity;

    [Header("Ground Check")]
    private CapsuleCollider capsuleCollider;
    public LayerMask terrainLayer;
    public float groundCheckDistance = 0.1f;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isGrounded;
    public bool isJumping;

    [Header("Movement Settings")]
    public float walkingSpeed = 2;              // Speed of player when slightly tilting the stick
    public float runningSpeed = 5;              // Speed of player when fully tiling the stick
    public float sprintingSpeed = 8;            // Speed of player when holding sprint input
    public float rotationSpeed = 15;            // How fast the player turns towards input direction

    [Header("Jump Settings")]
    private Vector3 airDirection;
    private float airSpeed;
    private float lostGroundTimer = 0f;
    public float jumpHeight = 3;                // Controls how much the player's jump pushes them upwards
    public float gravityIntensity = 30f;        // Controls how fast the player falls when in the air
    public float airRotationLimit = 35f;        // Degrees left and right that the player can rotate in midair
    public float airControlDuration = 1.5f;     // How many seconds in the air before the player can rotate fully
    public float groundLostThreshold = 0.01f;   // How long the raycast can miss before we become airborne
    private float airTimer = 0f;                // How long the player has been in the air for
    

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        inputManager = GetComponent<InputManager>();
        animatorManager = GetComponent<AnimatorManager>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.useGravity = false;
        cameraObject = Camera.main.transform;
    }


    public void HandleAllMovement()
    {
        HandleFallingAndLanding();

        // Only block movement if interacting with something on the ground
        if (playerManager.isInteracting && isGrounded) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        moveDirection = new Vector3(cameraObject.forward.x, 0f, cameraObject.forward.z) * inputManager.verticalInput;
        moveDirection = moveDirection + cameraObject.right * inputManager.horizontalInput;
        moveDirection.Normalize();
        moveDirection.y = 0;

        if (isSprinting)
        {
            moveDirection = moveDirection * sprintingSpeed;
        }
        else
        {
            if (inputManager.moveAmount >= 0.5f)
            {
                moveDirection = moveDirection * runningSpeed;
            }
            else
            {
                moveDirection = moveDirection * walkingSpeed;
            }
        }

        Vector3 movementVelocity = moveDirection;

        if (isAirborne)
        {
            // Lock air speed to ground speed
            Vector3 horizontal = airDirection * airSpeed;
            movementVelocity.x = horizontal.x;
            movementVelocity.z = horizontal.z;

            // Keep the vertical velocity
            movementVelocity.y = playerRigidbody.velocity.y;
        }

        playerRigidbody.velocity = movementVelocity;
    }

    private void HandleRotation()
    {
    // 1) Build the raw input‐based look direction
    Vector3 targetDirection = cameraObject.forward * inputManager.verticalInput + cameraObject.right   * inputManager.horizontalInput;
    targetDirection.y = 0f;

    if (targetDirection.sqrMagnitude > 0.001f)
        targetDirection.Normalize();
    else
        // if no input, just keep facing whichever way you're already facing
        targetDirection = transform.forward;

    // 2) While in the air *and* still within the limited‐control window...
    if (!isGrounded && airTimer < airControlDuration)
    {
        // only clamp if the player is actually trying to turn
        if (inputManager.horizontalInput != 0f || inputManager.verticalInput != 0f)
        {
            // signed angle between your locked jump direction and the desired look
            float angle   = Vector3.SignedAngle(airDirection, targetDirection, Vector3.up);
            float clamped = Mathf.Clamp(angle, -airRotationLimit, airRotationLimit);

            // rotate off your stored airDirection by that clamped amount
            targetDirection = Quaternion.AngleAxis(clamped, Vector3.up) * airDirection;
        }
        else
        {
            // no input → keep whatever forward you're already at
            targetDirection = transform.forward;
        }
    }
    // else: either you're grounded or your airTime >= threshold,
    // so targetDirection remains exactly your camera/input look.

    // 3) Smoothly rotate toward that final direction
    Quaternion targetRot = Quaternion.LookRotation(targetDirection);
    transform.rotation = Quaternion.Slerp(
        transform.rotation,
        targetRot,
        rotationSpeed * Time.deltaTime
    );
    }

    private void HandleFallingAndLanding()
    {
        // Raycast straight down from the capsule’s center
        Vector3 origin       = capsuleCollider.bounds.center;
        float   halfHeight   = capsuleCollider.bounds.extents.y;
        float   castDistance = halfHeight + groundCheckDistance;

        RaycastHit hitInfo;
        bool rawHit = Physics.Raycast(
            origin,
            Vector3.down,
            out hitInfo,
            castDistance,
            terrainLayer
        );

        // Hysteresis so small lips don’t flicker you on/off the ground
        if (rawHit) lostGroundTimer = 0f;
        else        lostGroundTimer += Time.deltaTime;

        bool groundedThisFrame = (lostGroundTimer <= groundLostThreshold);
        isGrounded             = groundedThisFrame;

        // grab vertical velocity so we only start “falling” on the way down
        float verticalVelocity = playerRigidbody.velocity.y;

        // ----- LANDING (once) -----
        if (rawHit && isAirborne && verticalVelocity <= 0f)
        {
            animatorManager.PlayTargetAnimation("Land", true);
            animatorManager.animator.SetBool("isJumping", false);
            isJumping = false;
            playerManager.isInteracting = false;
            isAirborne                 = false;

            // kill horizontal drift
            Vector3 v = playerRigidbody.velocity;
            playerRigidbody.velocity = new Vector3(0f, v.y, 0f);
        }
        // ----- FALLING (once, on descent) -----
        else if (!groundedThisFrame 
                && verticalVelocity < 0f 
                && !isAirborne)
        {
            isAirborne = true;

            // if we just *jumped*, skip over capture for that case
            if (!animatorManager.animator.GetBool("isJumping"))
            {
                // otherwise we’re *dropping* off an edge:
                if (moveDirection.sqrMagnitude > 0.0001f)
                {
                    // carry your actual input‐based speed
                    airDirection = moveDirection.normalized;
                    airSpeed     = moveDirection.magnitude;
                }
                else
                {
                    // stationary jump or drop → no horizontal movement
                    airDirection = Vector3.zero;
                    airSpeed     = 0f;
                }
            }

            animatorManager.PlayTargetAnimation("Falling", true);
        }

        // custom gravity whenever off the ground
        if (!isGrounded)
        {
            playerRigidbody.AddForce(
                Vector3.down * gravityIntensity,
                ForceMode.Acceleration
            );
        }
    }

    public void HandleJumping()
    {
        if (isGrounded)
        {
            // Capture movement speed before jumping
            airDirection = moveDirection.normalized;
            airSpeed = moveDirection.magnitude;
            isAirborne = true;

            animatorManager.PlayTargetAnimation("Jump", true);
            animatorManager.animator.SetBool("isJumping", true);
            isJumping = true;

            float jumpingVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravityIntensity) * jumpHeight);

            Vector3 playerVelocity = moveDirection;
            playerVelocity.y = jumpingVelocity;
            playerRigidbody.velocity = playerVelocity;
        }
    }
}