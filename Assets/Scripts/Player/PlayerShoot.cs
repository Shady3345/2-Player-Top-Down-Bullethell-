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
    private InputAction burstAction;

    private float nextFireTime = 0f;
    private float nextBurstTime = 0f;
    private bool wantsToBurst = false;
    private bool inputInitialized = false;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        burstAction = inputActions.Player.Burst;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner && !inputInitialized)
        {
            InitializeInput();
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (base.Owner.IsLocalClient && TimeManager != null)
        {
            TimeManager.OnTick += OnTick;
        }
    }

    private void InitializeInput()
    {
        if (inputInitialized) return;

        inputActions.Enable();
        burstAction.performed += OnBurstPerformed;

        inputInitialized = true;
    }

    private void OnBurstPerformed(InputAction.CallbackContext context)
    {
        wantsToBurst = true;
    }

    private void OnDisable()
    {
        if (!IsOwner || !inputInitialized) return;

        wantsToBurst = false;

        if (inputActions != null)
        {
            inputActions.Disable();
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnEnable()
    {
        if (!IsOwner || !inputInitialized) return;

        if (inputActions != null)
        {
            inputActions.Enable();
        }

        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnTick += OnTick;
        }

        // Reset cooldowns on respawn for fairness
        nextFireTime = 0f;
        nextBurstTime = 0f;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        if (IsOwner && TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnDestroy()
    {
        if (inputInitialized)
        {
            burstAction.performed -= OnBurstPerformed;
            inputActions?.Disable();
        }
    }

    private void OnTick()
    {
        if (!IsOwner) return;

        // Only allow shooting when game is playing
        if (NetworkGameManager.Instance != null && !NetworkGameManager.Instance.IsGamePlaying())
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

        // Burst shot
        if (wantsToBurst && Time.time >= nextBurstTime)
        {
            BurstShootServerRpc();
            nextBurstTime = Time.time + burstCooldown;
            wantsToBurst = false;
        }
    }

    [ServerRpc]
    private void ShootServerRpc()
    {
        if (playerBulletPrefab == null) return;

        SpawnBullet();
    }

    [ServerRpc]
    private void BurstShootServerRpc()
    {
        if (playerBulletPrefab == null) return;

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
    }

    private void SpawnBullet()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = Instantiate(playerBulletPrefab, spawnPos, transform.rotation);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.up * bulletSpeed;
        }

        ServerManager.Spawn(bullet);
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