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
    [SerializeField] private float minZ = -4f;
    [SerializeField] private float maxZ = 4f;

    [Header("Input System")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction colorChangeAction;

    private bool isInitialized = false;

    #region Inits
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!base.IsOwner) return;

        // Enable input actions for owner
        moveAction?.Enable();
        colorChangeAction?.Enable();

        if (TimeManager != null)
            TimeManager.OnTick += OnTick;

        isInitialized = true;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        playerColor.OnChange -= OnColorChanged;

        if (isInitialized && base.IsOwner)
        {
            moveAction?.Disable();
            colorChangeAction?.Disable();

            if (TimeManager != null)
                TimeManager.OnTick -= OnTick;
        }

        isInitialized = false;
    }

    private void OnDisable()
    {
        // Only unsubscribe from color changes here
        playerColor.OnChange -= OnColorChanged;
    }

    private void Start()
    {
        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        playerColor.OnChange += OnColorChanged;
        playerRenderer = GetComponentInChildren<Renderer>();

        if (playerRenderer != null)
        {
            playerRenderer.material = new Material(playerRenderer.material);
            playerRenderer.material.color = playerColor.Value;
        }

        yield return null;

        if (base.IsOwner)
        {
            ChangeColor(Random.value, Random.value, Random.value);
        }
    }
    #endregion

    private void OnTick()
    {
        if (!base.IsOwner) return;
        HandleInput();
    }

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
        float newZ = transform.position.z + input.y * moveSpeed * (float)TimeManager.TickDelta;

        newX = Mathf.Clamp(newX, minX, maxX);
        newZ = Mathf.Clamp(newZ, minZ, maxZ);

        transform.position = new Vector3(newX, transform.position.y, newZ);
    }
    #endregion

    #region ColorChange
    private void CheckForChangeColor()
    {
        if (!colorChangeAction.triggered) return;

        float r = Random.value;
        float g = Random.value;
        float b = Random.value;
        ChangeColor(r, g, b);
    }

    [ServerRpc]
    private void ChangeColor(float r, float g, float b)
    {
        playerColor.Value = new Color(r, g, b);
    }

    private void OnColorChanged(Color prevColor, Color newColor, bool asServer)
    {
        if (playerRenderer != null)
        {
            playerRenderer.material.color = newColor;
        }
    }
    #endregion
}