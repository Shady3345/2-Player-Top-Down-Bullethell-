using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private readonly SyncVar<int> currentHealth = new SyncVar<int>();

    [Header("Invincibility")]
    public float invincibilityDuration = 1f;
    private readonly SyncVar<bool> isInvincible = new SyncVar<bool>();
    private float invincibilityTimer = 0f;

    [Header("Respawn")]
    public Transform[] spawnPoints;

    [Header("Ready System")]
    private readonly SyncVar<bool> isReady = new SyncVar<bool>(false);
    private readonly SyncVar<string> playerName = new SyncVar<string>("");

    private readonly SyncVar<int> playerIndex = new SyncVar<int>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // Public Getters
    public bool IsReady => isReady.Value;
    public string PlayerName => playerName.Value;
    public int GetCurrentHealth() => currentHealth.Value;
    public int GetPlayerIndex() => playerIndex.Value;
    public bool IsInvincible() => isInvincible.Value;
    public bool IsAlive() => currentHealth.Value > 0;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        currentHealth.OnChange += OnHealthChanged;

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

    [Server]
    private void RegisterWithGameManager()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("[PlayerStats] NetworkGameManager Instance not found!");
        }
    }

    [Server]
    public void SetPlayerIndex(int index)
    {
        playerIndex.Value = index;
        Debug.Log($"[PlayerStats] Player Index set to: {index}");
    }

    private void Update()
    {
        if (IsServerStarted && isInvincible.Value)
        {
            invincibilityTimer -= Time.deltaTime;

            if (invincibilityTimer <= 0)
            {
                isInvincible.Value = false;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServerStarted) return;
        if (isInvincible.Value) return;
        if (currentHealth.Value <= 0) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        Debug.Log($"[PlayerStats] Player {playerIndex.Value} took {damage} damage! Health: {currentHealth.Value}/{maxHealth}");

        if (currentHealth.Value <= 0)
        {
            Die();
        }
        else
        {
            isInvincible.Value = true;
            invincibilityTimer = invincibilityDuration;
        }
    }

    public void Heal(int amount)
    {
        if (!IsServerStarted) return;
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
    }

    private void OnHealthChanged(int prev, int next, bool asServer)
    {
        Debug.Log($"[PlayerStats] Player {playerIndex.Value} health changed: {prev} -> {next} (IsServer: {asServer})");

        // ← NEU: Benachrichtige GameManager über Health-Änderung
        if (NetworkGameManager.Instance != null && playerIndex.Value >= 0)
        {
            NetworkGameManager.Instance.OnPlayerHealthChanged(playerIndex.Value, next);
        }

        if (IsClientStarted)
        {
            OnHealthChangedClient(prev, next);
        }
    }

    private void OnHealthChangedClient(int prev, int next)
    {
        if (next < prev)
        {
            Debug.Log("[PlayerStats] Visual: Player took damage!");
        }
        else if (next > prev)
        {
            Debug.Log("[PlayerStats] Visual: Player healed!");
        }
    }

    private void Die()
    {
        if (!IsServerStarted) return;
        Debug.Log($"[PlayerStats] Player {playerIndex.Value} died!");

        DisablePlayer();
        NotifyEnemiesOfDeath();

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.CheckGameOver();
        }
    }

    [Server]
    private void NotifyEnemiesOfDeath()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.OnPlayerDied(gameObject);
            }
        }
    }

    [Server]
    private void DisablePlayer()
    {
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        var shooting = GetComponent<PlayerShoot>();
        if (shooting != null) shooting.enabled = false;

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        RpcDisablePlayer();
    }

    [ObserversRpc]
    private void RpcDisablePlayer()
    {
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        var shooting = GetComponent<PlayerShoot>();
        if (shooting != null) shooting.enabled = false;

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    [Server]
    public void Respawn()
    {
        Debug.Log($"[PlayerStats] Respawning Player {playerIndex.Value}");

        currentHealth.Value = maxHealth;
        isInvincible.Value = false;

        Vector3 spawnPosition = initialPosition;
        Quaternion spawnRotation = initialRotation;

        if (spawnPoints != null && spawnPoints.Length > 0 && playerIndex.Value >= 0)
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
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = true;

        var shooting = GetComponent<PlayerShoot>();
        if (shooting != null) shooting.enabled = true;

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    [ObserversRpc]
    private void RpcRespawn(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = true;

        var shooting = GetComponent<PlayerShoot>();
        if (shooting != null) shooting.enabled = true;

        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    #region Ready System

    [ServerRpc]
    public void SetReadyServerRpc(string name)
    {
        if (string.IsNullOrEmpty(playerName.Value))
        {
            playerName.Value = name;

            if (NetworkGameManager.Instance != null && playerIndex.Value >= 0)
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