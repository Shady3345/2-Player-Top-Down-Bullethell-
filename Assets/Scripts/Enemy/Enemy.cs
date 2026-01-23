using FishNet.Object;
using UnityEngine;

/// <summary>
/// Networked enemy behaviour:
/// - Tracks and targets players
/// - Moves and rotates toward them
/// - Shoots bullets
/// - Handles damage, death, and scoring
/// </summary>
public class Enemy : NetworkBehaviour
{
    #region Enemy Stats

    [Header("Enemy Stats")]
    public int health = 3;
    public float moveSpeed = 2f;
    public int scoreValue = 10;

    #endregion

    #region Shooting

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 2f;
    public float bulletSpeed = 5f;

    #endregion

    #region Behavior

    [Header("Behavior")]
    public bool trackPlayer = true;
    public bool moveTowardsPlayer = false;
    public float stopDistance = 5f;

    #endregion

    // Current target player
    private PlayerStats targetPlayer;

    // Shooting timing
    private float nextFireTime = 0f;

    // Retargeting timing
    private float retargetInterval = 2f;
    private float nextRetargetTime = 0f;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Select an initial target when spawned on the server
        FindClosestPlayer();
    }

    private void Update()
    {
        // Enemy logic runs only on the server
        if (!IsServerStarted) return;

        // Periodically re-evaluate the closest target
        if (Time.time >= nextRetargetTime)
        {
            FindClosestPlayer();
            nextRetargetTime = Time.time + retargetInterval;
        }

        // Stop processing if there is no valid target
        if (targetPlayer == null || !targetPlayer.IsAlive())
        {
            targetPlayer = null;
            return;
        }

        HandleMovement();
        HandleRotation();
        HandleShooting();
    }

    /// <summary>
    /// Finds the closest alive player and assigns it as the target.
    /// </summary>
    private void FindClosestPlayer()
    {
        if (NetworkGameManager.Instance == null)
        {
            targetPlayer = null;
            return;
        }

        var allPlayers = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        float closestDistance = float.MaxValue;
        PlayerStats newTarget = null;

        foreach (var player in allPlayers)
        {
            // Ignore invalid or dead players
            if (player == null || !player.IsAlive())
                continue;

            float distance = Vector2.Distance(
                transform.position,
                player.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                newTarget = player;
            }
        }

        targetPlayer = newTarget;
    }

    /// <summary>
    /// Called by PlayerStats when a player dies.
    /// Forces retargeting if the current target died.
    /// </summary>
    [Server]
    public void OnPlayerDied(GameObject deadPlayer)
    {
        if (targetPlayer != null && targetPlayer.gameObject == deadPlayer)
        {
            targetPlayer = null;
            FindClosestPlayer();
        }
    }

    /// <summary>
    /// Moves toward the target player until within stop distance.
    /// </summary>
    private void HandleMovement()
    {
        if (!moveTowardsPlayer || targetPlayer == null) return;

        float distanceToPlayer = Vector2.Distance(
            transform.position,
            targetPlayer.transform.position
        );

        if (distanceToPlayer > stopDistance)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPlayer.transform.position,
                moveSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Rotates the enemy to face the target player.
    /// </summary>
    private void HandleRotation()
    {
        if (!trackPlayer || targetPlayer == null) return;

        Vector2 direction = targetPlayer.transform.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// Handles firing logic based on fire rate.
    /// </summary>
    private void HandleShooting()
    {
        if (targetPlayer == null) return;

        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }

    /// <summary>
    /// Spawns and launches a networked bullet toward the target.
    /// </summary>
    private void Shoot()
    {
        if (bulletPrefab == null || targetPlayer == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position;

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        ServerManager.Spawn(bullet);

        Vector2 direction = (targetPlayer.transform.position - spawnPos).normalized;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * bulletSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// Applies damage to the enemy (server-only).
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!IsServerStarted) return;

        health -= damage;
        if (health <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Handles enemy death, scoring, and despawning.
    /// </summary>
    private void Die()
    {
        if (!IsServerStarted) return;

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.EnemyKilled(scoreValue);
        }

        ServerManager.Despawn(gameObject);
    }

    /// <summary>
    /// Handles collisions with player bullets and players.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerStarted) return;

        // Hit by player bullet
        if (other.CompareTag("PlayerBullet"))
        {
            TakeDamage(1);
            ServerManager.Despawn(other.gameObject);
        }
        // Collides with player
        else if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats != null && stats.IsAlive())
            {
                int playerIndex = stats.GetPlayerIndex();

                if (NetworkGameManager.Instance != null)
                {
                    NetworkGameManager.Instance.DamagePlayer(playerIndex, 10);
                }
            }
        }
    }
}
