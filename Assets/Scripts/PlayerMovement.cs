using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float sprintAccelerationTime = 0.25f;
    public float runStopTime = 0.2f;
    public float airForwardSpeed = 8f;
    public float airBackwardSpeed = 6f;
    public float airDirectionResponsiveness = 10f;
    public float jumpPower = 8f;
    public float gravity = 25f;
    public float groundedGravity = -2f;
    public float fallMultiplier = 2.2f;
    public float lowJumpMultiplier = 1.8f;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;
    public AudioClip[] footstepClips;
    public bool autoLoadFootstepsFromResources = true;
    public string footstepResourcesPath = "Audio/Footsteps";
    public float footstepVolume = 0.6f;
    public float runFootstepVolumeMultiplier = 1.2f;
    public float landingVolumeMultiplier = 1f;
    public float footstepIntervalWalk = 0.45f;
    public float footstepIntervalRun = 0.3f;
    public float footstepIntervalCrouch = 0.6f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private float verticalVelocity = 0f;
    private float sprintBlend = 0f;
    private float footstepTimer = 0f;
    private Vector3 currentHorizontalVelocity = Vector3.zero;
    private int lastFootstepClipIndex = -1;
    private bool wasGrounded = false;
    private bool wasMovingOnGround = false;
    private CharacterController characterController;
    private AudioSource audioSource;

    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (autoLoadFootstepsFromResources)
        {
            LoadFootstepsFromResources();
        }

        wasGrounded = characterController.isGrounded;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        bool isGrounded = characterController.isGrounded;
        bool isCrouching = canMove && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        bool sprintRequested = canMove && Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        bool jumpPressed = Input.GetButtonDown("Jump") && canMove && isGrounded;
        bool useAirControl = !isGrounded || jumpPressed;

        Vector2 rawInput = canMove
            ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))
            : Vector2.zero;
        rawInput = Vector2.ClampMagnitude(rawInput, 1f);
        bool hasMoveInput = rawInput.sqrMagnitude > 0.0001f;

        float sprintStep = (sprintAccelerationTime > 0f) ? Time.deltaTime / sprintAccelerationTime : 1f;
        float sprintTarget = (sprintRequested && hasMoveInput && isGrounded) ? 1f : 0f;
        sprintBlend = Mathf.MoveTowards(sprintBlend, sprintTarget, sprintStep);

        float moveSpeed = isCrouching ? crouchSpeed : Mathf.Lerp(walkSpeed, runSpeed, sprintBlend);
        Vector3 desiredHorizontalVelocity = ((forward * rawInput.y) + (right * rawInput.x)) * moveSpeed;

        if (!useAirControl && hasMoveInput)
        {
            currentHorizontalVelocity = desiredHorizontalVelocity;
        }
        else if (!useAirControl)
        {
            float currentSpeed = currentHorizontalVelocity.magnitude;
            if (currentSpeed > walkSpeed)
            {
                float stopTime = Mathf.Max(0.01f, runStopTime);
                float deceleration = runSpeed / stopTime;
                currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, Vector3.zero, deceleration * Time.deltaTime);
            }
            else
            {
                currentHorizontalVelocity = Vector3.zero;
            }
        }
        else
        {
            float airInput = rawInput.y;
            if (Mathf.Abs(airInput) > 0.01f)
            {
                Vector3 cameraForward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
                Vector3 cameraForwardFlat = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
                if (cameraForwardFlat.sqrMagnitude < 0.0001f)
                {
                    cameraForwardFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                }
                cameraForwardFlat.Normalize();

                float targetSpeed = airInput > 0f ? airForwardSpeed : airBackwardSpeed;
                Vector3 targetAirVelocity = cameraForwardFlat * Mathf.Sign(airInput) * targetSpeed;

                // In air use only forward/back relative to camera; A/D is intentionally ignored.
                currentHorizontalVelocity = Vector3.MoveTowards(
                    currentHorizontalVelocity,
                    targetAirVelocity,
                    airDirectionResponsiveness * Time.deltaTime
                );
            }
        }

        moveDirection.x = currentHorizontalVelocity.x;
        moveDirection.z = currentHorizontalVelocity.z;

        if (isGrounded)
        {
            // Keep the controller grounded on slopes and small steps.
            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }

            if (jumpPressed)
            {
                verticalVelocity = jumpPower;
            }
        }
        else
        {
            float gravityMultiplier = 1f;

            // Faster fall and shorter jump when the player releases jump early.
            if (verticalVelocity < 0f)
            {
                gravityMultiplier = fallMultiplier;
            }
            else if (!Input.GetButton("Jump"))
            {
                gravityMultiplier = lowJumpMultiplier;
            }

            verticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
        }

        moveDirection.y = verticalVelocity;

        if (isCrouching)
        {
            characterController.height = crouchHeight;
        }
        else
        {
            characterController.height = defaultHeight;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        if (!wasGrounded && characterController.isGrounded)
        {
            PlayRandomFootstep(landingVolumeMultiplier);
        }
        wasGrounded = characterController.isGrounded;

        bool isRunning = currentHorizontalVelocity.magnitude > walkSpeed + 0.1f;
        PlayFootsteps(isCrouching, isRunning);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }

    void PlayFootsteps(bool isCrouching, bool isRunning)
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            return;
        }

        Vector3 horizontalVelocity = characterController.velocity;
        horizontalVelocity.y = 0f;
        bool isMoving = horizontalVelocity.sqrMagnitude > 0.1f;

        if (!characterController.isGrounded || !isMoving || !canMove)
        {
            footstepTimer = 0f;
            wasMovingOnGround = false;
            return;
        }

        float interval = footstepIntervalWalk;
        if (isCrouching)
        {
            interval = footstepIntervalCrouch;
        }
        else if (isRunning)
        {
            interval = footstepIntervalRun;
        }

        if (!wasMovingOnGround)
        {
            float startStepMultiplier = isRunning ? runFootstepVolumeMultiplier : 1f;
            PlayRandomFootstep(startStepMultiplier);
            footstepTimer = 0f;
            wasMovingOnGround = true;
            return;
        }

        footstepTimer += Time.deltaTime;
        if (footstepTimer >= interval)
        {
            footstepTimer = 0f;
            float stepMultiplier = isRunning ? runFootstepVolumeMultiplier : 1f;
            PlayRandomFootstep(stepMultiplier);
        }

        wasMovingOnGround = true;
    }

    void PlayRandomFootstep(float volumeMultiplier)
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            return;
        }

        int clipIndex = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && clipIndex == lastFootstepClipIndex)
        {
            clipIndex = (clipIndex + 1) % footstepClips.Length;
        }

        AudioClip selectedClip = footstepClips[clipIndex];
        lastFootstepClipIndex = clipIndex;

        if (selectedClip != null)
        {
            audioSource.PlayOneShot(selectedClip, footstepVolume * volumeMultiplier);
        }
    }

    void LoadFootstepsFromResources()
    {
        if (string.IsNullOrWhiteSpace(footstepResourcesPath))
        {
            return;
        }

        AudioClip[] loadedClips = Resources.LoadAll<AudioClip>(footstepResourcesPath);
        if (loadedClips != null && loadedClips.Length > 0)
        {
            footstepClips = loadedClips;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Krzak krzak = hit.collider.GetComponentInParent<Krzak>();
        if (krzak == null)
        {
            return;
        }

        Vector3 kickDirection = currentHorizontalVelocity;
        kickDirection.y = 0f;
        if (kickDirection.sqrMagnitude < 0.0001f)
        {
            krzak.StopMovement();
            return;
        }

        Vector3 toObject = hit.collider.bounds.center - transform.position;
        toObject.y = 0f;
        if (toObject.sqrMagnitude > 0.0001f)
        {
            float pushAlignment = Vector3.Dot(kickDirection.normalized, toObject.normalized);
            if (pushAlignment <= 0f)
            {
                return;
            }
        }

        krzak.KickFromWorldDirection(kickDirection);
    }
}