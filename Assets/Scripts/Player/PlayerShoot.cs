using FishNet.Example.ColliderRollbacks;
using FishNet.Object;
using Unity.VisualScripting;
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
    private InputSystem_Actions inputActions; // <- Korrigierter Typ

    private InputAction shootAction;
    private float nextFireTime = 0f;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        shootAction = inputActions.Player.Attack; // "Attack" ist deine Schießaktion laut der .inputactions
        if( inputActions == null)
        {
            Debug.LogError("InputActions is null in PlayerShoot");
        }
    }

    private void OnEnable()
    {
        if (IsOwner)
        {
            inputActions?.Enable();
        }
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            inputActions?.Disable();
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

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        if (IsOwner && TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
    }

    private void OnTick()
    {
        if (!IsOwner) return;

        if (shootAction.IsPressed() && Time.time >= nextFireTime)
        {
            ShootServerRpc();
            nextFireTime = Time.time + fireRate;
        }
    }

    [ServerRpc]
    private void ShootServerRpc()
    {
        if (playerBulletPrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = Instantiate(playerBulletPrefab, spawnPos, transform.rotation);

        ServerManager.Spawn(bullet);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.up * bulletSpeed;
        }
    }
}