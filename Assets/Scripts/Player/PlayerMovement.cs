using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player movement and rotation:
/// - Uses the new Input System
/// - Only processes input for the owning client
/// - Sends movement and rotation to the server via ServerRPCs
/// - Uses FishNet Tick system for movement timing
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    // Sprite renderer for potential visual changes (flip, effects, etc.)
    private SpriteRenderer spriteRenderer;

    #region Movement Settings

    [SerializeField] private float moveSpeed = 5f;

    // World boundaries to clamp player movement
    [SerializeField] private float minX = -16f;
    [SerializeField] private float maxX = 16f;
    [SerializeField] private float minY = -16f;
    [SerializeField] private float maxY = 16f;

    #endregion

    #region Input System

    [Header("Input System")]
    private InputSystem_Actions inputActions;
    private InputAction moveAction;
    private InputAction lookAction;

    #endregion

    private void Awake()
    {
        // Initialize input actions once
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            moveAction = inputActions.Player.Move;
            lookAction = inputActions.Player.Look;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Cache sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Only the owning client handles input
        if (IsOwner)
        {
            InitializeOwnerInput();
        }
    }

    /// <summary>
    /// Enables input and registers to the network tick for the owning client.
    /// </summary>
    private void InitializeOwnerInput()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
        }

        // Subscribe to FishNet tick event
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }
    }

    private void OnEnable()
    {
        if (!IsOwner || inputActions == null) return;

        // Re-enable input when object is enabled
        inputActions.Enable();

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }
    }

    private void OnDisable()
    {
        if (!IsOwner) return;

        // Disable input when object is disabled
        if (inputActions != null)
        {
            inputActions.Disable();
        }

        // Unsubscribe from tick event
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    /// <summary>
    /// Called every network tick (server-authoritative movement).
    /// </summary>
    private void OnTick()
    {
        if (!IsOwner) return;

        // Only allow movement while the game is running
        if (NetworkGameManager.Instance != null &&
            NetworkGameManager.Instance.IsGamePlaying())
        {
            HandleInput();
            HandleRotation();
        }
    }

    /// <summary>
    /// Handles player rotation based on mouse position.
    /// Rotation is sent to the server.
    /// </summary>
    private void HandleRotation()
    {
        // Read mouse position from Input System (screen space)
        Vector2 mouseScreenPos = lookAction.ReadValue<Vector2>();

        // Convert to world space
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0)
        );

        // Calculate direction and rotation angle
        Vector2 direction = mouseWorldPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        RotateServerRpc(angle);
    }

    /// <summary>
    /// Applies rotation on the server.
    /// </summary>
    [ServerRpc]
    private void RotateServerRpc(float angle)
    {
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// Reads movement input and sends it to the server.
    /// </summary>
    private void HandleInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input != Vector2.zero)
        {
            MoveServerRpc(input);
        }
    }

    /// <summary>
    /// Applies movement on the server using tick delta time
    /// and clamps position to world bounds.
    /// </summary>
    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        float newX = transform.position.x +
                     input.x * moveSpeed * (float)TimeManager.TickDelta;

        float newY = transform.position.y +
                     input.y * moveSpeed * (float)TimeManager.TickDelta;

        // Clamp movement to allowed area
        newX = Mathf.Clamp(newX, minX, maxX);
        newY = Mathf.Clamp(newY, minY, maxY);

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}
