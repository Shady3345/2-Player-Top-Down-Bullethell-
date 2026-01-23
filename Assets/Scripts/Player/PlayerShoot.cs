using FishNet.Object;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player shooting logic:
/// - Automatic continuous fire
/// - Burst fire ability with cooldown
/// - Input handled only by owning client
/// - Bullet spawning is server-authoritative
/// </summary>
public class PlayerShoot : NetworkBehaviour
{
    #region Shooting Settings

    [Header("Shooting")]
    [SerializeField] private GameObject playerBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;

    #endregion

    #region Burst Ability

    [Header("Burst Ability")]
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float burstDelay = 0.1f;
    [SerializeField] private float burstCooldown = 3f;

    #endregion

    #region Input

    [Header("Input")]
    private InputSystem_Actions inputActions;
    private InputAction burstAction;

    #endregion

    // Timing control
    private float nextFireTime = 0f;
    private float nextBurstTime = 0f;

    // Input state
    private bool wantsToBurst = false;
    private bool inputInitialized = false;

    private void Awake()
    {
        // Initialize input actions
        inputActions = new InputSystem_Actions();
        burstAction = inputActions.Player.Burst;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Only the owning client initializes input
        if (IsOwner && !inputInitialized)
        {
            InitializeInput();
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // Subscribe to network tick on local owner
        if (base.Owner.IsLocalClient && TimeManager != null)
        {
            TimeManager.OnTick += OnTick;
        }
    }

    /// <summary>
    /// Enables input and registers callbacks.
    /// </summary>
    private void InitializeInput()
    {
        if (inputInitialized) return;

        inputActions.Enable();
        burstAction.performed += OnBurstPerformed;

        inputInitialized = true;
    }

    /// <summary>
    /// Called when burst input is performed.
    /// Sets a flag to trigger burst on the next tick.
    /// </summary>
    private void OnBurstPerformed(InputAction.CallbackContext context)
    {
        wantsToBurst = true;
    }

    private void OnEnable()
    {
        if (!IsOwner || !inputInitialized) return;

        // Re-enable input when object becomes active
        inputActions.Enable();

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }

        // Reset cooldowns on respawn for fairness
        nextFireTime = 0f;
        nextBurstTime = 0f;
    }

    private void OnDisable()
    {
        if (!IsOwner || !inputInitialized) return;

        wantsToBurst = false;

        // Disable input when object is disabled
        inputActions.Disable();

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        // Clean up tick subscription
        if (IsOwner && TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnDestroy()
    {
        // Clean up input callbacks
        if (inputInitialized)
        {
            burstAction.performed -= OnBurstPerformed;
            inputActions?.Disable();
        }
    }

    /// <summary>
    /// Called every network tick for the owning client.
    /// Handles firing logic and cooldowns.
    /// </summary>
    private void OnTick()
    {
        if (!IsOwner) return;

        // Do not allow shooting if the game is not running
        if (NetworkGameManager.Instance != null &&
            !NetworkGameManager.Instance.IsGamePlaying())
        {
            wantsToBurst = false;
            return;
        }

        // Automatic continuous fire
        if (Time.time >= nextFireTime)
        {
            ShootServerRpc();
            nextFireTime = Time.time + fireRate;
        }

        // Burst fire ability
        if (wantsToBurst && Time.time >= nextBurstTime)
        {
            BurstShootServerRpc();
            nextBurstTime = Time.time + burstCooldown;
            wantsToBurst = false;
        }
    }

    /// <summary>
    /// Requests the server to spawn a single bullet.
    /// </summary>
    [ServerRpc]
    private void ShootServerRpc()
    {
        if (playerBulletPrefab == null) return;
        SpawnBullet();
    }

    /// <summary>
    /// Requests the server to start a burst firing coroutine.
    /// </summary>
    [ServerRpc]
    private void BurstShootServerRpc()
    {
        if (playerBulletPrefab == null) return;
        StartCoroutine(BurstCoroutine());
    }

    /// <summary>
    /// Spawns multiple bullets with a delay between each.
    /// Runs on the server.
    /// </summary>
    private IEnumerator BurstCoroutine()
    {
        for (int i = 0; i < burstCount; i++)
        {
            SpawnBullet();

            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstDelay);
            }
        }
    }

    /// <summary>
    /// Instantiates and launches a bullet in the forward direction.
    /// </summary>
    private void SpawnBullet()
    {
        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position;

        GameObject bullet =
            Instantiate(playerBulletPrefab, spawnPos, transform.rotation);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.up * bulletSpeed;
        }

        ServerManager.Spawn(bullet);
    }

    #region Public Helpers

    /// <summary>
    /// Returns whether the burst ability is ready.
    /// </summary>
    public bool IsBurstReady()
    {
        return Time.time >= nextBurstTime;
    }

    /// <summary>
    /// Returns remaining burst cooldown time.
    /// </summary>
    public float GetBurstCooldownRemaining()
    {
        return Mathf.Max(0, nextBurstTime - Time.time);
    }

    #endregion
}
