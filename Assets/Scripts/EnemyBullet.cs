using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public int damage = 1;
    public float lifetime = 5f; // Destroy after 5 seconds if it doesn't hit anything

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hit the player
        if (other.CompareTag("Player"))
        {
            //PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
         //   if (playerHealth != null)
            {
         //       playerHealth.TakeDamage(damage);
            }
            Destroy(gameObject);
        }

        // Destroy if hit walls/obstacles (optional)
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    void OnBecameInvisible()
    {
        // Destroy bullet when it goes off screen
        Destroy(gameObject);
    }
}