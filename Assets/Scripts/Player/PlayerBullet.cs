using FishNet.Object;
using UnityEngine;

/// <summary>
/// Networked player bullet:
/// - Exists under server authority
/// - Damages enemies on collision
/// - Despawns after a lifetime or when off-screen
/// </summary>
public class PlayerBullet : NetworkBehaviour
{
    // Damage dealt to enemies
    public int damage = 1;

    // Time in seconds before the bullet is automatically destroyed
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
    /// Handles collision with enemies.
    /// Applies damage and despawns the bullet.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Collision logic runs only on the server
        if (!IsServerStarted) return;

        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            // Remove bullet after hitting an enemy
            ServerManager.Despawn(gameObject);
        }
    }

    /// <summary>
    /// Called when the bullet leaves the camera view.
    /// Used as a safety cleanup to avoid leftover bullets.
    /// </summary>
    private void OnBecameInvisible()
    {
        if (IsServerStarted)
        {
            ServerManager.Despawn(gameObject);
        }
    }
}
