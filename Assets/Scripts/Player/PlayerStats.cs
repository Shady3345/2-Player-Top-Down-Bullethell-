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
    private readonly SyncVar<bool> isInvincible = new SyncVar<bool>(); // <- Synchronisiert!
    private float invincibilityTimer = 0f;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        currentHealth.OnChange += OnHealthChanged;

        if (IsServerStarted)
        {
            currentHealth.Value = maxHealth;
            isInvincible.Value = false;
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        currentHealth.OnChange -= OnHealthChanged;
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
        if (isInvincible.Value) return; // <- Jetzt synchronisiert

        currentHealth.Value -= damage;
        Debug.Log($"Player took damage! Health: {currentHealth.Value}");

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
        Debug.Log($"Health changed: {prev} -> {next}");
    }

    private void Die()
    {
        if (!IsServerStarted) return;
        Debug.Log("Player died!");

        ServerManager.Despawn(gameObject);
    }
}