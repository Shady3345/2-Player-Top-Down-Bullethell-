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
    public float respawnDelay = 2f;

    // ← GEÄNDERT: PlayerIndex ist jetzt ein SyncVar für zuverlässige Synchronisation
    private readonly SyncVar<int> playerIndex = new SyncVar<int>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        currentHealth.OnChange += OnHealthChanged;
        playerIndex.OnChange += OnPlayerIndexChanged; // ← NEU: Listener für Index-Änderung

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (IsServerStarted)
        {
            currentHealth.Value = maxHealth;
            isInvincible.Value = false;

            // ← GEÄNDERT: Registriere beim NetworkGameManager statt selbst zu bestimmen
            RegisterWithGameManager();
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        currentHealth.OnChange -= OnHealthChanged;
        playerIndex.OnChange -= OnPlayerIndexChanged;
    }

    // ← NEU: Registrierung beim GameManager
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

    // ← NEU: Wird vom NetworkGameManager aufgerufen
    [Server]
    public void SetPlayerIndex(int index)
    {
        playerIndex.Value = index;
        Debug.Log($"[PlayerStats] Player Index set to: {index}, ObjectId: {ObjectId}");

        // Synchronisiere initial Health mit GameManager
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SetPlayerHealth(index, currentHealth.Value);
        }
    }

    // ← NEU: Callback wenn Index sich ändert (auch auf Clients)
    private void OnPlayerIndexChanged(int oldVal, int newVal, bool asServer)
    {
        Debug.Log($"[PlayerStats] PlayerIndex changed: {oldVal} -> {newVal} (IsServer: {asServer}, IsClient: {IsClientStarted})");
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
        Debug.Log($"[PlayerStats] Player {playerIndex.Value} took {damage} damage! Health: {currentHealth.Value}");

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

        if (IsServerStarted && playerIndex.Value >= 0 && NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SetPlayerHealth(playerIndex.Value, next);
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

        if (NetworkGameManager.Instance != null && playerIndex.Value >= 0)
        {
            NetworkGameManager.Instance.SetPlayerHealth(playerIndex.Value, 0);
        }

        DisablePlayer();
        NotifyEnemiesOfDeath();
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
        Debug.Log($"[PlayerStats] Server: DisablePlayer called for Player {playerIndex.Value}");

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
        Debug.Log($"[PlayerStats] Client: RpcDisablePlayer called for Player {playerIndex.Value}");

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
        Debug.Log($"[PlayerStats] Server: Respawning Player {playerIndex.Value}");

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

        RpcRespawn(spawnPosition, spawnRotation);
    }

    [ObserversRpc]
    private void RpcRespawn(Vector3 position, Quaternion rotation)
    {
        Debug.Log($"[PlayerStats] Client: RpcRespawn called for Player {playerIndex.Value}");

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

    public int GetCurrentHealth() => currentHealth.Value;
    public int GetPlayerIndex() => playerIndex.Value;
    public bool IsInvincible() => isInvincible.Value;
    public bool IsAlive() => currentHealth.Value > 0;
}