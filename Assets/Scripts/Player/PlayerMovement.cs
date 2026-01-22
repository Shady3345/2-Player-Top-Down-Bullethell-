using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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

    // Ready System
    private readonly SyncVar<bool> isReady = new SyncVar<bool>(false);
    private readonly SyncVar<string> playerName = new SyncVar<string>("");
    public bool IsReady => isReady.Value;
    public string PlayerName => playerName.Value;

    // Spieler-Identifikation
    private int myPlayerSlot = -1;

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

        // Ready State Listener
        isReady.OnChange += (oldVal, newVal, asServer) =>
        {
            Debug.Log($"Player {playerName.Value} ready state changed: {oldVal} -> {newVal}");
        };

        playerName.OnChange += (oldVal, newVal, asServer) =>
        {
            Debug.Log($"Player name changed: {oldVal} -> {newVal}");
            UpdatePlayerNameInGameManager();
        };
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
        Debug.Log($"PlayerMovement: OnDisable called - IsOwner: {IsOwner}");

        if (!IsOwner) return;

        // Disable Input Actions
        if (inputActions != null)
        {
            inputActions.Disable();
            Debug.Log("PlayerMovement: Input actions disabled");
        }

        // Unregister from TimeManager
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            Debug.Log("PlayerMovement: OnTick unregistered");
        }
    }

    private void OnEnable()
    {
        Debug.Log($"PlayerMovement: OnEnable called - IsOwner: {IsOwner}");

        // Nur für Owner und nach der initialen Initialisierung
        if (!IsOwner || inputActions == null) return;

        // Re-enable Input Actions wenn Komponente aktiviert wird (z.B. nach Respawn)
        if (inputActions != null)
        {
            inputActions.Enable();
            Debug.Log("PlayerMovement: Input actions re-enabled");
        }

        // Re-register to TimeManager
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick; // Safety: remove if already registered
            TimeManager.OnTick += OnTick;
            Debug.Log("PlayerMovement: OnTick re-registered");
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

    #region Ready System

    [ServerRpc]
    public void SetReadyStateServerRpc(string name)
    {
        Debug.Log($"SetReadyStateServerRpc called: Name={name}, CurrentReady={isReady.Value}");

        // Setze Namen wenn noch nicht gesetzt
        if (string.IsNullOrEmpty(playerName.Value))
        {
            playerName.Value = name;
            myPlayerSlot = DeterminePlayerSlot();
            Debug.Log($"Player {name} assigned to slot {myPlayerSlot}");
        }

        // Toggle Ready State
        isReady.Value = !isReady.Value;
        Debug.Log($"Player {playerName.Value} ready state is now: {isReady.Value}");

        // Benachrichtige GameManager
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.CheckAndStartGame();
        }
    }

    [Server]
    public void ResetReadyState()
    {
        isReady.Value = false;
        Debug.Log($"Player {playerName.Value} ready state reset");
    }

    [Server]
    private int DeterminePlayerSlot()
    {
        // Bestimme Slot basierend auf Spawn-Position oder Reihenfolge
        var allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        int slot = 0;

        foreach (var player in allPlayers)
        {
            if (player == this) break;
            if (!string.IsNullOrEmpty(player.PlayerName))
                slot++;
        }

        return slot;
    }

    private void UpdatePlayerNameInGameManager()
    {
        if (!IsServerStarted || NetworkGameManager.Instance == null) return;

        if (myPlayerSlot == 0)
        {
            NetworkGameManager.Instance.Player1.Value = playerName.Value;
        }
        else if (myPlayerSlot == 1)
        {
            NetworkGameManager.Instance.Player2.Value = playerName.Value;
        }
    }

    #endregion

    #region Helper Methods

    // HELPER: Gibt den Spieler-Slot zurück (0 = Player1, 1 = Player2)
    public int GetPlayerSlot()
    {
        return myPlayerSlot;
    }

    // OPTIONAL: Wenn du Health auf dem Player tracken willst
    public int GetHealth()
    {
        if (NetworkGameManager.Instance != null)
        {
            return NetworkGameManager.Instance.GetPlayerHealth(GetPlayerSlot());
        }
        return 100;
    }

    #endregion
}