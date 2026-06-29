using UnityEngine;

public class UiModelAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera to position the model relative to. Defaults to Camera.main if left empty.")]
    public Camera targetCamera;

    [Header("Final Position & Rotation")]
    [Tooltip("If true, the object will fly to its initial position in the scene. If false, it uses the positionOffset relative to the camera.")]
    public bool flyToInitialPosition = true;

    [Tooltip("The final position offset relative to the camera. (e.g., Z=5 puts it 5 units in front of the camera). Used if flyToInitialPosition is false.")]
    public Vector3 positionOffset = new Vector3(0, 0, 5f);
    
    [Tooltip("Base rotation offset relative to the camera.")]
    public Vector3 baseRotationOffset = Vector3.zero;

    [Header("Fly-in Animation Settings")]
    [Tooltip("How long the fly-in animation takes in seconds.")]
    public float flyInDuration = 1.0f;
    
    [Tooltip("Where the model starts flying from, relative to the camera. Use negative Z to start behind the camera.")]
    public Vector3 startOffsetFromCamera = new Vector3(0, -2f, -5f);
    
    [Tooltip("Curve controlling the fly-in movement. Default is Ease In/Out.")]
    public AnimationCurve flyInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pendulum Animation Settings")]
    [Tooltip("Speed of the pendulum swing.")]
    public float pendulumSpeed = 2f;
    
    [Tooltip("Maximum angle of the pendulum swing on each axis.")]
    public Vector3 pendulumAngle = new Vector3(0, 10f, 0);

    // Internal state
    private float _flyInTimer;
    private bool _isFlyingIn;
    private Vector3 _initialWorldPosition;

    void Awake()
    {
        _initialWorldPosition = transform.position;
    }

    void OnEnable()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Reset state every time the object is enabled
        _flyInTimer = 0f;
        _isFlyingIn = true;
    }

    void Update()
    {
        if (targetCamera == null) return;

        // Calculate Target Position
        Vector3 targetPos;
        if (flyToInitialPosition)
        {
            targetPos = _initialWorldPosition;
        }
        else
        {
            targetPos = targetCamera.transform.position + targetCamera.transform.rotation * positionOffset;
        }

        // 1. Handle Fly-in Position
        if (_isFlyingIn)
        {
            _flyInTimer += Time.deltaTime;
            float t = _flyInTimer / flyInDuration;
            
            if (t >= 1f)
            {
                t = 1f;
                _isFlyingIn = false;
            }

            // Evaluate the animation curve for smooth movement
            float curvedT = flyInCurve.Evaluate(t);
            
            // Calculate start position relative to current camera position
            Vector3 startPos = targetCamera.transform.position + targetCamera.transform.rotation * startOffsetFromCamera;
            
            // Lerp between start and target position
            transform.position = Vector3.Lerp(startPos, targetPos, curvedT);
        }
        else
        {
            // Once fly-in is done, snap to target position so it stays there
            transform.position = targetPos;
        }

        // 2. Handle Rotation (Base Offset + Pendulum Spin)
        Vector3 currentPendulumRotation = new Vector3(
            Mathf.Sin(Time.time * pendulumSpeed) * pendulumAngle.x,
            Mathf.Sin(Time.time * pendulumSpeed) * pendulumAngle.y,
            Mathf.Sin(Time.time * pendulumSpeed) * pendulumAngle.z
        );
        
        // Base rotation facing the camera + user offset
        Quaternion baseRotation = targetCamera.transform.rotation * Quaternion.Euler(baseRotationOffset);
        
        // Apply the pendulum spin on top of the base rotation
        transform.rotation = baseRotation * Quaternion.Euler(currentPendulumRotation);
    }
}
