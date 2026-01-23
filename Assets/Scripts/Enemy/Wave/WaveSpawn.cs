using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles enemy wave spawning on the server:
/// - Manages wave progression
/// - Spawns enemies with weighted randomness
/// - Tracks alive enemies
/// - Notifies clients about wave start/end
/// </summary>
public class WaveSpawn : NetworkBehaviour
{
    /// <summary>
    /// Defines a spawnable enemy type with weight and score value.
    /// </summary>
    [System.Serializable]
    public class EnemyType
    {
        public GameObject enemyPrefab;
        public string enemyName;
        public int spawnWeight = 1;
        public int scoreValue = 10;
    }

    #region Inspector Settings

    [Header("Enemy Types")]
    public EnemyType[] enemyTypes;

    [Header("Spawn Settings")]
    public Transform[] spawnPoints;
    public float spawnRadius = 2f;

    [Header("Wave Settings")]
    public int startingEnemiesPerWave = 5;
    public float enemyIncreasePerWave = 2f;
    public float timeBetweenWaves = 10f;
    public float timeBetweenSpawns = 0.5f;

    #endregion

    #region Networked State

    // Current wave index (synced)
    private readonly SyncVar<int> currentWave = new SyncVar<int>(0);

    // Number of enemies currently alive (synced)
    private readonly SyncVar<int> enemiesAlive = new SyncVar<int>(0);

    // Whether a wave is currently active (synced)
    private readonly SyncVar<bool> waveInProgress = new SyncVar<bool>(false);

    #endregion

    // List of currently spawned enemies (server-side only)
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    // Prevents starting multiple wave coroutines
    private bool isSpawning = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Wave spawning is started via Update when the game begins
    }

    private void Update()
    {
        // Only the server controls wave spawning
        if (!IsServerStarted || isSpawning || NetworkGameManager.Instance == null)
            return;

        // Start waves only when the game is in Playing state
        if (NetworkGameManager.Instance.IsGamePlaying())
        {
            isSpawning = true;
            StartCoroutine(WaveCoroutine());
        }
    }

    /// <summary>
    /// Main wave loop coroutine.
    /// Spawns waves while the game is active.
    /// </summary>
    private IEnumerator WaveCoroutine()
    {
        // Small delay before first wave
        yield return new WaitForSeconds(2f);

        while (NetworkGameManager.Instance != null &&
               NetworkGameManager.Instance.IsGamePlaying())
        {
            // Start next wave
            currentWave.Value++;
            waveInProgress.Value = true;

            // Update GameManager with current wave
            NetworkGameManager.Instance.SetCurrentWave(currentWave.Value);

            // Notify all clients
            RpcAnnounceWave(currentWave.Value);

            // Calculate number of enemies for this wave
            int enemiesToSpawn = Mathf.RoundToInt(
                startingEnemiesPerWave +
                (currentWave.Value - 1) * enemyIncreasePerWave
            );

            // Spawn enemies for this wave
            yield return StartCoroutine(SpawnWave(enemiesToSpawn));

            // Wait until all enemies are dead
            while (enemiesAlive.Value > 0)
            {
                // Clean up destroyed enemies
                spawnedEnemies.RemoveAll(enemy => enemy == null);
                enemiesAlive.Value = spawnedEnemies.Count;

                yield return new WaitForSeconds(0.5f);
            }

            // Wave finished
            waveInProgress.Value = false;
            RpcAnnounceWaveComplete(currentWave.Value);

            // Pause before next wave
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        isSpawning = false;
    }

    /// <summary>
    /// Spawns a single wave of enemies over time.
    /// </summary>
    private IEnumerator SpawnWave(int enemyCount)
    {
        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }
    }

    /// <summary>
    /// Spawns one enemy at a random spawn point and position.
    /// </summary>
    private void SpawnEnemy()
    {
        if (enemyTypes.Length == 0 || spawnPoints.Length == 0)
            return;

        // Pick random spawn point
        Transform spawnPoint =
            spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Add random offset inside spawn radius
        Vector3 spawnPosition =
            spawnPoint.position +
            (Vector3)Random.insideUnitCircle * spawnRadius;

        // Select enemy type by weight
        EnemyType selectedType = GetRandomEnemyType();
        if (selectedType?.enemyPrefab == null)
            return;

        // Spawn enemy on server
        GameObject enemy =
            Instantiate(selectedType.enemyPrefab, spawnPosition, Quaternion.identity);

        ServerManager.Spawn(enemy);

        // Apply score value to enemy
        Enemy enemyScript = enemy.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.scoreValue = selectedType.scoreValue;
        }

        spawnedEnemies.Add(enemy);
        enemiesAlive.Value++;
    }

    /// <summary>
    /// Selects an enemy type using weighted randomness.
    /// </summary>
    private EnemyType GetRandomEnemyType()
    {
        int totalWeight = 0;
        foreach (var type in enemyTypes)
            totalWeight += type.spawnWeight;

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var type in enemyTypes)
        {
            currentWeight += type.spawnWeight;
            if (randomValue < currentWeight)
                return type;
        }

        // Fallback (should never happen)
        return enemyTypes[0];
    }

    #region RPC Notifications

    /// <summary>
    /// Notifies all clients that a wave has started.
    /// </summary>
    [ObserversRpc]
    private void RpcAnnounceWave(int wave)
    {
        Debug.Log($"Wave {wave} started!");
    }

    /// <summary>
    /// Notifies all clients that a wave has ended.
    /// </summary>
    [ObserversRpc]
    private void RpcAnnounceWaveComplete(int wave)
    {
        Debug.Log($"Wave {wave} completed!");
    }

    #endregion

    #region Public Getters

    public int GetCurrentWave() => currentWave.Value;
    public int GetEnemiesAlive() => enemiesAlive.Value;
    public bool IsWaveInProgress() => waveInProgress.Value;

    #endregion

    #region Server Control

    /// <summary>
    /// Stops all wave spawning immediately.
    /// </summary>
    [Server]
    public void StopSpawning()
    {
        StopAllCoroutines();
        isSpawning = false;
    }

    /// <summary>
    /// Resets wave state back to initial values.
    /// </summary>
    [Server]
    public void ResetWaves()
    {
        StopAllCoroutines();
        isSpawning = false;

        currentWave.Value = 0;
        enemiesAlive.Value = 0;
        waveInProgress.Value = false;

        spawnedEnemies.Clear();

        Debug.Log("Waves reset to 0");
    }

    #endregion
}
