using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : NetworkBehaviour
{
    [Header("Shooting")]
    [SerializeField] private GameObject playerBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Input")]
    private InputSystem_Actions inputActions;
    private InputAction shootAction;
    private float nextFireTime = 0f;
    private bool wantsToShoot = false;
    private bool inputInitialized = false;

    private void Awake()
    {
        Debug.Log("PlayerShoot: Awake called");
        inputActions = new InputSystem_Actions();
        shootAction = inputActions.Player.Attack;
        Debug.Log("PlayerShoot: Input actions initialized");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"PlayerShoot: OnStartClient called - IsOwner: {base.Owner.IsLocalClient}");

        // Input nur für den Owner initialisieren
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
        shootAction.performed += OnShootPerformed;
        shootAction.canceled += OnShootCanceled;

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
            inputActions?.Disable();
        }
    }

    private void OnTick()
    {
        if (!IsOwner) return;

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Debug.Log("PlayerShoot: Shooting!");
            ShootServerRpc();
            nextFireTime = Time.time + fireRate;
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

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = Instantiate(playerBulletPrefab, spawnPos, transform.rotation);

        Debug.Log($"PlayerShoot: Bullet instantiated at {spawnPos}");

        ServerManager.Spawn(bullet);
        Debug.Log("PlayerShoot: Bullet spawned on network");

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
    }
}