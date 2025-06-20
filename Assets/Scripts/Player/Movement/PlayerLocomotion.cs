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
    public float rayCastHeightOffset = 1f;
    public float maxDistance = 0.5f;
    public LayerMask terrainLayer;

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
    public float jumpHeight = 3;
    public float gravityIntensity = 15f;
    private Vector3 airDirection;
    private float airSpeed;
    public float airRotationLimit = 55f; // Degrees left and right that the player can rotate in midair


    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        inputManager = GetComponent<InputManager>();
        animatorManager = GetComponent<AnimatorManager>();
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
        // Build the raw input look direction
        Vector3 targetDirection = Vector3.zero;

        targetDirection = cameraObject.forward * inputManager.verticalInput;
        targetDirection = targetDirection + cameraObject.right * inputManager.horizontalInput;
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            targetDirection.Normalize();
        }
        else
        {
            targetDirection = transform.forward;
        }

        // If in the air, clamp around initial jump direction
        if (!isGrounded)
        {
            // Only clamp if there IS input
            if (inputManager.horizontalInput != 0f || inputManager.verticalInput != 0f)
            {
                // Find angle from jump-time direction
                float angle = Vector3.SignedAngle(airDirection, targetDirection, Vector3.up);
                float clampedAngle = Mathf.Clamp(angle, -airRotationLimit, airRotationLimit);

                // Rotate in the stored air direction, by the clamped amount
                Quaternion off = Quaternion.AngleAxis(clampedAngle, Vector3.up);
                targetDirection = off * airDirection;
            }
            else
            {
                // No input => face the current direction
                targetDirection = transform.forward;
            }
        }

        // Slerp into final target direction
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void HandleFallingAndLanding()
    {
        // 1) Cast from chest height
        Vector3 raycastOrigin = transform.position + Vector3.up * rayCastHeightOffset;
        RaycastHit hit;

        // 2) Detect ground with SphereCast and vertical velocity
        bool hittingGround = Physics.Raycast(
            raycastOrigin,
            Vector3.down,
            out hit,
            maxDistance,
            terrainLayer
        );
        isGrounded = hittingGround;

        // 3) Grab our current vertical velocity
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