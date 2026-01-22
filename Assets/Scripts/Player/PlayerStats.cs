using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [Header("Health")]
    public int maxHealth = 10;
    private readonly SyncVar<int> currentHealth = new SyncVar<int>();

    [Header("Invincibility")]
    public float invincibilityDuration = 1f;
    private readonly SyncVar<bool> isInvincible = new SyncVar<bool>();
    private float invincibilityTimer = 0f;

    [Header("Respawn")]
    public Transform[] spawnPoints; // Assign in Inspector
    public float respawnDelay = 2f;

    private int playerIndex = -1;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        currentHealth.OnChange += OnHealthChanged;

        // Speichere initiale Position
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (IsServerStarted)
        {
            currentHealth.Value = maxHealth;
            isInvincible.Value = false;
        }

        // Bestimme Spieler-Index
        DeterminePlayerIndex();
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        currentHealth.OnChange -= OnHealthChanged;
    }

    private void DeterminePlayerIndex()
    {
        // Versuche PlayerMovement zu finden falls vorhanden
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == movement)
                {
                    playerIndex = i;
                    Debug.Log($"Player Index determined: {playerIndex}");
                    break;
                }
            }
        }
        else
        {
            // Fallback: Zähle PlayerStats
            var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == this)
                {
                    playerIndex = i;
                    Debug.Log($"Player Index determined (fallback): {playerIndex}");
                    break;
                }
            }
        }
    }

    private void Update()
    {
        // Invincibility Timer läuft auf dem Server
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

        currentHealth.Value -= damage;
        Debug.Log($"Player {playerIndex} took damage! Health: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            Die();
        }
        else
        {
            // Start invincibility auf dem Server
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
        Debug.Log($"Player {playerIndex} health changed: {prev} -> {next}");

        // Synchronisiere mit NetworkGameManager
        if (IsServerStarted && playerIndex >= 0 && NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SetPlayerHealth(playerIndex, next);
        }

        // Visual Feedback für Clients
        if (IsClientStarted)
        {
            OnHealthChangedClient(prev, next);
        }
    }

    private void OnHealthChangedClient(int prev, int next)
    {
        // Hier kannst du visuelle Effekte hinzufügen:
        // - Roten Flash bei Damage
        // - Screen shake
        // - Particle effects
        // - Sound effects

        if (next < prev)
        {
            // Damage genommen
            Debug.Log("Visual: Player took damage!");
            // Beispiel: GetComponent<SpriteRenderer>()?.color = Color.red;
        }
        else if (next > prev)
        {
            // Geheilt
            Debug.Log("Visual: Player healed!");
        }
    }

    private void Die()
    {
        if (!IsServerStarted) return;
        Debug.Log($"Player {playerIndex} died!");

        // Setze Health auf 0 im NetworkGameManager
        if (NetworkGameManager.Instance != null && playerIndex >= 0)
        {
            NetworkGameManager.Instance.SetPlayerHealth(playerIndex, 0);
        }

        // Disable Player statt Despawn (damit er im GameOver Screen bleibt)
        DisablePlayer();
    }

    [Server]
    private void DisablePlayer()
    {
        // Disable Movement
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        // Disable Collider
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        // Disable Renderer (optional - macht Spieler unsichtbar)
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = false;

        RpcDisablePlayer();
    }

    [ObserversRpc]
    private void RpcDisablePlayer()
    {
        // Client-side disable effects
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;
    }

    [Server]
    public void Respawn()
    {
        Debug.Log($"Respawning Player {playerIndex}");

        // Reset Health
        currentHealth.Value = maxHealth;
        isInvincible.Value = false;

        // Bestimme Spawn Position
        Vector3 spawnPosition = initialPosition;
        Quaternion spawnRotation = initialRotation;

        if (spawnPoints != null && spawnPoints.Length > 0 && playerIndex >= 0)
        {
            int spawnIndex = Mathf.Min(playerIndex, spawnPoints.Length - 1);
            if (spawnPoints[spawnIndex] != null)
            {
                spawnPosition = spawnPoints[spawnIndex].position;
                spawnRotation = spawnPoints[spawnIndex].rotation;
            }
        }

        // Teleportiere Spieler
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        // Enable Player Components
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = true;

        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = true;

        // Reset Rigidbody velocity
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
        // Client-side respawn effects
        transform.position = position;
        transform.rotation = rotation;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = true;

        // Reset Rigidbody auf Client
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Debug.Log($"Client: Player {playerIndex} respawned");
    }

    public int GetCurrentHealth() => currentHealth.Value;
    public int GetPlayerIndex() => playerIndex;
    public bool IsInvincible() => isInvincible.Value;
    public bool IsAlive() => currentHealth.Value > 0;
}