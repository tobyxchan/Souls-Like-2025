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
    public float inAirTimer;
    public float leapingVelocity;
    public float fallingVelocity;

    [Header("Ground Check")]
    public float rayCastHeightOffset = 1f;
    public float maxDistance = 0.5f;
    public LayerMask terrainLayer;

    [Header("Movement Flags")]
    public bool isSprinting;
    public bool isGrounded;

    [Header("Movement Settings")]
    public float walkingSpeed = 2;
    public float runningSpeed = 5;
    public float sprintingSpeed = 8;
    public float rotationSpeed = 15;


    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
        inputManager = GetComponent<InputManager>();
        animatorManager = GetComponent<AnimatorManager>();
        playerRigidbody = GetComponent<Rigidbody>();
        cameraObject = Camera.main.transform;
    }


    public void HandleAllMovement()
    {
        HandleFallingAndLanding();

        // Change this later to allow air control
        if (playerManager.isInteracting) return;

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

        // Check walking, running or sprinting


        Vector3 movementVelocity = moveDirection;
        playerRigidbody.velocity = movementVelocity;
    }

    private void HandleRotation()
    {
        Vector3 targetDirection = Vector3.zero;

        targetDirection = cameraObject.forward * inputManager.verticalInput;
        targetDirection = targetDirection + cameraObject.right * inputManager.horizontalInput;
        targetDirection.Normalize();
        targetDirection.y = 0;

        if (targetDirection == Vector3.zero)
        {
            targetDirection = transform.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        Quaternion playerRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        transform.rotation = playerRotation;
    }

    private void HandleFallingAndLanding()
    {
        // 1. Cast from chest height
        Vector3 raycastOrigin = transform.position + Vector3.up * rayCastHeightOffset;
        RaycastHit hit;

        // 2. Detect ground with SphereCast
        bool hittingGround = Physics.SphereCast(
            raycastOrigin,
            0.5f,
            Vector3.down,
            out hit,
            maxDistance,
            terrainLayer
        );

        // 3. If not touching ground, must be airborne
        if (!hittingGround)
        {
            // Just stepped off?
            if (!isAirborne)
            {
                isAirborne = true;
                animatorManager.PlayTargetAnimation("Falling", true);

                // One-time force behind player
                playerRigidbody.AddForce(transform.forward * leapingVelocity,ForceMode.VelocityChange);
            }

            // Start timer and add downward force
            inAirTimer += Time.deltaTime;
            playerRigidbody.AddForce(Vector3.down * fallingVelocity * inAirTimer);

            isGrounded = false;
            // No more touching ground, so no need to reset playerManager.isInteracting here
        }
        else
        {
            // We are touching the ground
            if (isAirborne)
            {
                // Play landing animation once, on first hit
                animatorManager.PlayTargetAnimation("Land", true);
                inAirTimer = 0f;
                isAirborne = false;

                // Kill horizontal drift
                Vector3 velocity = playerRigidbody.velocity;
                playerRigidbody.velocity = new Vector3(0f, velocity.y, 0f);
            }
            isGrounded = true;
        }
    }
}