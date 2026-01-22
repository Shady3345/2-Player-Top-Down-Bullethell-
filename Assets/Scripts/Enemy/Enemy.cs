using FishNet.Object;
using UnityEngine;

public class Enemy : NetworkBehaviour
{
    [Header("Enemy Stats")]
    public int health = 3;
    public float moveSpeed = 2f;
    public int scoreValue = 10;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 2f;
    public float bulletSpeed = 5f;

    [Header("Behavior")]
    public bool trackPlayer = true;
    public bool moveTowardsPlayer = false;
    public float stopDistance = 5f;

    private Transform targetPlayer;
    private float nextFireTime = 0f;
    private float retargetInterval = 2f; // ← NEU: Re-target alle 2 Sekunden
    private float nextRetargetTime = 0f;

    public override void OnStartServer()
    {
        base.OnStartServer();
        FindClosestPlayer();
    }

    private void Update()
    {
        if (!IsServerStarted) return;

        // ← NEU: Periodisches Re-Targeting
        if (Time.time >= nextRetargetTime)
        {
            FindClosestPlayer();
            nextRetargetTime = Time.time + retargetInterval;
        }

        if (targetPlayer == null)
        {
            FindClosestPlayer();
            return;
        }

        HandleMovement();
        HandleRotation();
        HandleShooting();
    }

    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = float.MaxValue;
        Transform newTarget = null;

        foreach (GameObject playerObj in players)
        {
            // ← NEU: Prüfe ob Spieler lebt
            PlayerStats stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null && !stats.IsAlive())
            {
                continue; // Überspringe tote Spieler
            }

            float distance = Vector2.Distance(transform.position, playerObj.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                newTarget = playerObj.transform;
            }
        }

        // Nur Target ändern wenn neues gefunden wurde
        if (newTarget != null)
        {
            if (targetPlayer != newTarget)
            {
                Debug.Log($"[Enemy] Switching target to {newTarget.name}");
            }
            targetPlayer = newTarget;
        }
        else
        {
            // Kein lebender Spieler gefunden
            targetPlayer = null;
        }
    }

    // ← NEU: Wird von PlayerStats aufgerufen wenn Spieler stirbt
    [Server]
    public void OnPlayerDied(GameObject deadPlayer)
    {
        if (targetPlayer != null && targetPlayer.gameObject == deadPlayer)
        {
            Debug.Log($"[Enemy] Current target died, finding new target...");
            targetPlayer = null;
            FindClosestPlayer();
        }
    }

    private void HandleMovement()
    {
        if (!moveTowardsPlayer || targetPlayer == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, targetPlayer.position);
        if (distanceToPlayer > stopDistance)
        {
            Vector2 direction = (targetPlayer.position - transform.position).normalized;
            transform.position = Vector2.MoveTowards(transform.position, targetPlayer.position, moveSpeed * Time.deltaTime);
        }
    }

    private void HandleRotation()
    {
        if (!trackPlayer || targetPlayer == null) return;

        Vector2 direction = targetPlayer.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void HandleShooting()
    {
        if (targetPlayer == null) return; // ← NEU: Nicht schießen ohne Ziel

        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || targetPlayer == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        ServerManager.Spawn(bullet);

        Vector2 direction = (targetPlayer.position - spawnPos).normalized;
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * bulletSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        bullet.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void TakeDamage(int damage)
    {
        if (!IsServerStarted) return;

        health -= damage;
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsServerStarted)
        {
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.EnemyKilled(scoreValue);
            }

            ServerManager.Despawn(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerStarted) return;

        if (other.CompareTag("PlayerBullet"))
        {
            TakeDamage(1);
            ServerManager.Despawn(other.gameObject);
        }
        else if (other.CompareTag("Player"))
        {
            // ← VERBESSERT: Nutze PlayerStats für korrekten Index
            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats != null && stats.IsAlive() && NetworkGameManager.Instance != null)
            {
                int playerIndex = stats.GetPlayerIndex();
                Debug.Log($"[Enemy] Collision with Player {playerIndex}");
                NetworkGameManager.Instance.DamagePlayer(playerIndex, 10);
            }
        }
    }
}