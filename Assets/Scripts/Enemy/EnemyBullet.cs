using FishNet.Object;
using UnityEngine;

/// <summary>
/// Networked enemy bullet:
/// - Exists only under server authority
/// - Damages players on collision
/// - Despawns after a lifetime or when off-screen
/// </summary>
public class EnemyBullet : NetworkBehaviour
{
    // Damage dealt to the player on hit
    public int damage = 1;

    // Maximum lifetime before auto-despawn
    public float lifetime = 5f;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Automatically destroy the bullet after its lifetime
        Invoke(nameof(DestroyBullet), lifetime);
    }

    /// <summary>
    /// Despawns the bullet on the server.
    /// </summary>
    private void DestroyBullet()
    {
        if (IsServerStarted)
        {
            ServerManager.Despawn(gameObject);
        }
    }

    /// <summary>
    /// Handles collision with the player.
    /// Applies damage and despawns the bullet.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Collision logic must run only on the server
        if (!IsServerStarted) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();

            if (playerStats != null)
            {
                playerStats.TakeDamage(damage);
            }

            // Remove bullet after hitting a player
            ServerManager.Despawn(gameObject);
        }
    }

    /// <summary>
    /// Called by Unity when the bullet leaves the camera view.
    /// Used as a safety cleanup to prevent lingering bullets.
    /// </summary>
    private void OnBecameInvisible()
    {
        if (IsServerStarted)
        {
            ServerManager.Despawn(gameObject);
        }
    }
}
