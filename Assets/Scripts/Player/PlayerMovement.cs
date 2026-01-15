using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    private readonly SyncVar<Color> playerColor = new SyncVar<Color>();
    private readonly SyncVar<bool> isReady = new SyncVar<bool>();
    public bool IsReady => isReady.Value;

    private Renderer playerRenderer;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float minX = -8f;
    [SerializeField] private float maxX = 8f;
    [SerializeField] private float minY = -4f;
    [SerializeField] private float maxY = 4f;

    [Header("Input System")]
    private InputSystem_Actions inputActions; // <- Korrigierter Typ

    private InputAction moveAction;
    private InputAction lookAction;

    #region Inits
    private void Awake()
    {
        // Initialisiere Input Actions
        inputActions = new InputSystem_Actions();

        moveAction = inputActions.Player.Move;
        lookAction = inputActions.Player.Look;
    }

    private void OnDisable()
    {
        playerColor.OnChange -= OnColorChanged;
        if (!IsOwner) return;

        inputActions?.Disable();

        if (TimeManager != null)
            TimeManager.OnTick -= OnTick;
    }

    private void Start()
    {
        StartCoroutine(DelayedIsOwner());
    }

    private IEnumerator DelayedIsOwner()
    {
        playerColor.OnChange += OnColorChanged;
        playerRenderer = GetComponentInChildren<Renderer>();
        playerRenderer.material = new Material(playerRenderer.material);
        playerRenderer.material.color = playerColor.Value;
        yield return null;

        if (IsOwner)
        {
            ChangeColor(Random.value, Random.value, Random.value);

            inputActions?.Enable();

            if (TimeManager != null)
                TimeManager.OnTick += OnTick;
        }
    }
    #endregion

    private void OnTick()
    {
        if (!IsOwner) return;
        HandleInput();
        RotateTowardsMouse();
    }

    #region Rotation
    private void RotateTowardsMouse()
    {
        Vector2 mouseScreenPos = lookAction.ReadValue<Vector2>();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0));

        Vector2 direction = mouseWorldPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    #endregion

    #region ReadyStateHandling
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
    #endregion

    #region Movement
    private void HandleInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input != Vector2.zero)
        {
            Move(input);
        }
    }

    [ServerRpc]
    private void Move(Vector2 input)
    {
        float newX = transform.position.x + input.x * moveSpeed * (float)TimeManager.TickDelta;
        float newY = transform.position.y + input.y * moveSpeed * (float)TimeManager.TickDelta;

        newX = Mathf.Clamp(newX, minX, maxX);
        newY = Mathf.Clamp(newY, minY, maxY);

        transform.position = new Vector3(newX, newY, transform.position.z);
    }
    #endregion

    #region ColorChange
    [ServerRpc]
    private void ChangeColor(float r, float g, float b)
    {
        playerColor.Value = new Color(r, g, b);
    }

    private void OnColorChanged(Color prevColor, Color newColor, bool asServer)
    {
        playerRenderer.material.color = newColor;
    }
    #endregion
}