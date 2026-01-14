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
    [SerializeField] private InputAction moveAction; // <- Muss Vector2 sein!
    [SerializeField] private InputAction colorChangeAction;

    #region Inits
    private void OnDisable()
    {
        playerColor.OnChange -= OnColorChanged;
        if (!IsOwner) return;

        moveAction?.Disable();
        colorChangeAction?.Disable();
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

            moveAction?.Enable();
            colorChangeAction?.Enable();
            if (TimeManager != null)
                TimeManager.OnTick += OnTick;
        }
    }
    #endregion

    private void OnTick()
    {
        if (!IsOwner) return;
        HandleInput();
        /*
        if (isReady.Value)
        {
            HandleInput();
        }
        else
        {
            CheckForChangeColor();
        } */
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
        // Lies Vector2 für 2D Movement auf X und Y Achse
        Vector2 input = moveAction.ReadValue<Vector2>();
        Debug.Log($"Input X: {input.x}, Input Y: {input.y}");

        if (input != Vector2.zero)
        {
            Debug.Log($"Input: {input}"); // Debug zum Testen
            Move(input);
        }
    }

    [ServerRpc]
    private void Move(Vector2 input)
    {
        // Berechne neue Position für X und Y Achse (2D Movement)
        float newX = transform.position.x + input.x * moveSpeed * (float)TimeManager.TickDelta;
        float newY = transform.position.y + input.y * moveSpeed * (float)TimeManager.TickDelta;

        // Clampe die Position innerhalb der Grenzen
        newX = Mathf.Clamp(newX, minX, maxX);
        newY = Mathf.Clamp(newY, minY, maxY);

        transform.position = new Vector3(newX, newY, transform.position.z);
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
        playerRenderer.material.color = newColor;
    }
    #endregion
}