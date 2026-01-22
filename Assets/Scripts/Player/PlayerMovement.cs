using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
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
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (IsOwner)
        {
            InitializeOwnerInput();
        }
    }

    private void InitializeOwnerInput()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }
    }

    private void OnDisable()
    {
        if (!IsOwner) return;

        if (inputActions != null)
        {
            inputActions.Disable();
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnEnable()
    {
        if (!IsOwner || inputActions == null) return;

        if (inputActions != null)
        {
            inputActions.Enable();
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }
    }

    private void OnTick()
    {
        if (!IsOwner) return;

        // Nur Bewegung erlauben wenn das Spiel läuft
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsGamePlaying())
        {
            HandleInput();
            HandleRotation();
        }
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