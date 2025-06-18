using UnityEngine;
using UnityEngine.UIElements;

public class CameraManager : MonoBehaviour
{
    InputManager inputManager;

    public Transform targetTransform;       // The object the camera is following
    public Transform cameraPivot;           // The object the camera uses to pivot
    public Transform cameraTransform;       // Transform of the Main Camera object
    public LayerMask collisionLayers;       // The layers the camera can collide with
    private float defaultPosition;          // Default camera position
    private Vector3 cameraFollowVelocity = Vector3.zero;
    private Vector3 cameraVectorPosition;

    public float cameraCollisionRadius = 0.2f;
    public float cameraCollisionOffset = 0.2f;
    public float minimumCollisionOffset = 0.2f;
    public float cameraFollowSpeed = 0.2f;
    public float cameraLookSpeed = 2;
    public float cameraPivotSpeed = 2;

    public float lookAngle;                 // Camera up and down
    public float pivotAngle;                // Camera left and right
    public float minimumPivotAngle = -25;   // Max camera low pivot
    public float maximumPivotAngle = 55;    // Max camera high pivot


    private void Awake()
    {
        inputManager = FindObjectOfType<InputManager>();
        targetTransform = FindObjectOfType<PlayerManager>().transform;
        cameraTransform = Camera.main.transform;
        defaultPosition = cameraTransform.localPosition.z;
    }


    public void HandleAllCameraMovement()
    {
        FollowTarget();
        RotateCamera();
        HandleCameraCollisions();
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = Vector3.SmoothDamp(
            transform.position,
            targetTransform.position,
            ref cameraFollowVelocity,
            cameraFollowSpeed
        );

        transform.position = targetPosition;
    }

    private void RotateCamera()
    {
        Vector3 rotation;

        lookAngle += (inputManager.cameraXInput * cameraLookSpeed);
        pivotAngle -= (inputManager.cameraYInput * cameraPivotSpeed);

        // Limit rotation
        pivotAngle = Mathf.Clamp(pivotAngle, minimumPivotAngle, maximumPivotAngle);

        rotation = Vector3.zero;
        rotation.y = lookAngle;
        Quaternion targetRotation = Quaternion.Euler(rotation);
        transform.rotation = targetRotation;

        rotation = Vector3.zero;
        rotation.x = pivotAngle;
        targetRotation = Quaternion.Euler(rotation);
        cameraPivot.localRotation = targetRotation;
    }

    private void HandleCameraCollisions()
    {
        // Start with the default camera distance
        float targetPosition = defaultPosition; 

        // Maximum distance when no collision occurs
        float distance = Mathf.Abs(defaultPosition); 

        RaycastHit hit;

        // Calculate direction vector from pivot to camera
        Vector3 direction = (cameraTransform.position - cameraPivot.position).normalized;

        // Perform SphereCast to detect obstacles
        if (Physics.SphereCast(cameraPivot.position, cameraCollisionRadius, direction, out hit, Mathf.Abs(defaultPosition), collisionLayers))
        {
            // Calculate distance to the obstacle
            distance = Vector3.Distance(cameraPivot.position, hit.point);

            // Adjust target position based on collision, but ensure it stays within the minimum offset
            targetPosition = -(Mathf.Max(distance - cameraCollisionOffset, minimumCollisionOffset));
        }
        else
        {
            // No collision; use the default position
            targetPosition = defaultPosition;
        }

        // Smoothly interpolate the camera's position
        cameraVectorPosition.z = Mathf.Lerp(cameraTransform.localPosition.z, targetPosition, 0.4f);
        cameraTransform.localPosition = cameraVectorPosition;
    }
}
