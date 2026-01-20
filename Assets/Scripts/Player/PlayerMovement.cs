using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    private readonly SyncVar<bool> isReady = new SyncVar<bool>();
    public bool IsReady => isReady.Value;

    private SpriteRenderer spriteRenderer;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float minX = -16f;
    [SerializeField] private float maxX = 16f;
    [SerializeField] private float minY = -16f;
    [SerializeField] private float maxY = 16f;

    [Header("Input System")]
    private InputSystem_Actions inputActions;

    private InputAction moveAction;
    private InputAction lookAction;

    private void Awake()
    {
        Debug.Log($"PlayerMovement: Awake called");

        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            moveAction = inputActions.Player.Move;
            lookAction = inputActions.Player.Look;
            Debug.Log("PlayerMovement: Input actions initialized in Awake");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"PlayerMovement: OnStartClient START - IsOwner: {IsOwner}, ObjectId: {ObjectId}");

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogError($"PlayerMovement: SpriteRenderer NOT FOUND! IsOwner: {IsOwner}, GameObject: {gameObject.name}");
            return;
        }

        Debug.Log($"PlayerMovement: SpriteRenderer found!");

        // Initialisiere Input hier für Owner
        if (IsOwner)
        {
            Debug.Log("PlayerMovement: Owner detected, initializing input");
            InitializeOwnerInput();
        }
    }

    private void InitializeOwnerInput()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
            Debug.Log("PlayerMovement: Input actions enabled");
        }
        else
        {
            Debug.LogError("PlayerMovement: inputActions is null in InitializeOwnerInput!");
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick; // Sicherheit: entferne falls schon registriert
            TimeManager.OnTick += OnTick;
            Debug.Log("PlayerMovement: OnTick registered to TimeManager");
        }
        else
        {
            Debug.LogError("PlayerMovement: TimeManager is null!");
        }
    }

    private void OnDisable()
    {
        if (!IsOwner) return;

        inputActions?.Disable();

        if (TimeManager != null)
            TimeManager.OnTick -= OnTick;

        Debug.Log("PlayerMovement: OnDisable - Input disabled and OnTick unregistered");
    }

    private void OnTick()
    {
        if (!IsOwner) return;
        HandleInput();
        HandleRotation();
    }

    private void HandleRotation()
    {
        Vector2 mouseScreenPos = lookAction.ReadValue<Vector2>();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0));

        Vector2 direction = mouseWorldPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        RotateServerRpc(angle);
    }

    [ServerRpc]
    private void RotateServerRpc(float angle)
    {
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    [ServerRpc]
    public void SetReadyStateServerRpc(string name)
    {
        isReady.Value = !isReady.Value;

        if (transform.position.x < 0)
        {
            NetworkGameManager.Instance.Player1.Value = name;
        }
        else
        {
            NetworkGameManager.Instance.Player2.Value = name;
        }

        NetworkGameManager.Instance.DisableNameField(Owner, isReady.Value);
        NetworkGameManager.Instance.CheckAndStartGame();
    }

    private void HandleInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input != Vector2.zero)
        {
            MoveServerRpc(input);
        }
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        float newX = transform.position.x + input.x * moveSpeed * (float)TimeManager.TickDelta;
        float newY = transform.position.y + input.y * moveSpeed * (float)TimeManager.TickDelta;

        newX = Mathf.Clamp(newX, minX, maxX);
        newY = Mathf.Clamp(newY, minY, maxY);

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
}