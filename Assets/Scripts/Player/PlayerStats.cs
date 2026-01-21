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

    private int playerIndex = -1;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        currentHealth.OnChange += OnHealthChanged;

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

        ServerManager.Despawn(gameObject);
    }

    public int GetCurrentHealth() => currentHealth.Value;
    public int GetPlayerIndex() => playerIndex;
    public bool IsInvincible() => isInvincible.Value;
}