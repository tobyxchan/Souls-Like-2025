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
    public float groundCheckDistance = 0.75f;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isGrounded;
    public bool isJumping;

    [Header("Movement Settings")]
    public float walkingSpeed = 2;
    public float runningSpeed = 5;
    public float sprintingSpeed = 8;
    public float rotationSpeed = 15;

    [Header("Jump Settings")]
    public float jumpHeight = 3;            // Controls how much the player's jump pushes them upwards
    public float gravityIntensity = 30f;    // Controls how fast the player falls when in the air
    public float airRotationLimit = 35f;    // Degrees left and right that the player can rotate in midair
    public float airControlDuration = 2f;   // How many seconds in the air before the player can rotate fully
    private float airTimer = 0f;            // How long the player has been in the air for
    private Vector3 airDirection;
    private float airSpeed;


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

        if (!isGrounded)
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
        // 1) compute raw input-based direction
        Vector3 targetDirection = cameraObject.forward * inputManager.verticalInput
                                + cameraObject.right * inputManager.horizontalInput;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude > 0.001f)
            targetDirection.Normalize();
        else
            targetDirection = transform.forward;

        // 2) if we're in the air AND still within the “limited control” window…
        if (!isGrounded && airTimer < airControlDuration)
        {
            // only clamp when there's actual input
            if (inputManager.horizontalInput != 0f || inputManager.verticalInput != 0f)
            {
                float angle = Vector3.SignedAngle(airDirection, targetDirection, Vector3.up);
                float clamped = Mathf.Clamp(angle, -airRotationLimit, airRotationLimit);
                targetDirection = Quaternion.AngleAxis(clamped, Vector3.up) * airDirection;
            }
            else
            {
                // no input → keep facing current forward
                targetDirection = transform.forward;
            }
        }
        // else: either we're grounded or airTime ≥ threshold,
        // so targetDirection stays as the full-input look direction

        // 3) slerp into the chosen direction
        Quaternion targetRot = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    private void HandleFallingAndLanding()
    {
        // 1) Use collider's world-space center
        Vector3 origin = capsuleCollider.bounds.center;
        float sphereRadius = capsuleCollider.radius;

        // 2) Calculate how far the center must move for the sphere to touch the floor
        float halfHeight = capsuleCollider.bounds.extents.y;
        float castDistance = (halfHeight - sphereRadius) + groundCheckDistance;

        // 3) Sweep the sphere down the desired distance
        RaycastHit hit;
        bool hittingGround = Physics.SphereCast(
            origin,
            sphereRadius,
            Vector3.down,
            out hit,
            castDistance,
            terrainLayer
        );
        
        isGrounded = hittingGround;

        // 4) Track how long we've been off the ground
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;
        }
        else
        {
            airTimer = 0f;
        }

        // 5) Grab our current vertical velocity
        float verticalVelocity = playerRigidbody.velocity.y;

        // ---LANDING--- (only once)
        if (hittingGround && isAirborne)
        {
            animatorManager.PlayTargetAnimation("Land", true);
            animatorManager.animator.SetBool("isJumping", false);
            playerManager.isInteracting = false;
            isAirborne = false;

            // kill horizontal drift
            Vector3 vel = playerRigidbody.velocity;
            playerRigidbody.velocity = new Vector3(0f, vel.y, 0f);
        }
        // ---FALLING--- (only on descent)
        else if (!hittingGround && verticalVelocity < 0f && !isAirborne)
        {
            isAirborne = true;
            animatorManager.PlayTargetAnimation("Falling", true);
        }

        // Apply custom gravity whenever off the ground
        if (!isGrounded)
        {
            playerRigidbody.AddForce(Vector3.down * gravityIntensity, ForceMode.Acceleration);
        }
    }

    public void HandleJumping()
    {
        if (isGrounded)
        {
            // Capture movement speed before jumping
            airDirection = moveDirection.normalized;
            airSpeed = moveDirection.magnitude;

            animatorManager.PlayTargetAnimation("Jump", true);
            animatorManager.animator.SetBool("isJumping", true);

            float jumpingVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravityIntensity) * jumpHeight);

            Vector3 playerVelocity = moveDirection;
            playerVelocity.y = jumpingVelocity;
            playerRigidbody.velocity = playerVelocity;
        }
    }
}