using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Handles all player state and lifecycle logic:
/// - Health & damage
/// - Temporary invincibility
/// - Death & respawn
/// - Ready system for lobby / game start
/// - Server-authoritative state with SyncVars
/// </summary>
public class PlayerStats : NetworkBehaviour
{
    #region Health

    [Header("Health")]
    public int maxHealth = 100;
    private readonly SyncVar<int> currentHealth = new SyncVar<int>();

    #endregion

    #region Invincibility

    [Header("Invincibility")]
    public float invincibilityDuration = 1f;
    private readonly SyncVar<bool> isInvincible = new SyncVar<bool>();
    private float invincibilityTimer = 0f;

    #endregion

    #region Respawn

    [Header("Respawn")]
    public Transform[] spawnPoints;

    #endregion

    #region Ready System

    [Header("Ready System")]
    private readonly SyncVar<bool> isReady = new SyncVar<bool>(false);
    private readonly SyncVar<string> playerName = new SyncVar<string>("");

    // Server-only write, readable by all observers
    private readonly SyncVar<int> playerIndex =
        new SyncVar<int>(new SyncTypeSettings(
            WritePermission.ServerOnly,
            ReadPermission.Observers));

    #endregion

    // Initial transform for fallback respawn
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    #region Public Getters

    public bool IsReady => isReady.Value;
    public string PlayerName => playerName.Value;
    public int GetCurrentHealth() => currentHealth.Value;
    public int GetPlayerIndex() => playerIndex.Value;
    public bool IsInvincible() => isInvincible.Value;
    public bool IsAlive() => currentHealth.Value > 0;

    #endregion

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // Subscribe to health changes
        currentHealth.OnChange += OnHealthChanged;

        // Cache initial transform
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (IsServerStarted)
        {
            currentHealth.Value = maxHealth;
            isInvincible.Value = false;
            RegisterWithGameManager();
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        currentHealth.OnChange -= OnHealthChanged;

        if (IsServerStarted && NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.UnregisterPlayer(this);
        }
    }

    #region Registration

    [Server]
    private void RegisterWithGameManager()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("NetworkGameManager Instance not found");
        }
    }

    [Server]
    public void SetPlayerIndex(int index)
    {
        playerIndex.Value = index;
    }

    #endregion

    private void Update()
    {
        // Server-side invincibility timer
        if (IsServerStarted && isInvincible.Value)
        {
            invincibilityTimer -= Time.deltaTime;

            if (invincibilityTimer <= 0f)
            {
                isInvincible.Value = false;
            }
        }
    }

    #region Health Logic

    /// <summary>
    /// Applies damage to the player (server-authoritative).
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!IsServerStarted) return;
        if (isInvincible.Value) return;
        if (currentHealth.Value <= 0) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        if (currentHealth.Value <= 0)
        {
            Die();
        }
        else
        {
            // Activate temporary invincibility
            isInvincible.Value = true;
            invincibilityTimer = invincibilityDuration;
        }
    }

    /// <summary>
    /// Heals the player up to max health.
    /// </summary>
    public void Heal(int amount)
    {
        if (!IsServerStarted) return;
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
    }

    #endregion

    #region Health Change Callback

    private void OnHealthChanged(int prev, int next, bool asServer)
    {
        // Inform GameManager
        if (NetworkGameManager.Instance != null && playerIndex.Value >= 0)
        {
            NetworkGameManager.Instance.OnPlayerHealthChanged(playerIndex.Value, next);
        }

        // Client-side visuals
        if (IsClientStarted)
        {
            OnHealthChangedClient(prev, next);
        }
    }

    private void OnHealthChangedClient(int prev, int next)
    {
        // Visual feedback (UI, flash, effects) can be added here
    }

    #endregion

    #region Death & Respawn

    /// <summary>
    /// Handles player death on the server.
    /// </summary>
    private void Die()
    {
        if (!IsServerStarted) return;

        DisablePlayer();
        NotifyEnemiesOfDeath();

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.CheckGameOver();
        }
    }

    /// <summary>
    /// Notifies all enemies that this player died,
    /// so they can re-target.
    /// </summary>
    [Server]
    private void NotifyEnemiesOfDeath()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            enemy?.OnPlayerDied(gameObject);
        }
    }

    /// <summary>
    /// Disables player components on the server and clients.
    /// </summary>
    [Server]
    private void DisablePlayer()
    {
        TogglePlayerComponents(false);
        RpcDisablePlayer();
    }

    [ObserversRpc]
    private void RpcDisablePlayer()
    {
        TogglePlayerComponents(false);
    }

    /// <summary>
    /// Respawns the player at a spawn point.
    /// </summary>
    [Server]
    public void Respawn()
    {
        currentHealth.Value = maxHealth;
        isInvincible.Value = false;

        Vector3 spawnPosition = initialPosition;
        Quaternion spawnRotation = initialRotation;

        if (spawnPoints != null &&
            spawnPoints.Length > 0 &&
            playerIndex.Value >= 0)
        {
            int spawnIndex = Mathf.Min(playerIndex.Value, spawnPoints.Length - 1);
            if (spawnPoints[spawnIndex] != null)
            {
                spawnPosition = spawnPoints[spawnIndex].position;
                spawnRotation = spawnPoints[spawnIndex].rotation;
            }
        }

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        EnablePlayer();
        RpcRespawn(spawnPosition, spawnRotation);
    }

    [Server]
    private void EnablePlayer()
    {
        TogglePlayerComponents(true);
    }

    [ObserversRpc]
    private void RpcRespawn(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        TogglePlayerComponents(true);
    }

    /// <summary>
    /// Enables or disables all gameplay-related components.
    /// </summary>
    private void TogglePlayerComponents(bool enabled)
    {
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = enabled;

        var shooting = GetComponent<PlayerShoot>();
        if (shooting != null) shooting.enabled = enabled;

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = enabled;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = enabled;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    #endregion

    #region Ready System

    /// <summary>
    /// Toggles ready state and sets player name.
    /// </summary>
    [ServerRpc]
    public void SetReadyServerRpc(string name)
    {
        if (string.IsNullOrEmpty(playerName.Value))
        {
            playerName.Value = name;

            if (NetworkGameManager.Instance != null &&
                playerIndex.Value >= 0)
            {
                NetworkGameManager.Instance.SetPlayerName(playerIndex.Value, name);
            }
        }

        isReady.Value = !isReady.Value;

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.CheckAndStartGame();
        }
    }

    [Server]
    public void ResetReady()
    {
        isReady.Value = false;
    }

    #endregion
}
