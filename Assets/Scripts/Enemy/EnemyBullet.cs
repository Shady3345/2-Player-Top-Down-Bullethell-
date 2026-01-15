using FishNet.Object;
using UnityEngine;

public class EnemyBullet : NetworkBehaviour
{
    public int damage = 1;
    public float lifetime = 5f;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Invoke(nameof(DestroyBullet), lifetime);
    }

    private void DestroyBullet()
    {
        if (IsServerStarted)
        {
            ServerManager.Despawn(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerStarted) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(damage);
            }
            ServerManager.Despawn(gameObject);
        }

        if (other.CompareTag("Wall"))
        {
            ServerManager.Despawn(gameObject);
        }
    }

    private void OnBecameInvisible()
    {
        if (IsServerStarted)
        {
            ServerManager.Despawn(gameObject);
        }
    }
}
