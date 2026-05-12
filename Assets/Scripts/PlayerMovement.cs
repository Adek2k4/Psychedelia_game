using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController), typeof(AudioSource))]
public class PlayerMovement : NetworkBehaviour
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
    public float walkBobAmplitude = 0.03f;
    public float runBobAmplitude = 0.06f;
    public float walkBobFrequency = 12f;
    public float runBobFrequency = 18f;
    public float bobLerpSpeed = 10f;
    public AudioClip[] footstepClips;
    public bool autoLoadFootstepsFromResources = true;
    public string footstepResourcesPath = "Audio/Footsteps";
    public float footstepVolume = 0.6f;
    public float runFootstepVolumeMultiplier = 1.2f;
    public float landingVolumeMultiplier = 1f;
    public float footstepIntervalWalk = 0.45f;
    public float footstepIntervalRun = 0.3f;
    public float spawnResyncDelay = 0.05f;
    public float spawnResyncWindow = 1.5f;
    public float groundedGraceTime = 0.1f;
    public float landingMinAirTime = 0.12f;

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
    private Vector3 defaultCameraLocalPos;
    private bool defaultCameraLocalPosCached = false;
    private float bobTimer = 0f;
    private float currentBobOffset = 0f;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float spawnTime = -999f;
    private float lastGroundedTime = -999f;
    private float airborneStartTime = -999f;

    private bool canMove = true;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            defaultCameraLocalPos = playerCamera.transform.localPosition;
            defaultCameraLocalPosCached = true;
        }

        if (autoLoadFootstepsFromResources)
        {
            LoadFootstepsFromResources();
        }
    }

    public override void OnNetworkSpawn()
    {
        SetLocalState(IsOwner);
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        spawnTime = Time.time;

        if (IsServer)
        {
            StartCoroutine(ResyncSpawnPosition());
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        canMove = enabled;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void SetLocalState(bool isLocalPlayer)
    {
        canMove = isLocalPlayer;

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocalPlayer;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = isLocalPlayer;
            }
        }

        if (characterController != null)
        {
            characterController.enabled = isLocalPlayer;
        }

        if (!isLocalPlayer)
        {
            enabled = false;
            return;
        }

        wasGrounded = characterController != null && characterController.isGrounded;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        bool isGroundedRaw = characterController.isGrounded;
        if (isGroundedRaw)
        {
            lastGroundedTime = Time.time;
        }
        bool isGrounded = isGroundedRaw || (Time.time - lastGroundedTime <= groundedGraceTime);
        bool sprintRequested = canMove && Input.GetKey(KeyCode.LeftShift);
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

        float moveSpeed = Mathf.Lerp(walkSpeed, runSpeed, sprintBlend);
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

        characterController.Move(moveDirection * Time.deltaTime);

        bool groundedAfterMoveRaw = characterController.isGrounded;
        if (groundedAfterMoveRaw)
        {
            lastGroundedTime = Time.time;
        }
        bool groundedAfterMove = groundedAfterMoveRaw || (Time.time - lastGroundedTime <= groundedGraceTime);

        if (!groundedAfterMove)
        {
            if (wasGrounded)
            {
                airborneStartTime = Time.time;
            }
        }
        else if (!wasGrounded && groundedAfterMoveRaw)
        {
            float airTime = Time.time - airborneStartTime;
            if (airTime >= landingMinAirTime)
            {
                PlayRandomFootstep(landingVolumeMultiplier);
            }
        }

        wasGrounded = groundedAfterMove;

        bool isRunning = currentHorizontalVelocity.magnitude > walkSpeed + 0.1f;
        PlayFootsteps(isRunning, groundedAfterMove);
        UpdateCameraBob(isRunning, groundedAfterMove);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }


    void PlayFootsteps(bool isRunning, bool isGrounded)
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            return;
        }

        Vector3 horizontalVelocity = currentHorizontalVelocity;
        horizontalVelocity.y = 0f;
        bool isMoving = horizontalVelocity.sqrMagnitude > 0.1f;

        if (!isGrounded || !isMoving || !canMove)
        {
            footstepTimer = 0f;
            wasMovingOnGround = false;
            return;
        }

        float interval = footstepIntervalWalk;
        if (isRunning)
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

    void UpdateCameraBob(bool isRunning, bool isGrounded)
    {
        if (playerCamera == null || !defaultCameraLocalPosCached)
        {
            return;
        }

        bool isMoving = currentHorizontalVelocity.sqrMagnitude > 0.1f;
        bool shouldBob = canMove && isGrounded && isMoving;

        float targetOffset = 0f;
        if (shouldBob)
        {
            float freq = Mathf.Max(0f, isRunning ? runBobFrequency : walkBobFrequency);
            float amp = Mathf.Max(0f, isRunning ? runBobAmplitude : walkBobAmplitude);
            bobTimer += Time.deltaTime * freq;
            targetOffset = Mathf.Sin(bobTimer) * amp;
        }
        else
        {
            bobTimer = 0f;
        }

        float lerpSpeed = Mathf.Max(0f, bobLerpSpeed);
        if (lerpSpeed <= 0f)
        {
            currentBobOffset = targetOffset;
        }
        else
        {
            currentBobOffset = Mathf.Lerp(currentBobOffset, targetOffset, Time.deltaTime * lerpSpeed);
        }

        Vector3 camPos = defaultCameraLocalPos;
        camPos.y += currentBobOffset;
        playerCamera.transform.localPosition = camPos;
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

    System.Collections.IEnumerator ResyncSpawnPosition()
    {
        if (spawnResyncDelay > 0f)
        {
            yield return new WaitForSeconds(spawnResyncDelay);
        }
        else
        {
            yield return null;
        }

        if (!IsSpawned)
        {
            yield break;
        }

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        ForceSpawnResyncClientRpc(spawnPosition, spawnRotation, rpcParams);
    }

    [ClientRpc]
    void ForceSpawnResyncClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        if (spawnResyncWindow > 0f && Time.time - spawnTime > spawnResyncWindow)
        {
            return;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(pos, rot);

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsOwner || !canMove)
        {
            return;
        }

        Krzak krzak = hit.collider.GetComponentInParent<Krzak>();
        if (krzak == null)
        {
            return;
        }

        Vector3 kickDirection = characterController.velocity;
        kickDirection.y = 0f;
        if (kickDirection.sqrMagnitude < 0.0001f)
        {
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

        krzak.RequestKickFromClient(kickDirection);
    }
}