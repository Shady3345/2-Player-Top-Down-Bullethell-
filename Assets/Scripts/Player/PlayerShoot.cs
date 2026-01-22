using FishNet.Object;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : NetworkBehaviour
{
    [Header("Shooting")]
    [SerializeField] private GameObject playerBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Burst Ability")]
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float burstDelay = 0.1f;
    [SerializeField] private float burstCooldown = 3f;

    [Header("Input")]
    private InputSystem_Actions inputActions;
    private InputAction shootAction;
    private InputAction burstAction;

    private float nextFireTime = 0f;
    private float nextBurstTime = 0f;
    private bool wantsToShoot = false;
    private bool wantsToBurst = false;
    private bool inputInitialized = false;

    private void Awake()
    {
        Debug.Log("PlayerShoot: Awake called");
        inputActions = new InputSystem_Actions();
        shootAction = inputActions.Player.Attack;
        burstAction = inputActions.Player.Burst;
        Debug.Log("PlayerShoot: Input actions initialized");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"PlayerShoot: OnStartClient called - IsOwner: {base.Owner.IsLocalClient}");

        if (IsOwner && !inputInitialized)
        {
            InitializeInput();
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        Debug.Log($"PlayerShoot: OnStartNetwork called - IsOwner: {base.Owner.IsLocalClient}");

        if (base.Owner.IsLocalClient && TimeManager != null)
        {
            TimeManager.OnTick += OnTick;
            Debug.Log("PlayerShoot: OnTick registered");
        }
    }

    private void InitializeInput()
    {
        if (inputInitialized) return;

        inputActions.Enable();

        // Normaler Schuss
        shootAction.performed += OnShootPerformed;
        shootAction.canceled += OnShootCanceled;

        // Burst
        burstAction.performed += OnBurstPerformed;

        inputInitialized = true;
        Debug.Log("PlayerShoot: Input initialized and callbacks registered!");
    }

    private void OnShootPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("PlayerShoot: *** ATTACK PERFORMED! ***");
        wantsToShoot = true;
    }

    private void OnShootCanceled(InputAction.CallbackContext context)
    {
        Debug.Log("PlayerShoot: Attack canceled");
        wantsToShoot = false;
    }

    private void OnBurstPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("PlayerShoot: *** BURST PERFORMED! ***");
        wantsToBurst = true;
    }

    // WICHTIG: OnDisable zum korrekten Cleanup beim Tod
    private void OnDisable()
    {
        Debug.Log($"PlayerShoot: OnDisable called - IsOwner: {IsOwner}");

        if (!IsOwner || !inputInitialized) return;

        // Reset shoot flags
        wantsToShoot = false;
        wantsToBurst = false;

        // Disable Input Actions
        if (inputActions != null)
        {
            inputActions.Disable();
            Debug.Log("PlayerShoot: Input actions disabled");
        }

        // Unregister TimeManager
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            Debug.Log("PlayerShoot: OnTick unregistered");
        }
    }

    // WICHTIG: OnEnable zum Reaktivieren nach Respawn
    private void OnEnable()
    {
        Debug.Log($"PlayerShoot: OnEnable called - IsOwner: {IsOwner}");

        // Nur für Owner und nach der initialen Initialisierung
        if (!IsOwner || !inputInitialized) return;

        // Re-enable Input Actions
        if (inputActions != null)
        {
            inputActions.Enable();
            Debug.Log("PlayerShoot: Input actions re-enabled");
        }

        // Re-register TimeManager
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick; // Safety: remove if already registered
            TimeManager.OnTick += OnTick;
            Debug.Log("PlayerShoot: OnTick re-registered");
        }

        // Reset cooldowns (optional - macht Respawn fairer)
        nextFireTime = 0f;
        nextBurstTime = 0f;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        Debug.Log("PlayerShoot: OnStopNetwork called");

        if (IsOwner && TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnDestroy()
    {
        if (inputInitialized)
        {
            shootAction.performed -= OnShootPerformed;
            shootAction.canceled -= OnShootCanceled;
            burstAction.performed -= OnBurstPerformed;
            inputActions?.Disable();
        }
    }

    private void OnTick()
    {
        if (!IsOwner) return;

        // Nur schießen erlauben wenn das Spiel läuft
        if (NetworkGameManager.Instance != null && !NetworkGameManager.Instance.IsGamePlaying())
        {
            wantsToShoot = false;
            wantsToBurst = false;
            return;
        }

        // Normaler Schuss
        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Debug.Log("PlayerShoot: Shooting!");
            ShootServerRpc();
            nextFireTime = Time.time + fireRate;
        }

        // Burst Schuss
        if (wantsToBurst && Time.time >= nextBurstTime)
        {
            Debug.Log("PlayerShoot: Burst activated!");
            BurstShootServerRpc();
            nextBurstTime = Time.time + burstCooldown;
            wantsToBurst = false;
        }
    }

    [ServerRpc]
    private void ShootServerRpc()
    {
        Debug.Log("PlayerShoot: ShootServerRpc called");

        if (playerBulletPrefab == null)
        {
            Debug.LogError("PlayerShoot: playerBulletPrefab is null!");
            return;
        }

        SpawnBullet();
    }

    [ServerRpc]
    private void BurstShootServerRpc()
    {
        Debug.Log("PlayerShoot: BurstShootServerRpc called");

        if (playerBulletPrefab == null)
        {
            Debug.LogError("PlayerShoot: playerBulletPrefab is null!");
            return;
        }

        StartCoroutine(BurstCoroutine());
    }

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

        Debug.Log("PlayerShoot: Burst completed!");
    }

    private void SpawnBullet()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = Instantiate(playerBulletPrefab, spawnPos, transform.rotation);

        Debug.Log($"PlayerShoot: Bullet instantiated at {spawnPos}");

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.up * bulletSpeed;
            Debug.Log($"PlayerShoot: Bullet velocity set to {transform.up * bulletSpeed}");
        }
        else
        {
            Debug.LogError("PlayerShoot: Bullet has no Rigidbody2D!");
        }

        ServerManager.Spawn(bullet);
        Debug.Log("PlayerShoot: Bullet spawned on network");
    }

    public bool IsBurstReady()
    {
        return Time.time >= nextBurstTime;
    }

    public float GetBurstCooldownRemaining()
    {
        return Mathf.Max(0, nextBurstTime - Time.time);
    }
}