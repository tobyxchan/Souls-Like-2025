using System.Collections;
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

    [Header("Ground Check")]
    private CapsuleCollider capsuleCollider;
    public LayerMask terrainLayer;
    public float groundCheckDistance = 0.1f;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isGrounded;
    public bool isJumping;

    [Header("Movement Settings")]
    public float walkingSpeed = 2;    // Speed of player when slightly tilting the stick
    public float runningSpeed = 5;    // Speed of player when fully tiling the stick
    public float sprintingSpeed = 8;    // Speed of player when holding sprint input
    public float rotationSpeed = 15;   // How fast the player turns towards input direction

    [Header("Jump Settings")]
    private Vector3 airDirection;
    private float airSpeed;
    public float jumpHeight = 1.5f;             // Controls how much the player's jump pushes them upwards
    public float gravityIntensity = 30f;        // Controls how fast the player falls when in the air
    public float airRotationLimit = 35f;        // Degrees left and right that the player can rotate in midair
    public float airControlDuration = 1.5f;     // How many seconds in the air before the player can rotate fully
    public float groundLostThreshold = 0.01f;   // How long the raycast can miss before we become airborne
    private float lostGroundTimer = 0f;         // How long since the player has touched the ground
    public int jumpWindupFrames = 5;            // How many frames of wind-up the jump animation has
    public int landingLockFrames = 3;           // How many frames after landing before the player can rotate again
    private bool jumpRequested = false;


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

        // Lock movement for a few frames after landing
        if (isAirborne || landingLockFrames > 0)
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
        // While airborne, lock facing to the jump/drop direction
        if (isAirborne || landingLockFrames > 0)
        {
            // If we somehow had zero airDirection (e.g. stationary jump), fall back to current forward
            Vector3 lockedDir = airDirection.sqrMagnitude > 0.001f ? airDirection : transform.forward;
            Quaternion targetRotation = Quaternion.LookRotation(lockedDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            // Consume one frame of landing lock
            if (landingLockFrames > 0) landingLockFrames--;
            return;
        }

        // Otherwise (grounded), do your normal camera-based rotation:
        Vector3 targetDirection = cameraObject.forward * inputManager.verticalInput + cameraObject.right * inputManager.horizontalInput;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            targetDirection.Normalize();
        }
        else
        {
            targetDirection = transform.forward;
        }

        Quaternion finalRot = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            finalRot,
            rotationSpeed * Time.deltaTime
        );
    }

    private void HandleFallingAndLanding()
    {
        // Raycast straight down from the capsule’s center
        Vector3 origin = capsuleCollider.bounds.center;
        float halfHeight = capsuleCollider.bounds.extents.y;
        float castDistance = halfHeight + groundCheckDistance;

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
        else lostGroundTimer += Time.deltaTime;

        bool groundedThisFrame = (lostGroundTimer <= groundLostThreshold);
        isGrounded = groundedThisFrame;

        // grab vertical velocity so we only start “falling” on the way down
        float verticalVelocity = playerRigidbody.velocity.y;

        // ----- LANDING (once) -----
        if (rawHit && isAirborne && verticalVelocity <= 0f)
        {
            animatorManager.PlayTargetAnimation("Land", true);
            animatorManager.animator.SetBool("isJumping", false);
            isJumping = false;
            playerManager.isInteracting = false;
            isAirborne = false;
            landingLockFrames = 3;

            // kill horizontal drift
            Vector3 v = playerRigidbody.velocity;
            playerRigidbody.velocity = new Vector3(0f, v.y, 0f);
        }
        // ----- FALLING (once, on descent) -----
        else if (!groundedThisFrame && verticalVelocity < 0f)
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
                    airSpeed = moveDirection.magnitude;
                }
                else
                {
                    // stationary jump or drop → no horizontal movement
                    airDirection = Vector3.zero;
                    airSpeed = 0f;
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
        // Start our jump only if grounded and not already queued
        if (isGrounded && !jumpRequested)
        {
            jumpRequested = true;

            // Play jumping animation
            animatorManager.PlayTargetAnimation("Jump", true);
            animatorManager.animator.SetBool("isJumping", true);
            isJumping = true;

            // Begin physics of jump after slight delay
            StartCoroutine(PerformJumpAfterWindup());
        }
    }

    private IEnumerator PerformJumpAfterWindup()
    {
        // Wait for N frames so our animation's windup plays
        for (int i = 0; i < jumpWindupFrames; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        // Capture movement speed
        airDirection = moveDirection.normalized;
        airSpeed = moveDirection.magnitude;

        // Move player upwards
        isAirborne = true;
        float jumpingVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravityIntensity) * jumpHeight);

        Vector3 playerVelocity = moveDirection;
        playerVelocity.y = jumpingVelocity;
        playerRigidbody.velocity = playerVelocity;

        // Allow next jump to be queued
        jumpRequested = false;
    }
}