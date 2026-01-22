using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveSpawn : NetworkBehaviour
{
    [System.Serializable]
    public class EnemyType
    {
        public GameObject enemyPrefab;
        public string enemyName;
        public int spawnWeight = 1;
        public int scoreValue = 10;
    }

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

    private readonly SyncVar<int> currentWave = new SyncVar<int>(0);
    private readonly SyncVar<int> enemiesAlive = new SyncVar<int>(0);
    private readonly SyncVar<bool> waveInProgress = new SyncVar<bool>(false);

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private bool isSpawning = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    private void Update()
    {
        if (IsServerStarted && !isSpawning && NetworkGameManager.Instance != null)
        {
            if (NetworkGameManager.Instance.IsGamePlaying())
            {
                isSpawning = true;
                StartCoroutine(WaveCoroutine());
            }
        }
    }

    private IEnumerator WaveCoroutine()
    {
        yield return new WaitForSeconds(2f);

        while (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsGamePlaying())
        {
            currentWave.Value++;
            waveInProgress.Value = true;

            NetworkGameManager.Instance.SetCurrentWave(currentWave.Value);

            RpcAnnounceWave(currentWave.Value);

            int enemiesToSpawn = Mathf.RoundToInt(startingEnemiesPerWave + (currentWave.Value - 1) * enemyIncreasePerWave);

            yield return StartCoroutine(SpawnWave(enemiesToSpawn));

            while (enemiesAlive.Value > 0)
            {
                spawnedEnemies.RemoveAll(enemy => enemy == null);
                enemiesAlive.Value = spawnedEnemies.Count;

                yield return new WaitForSeconds(0.5f);
            }

            waveInProgress.Value = false;
            RpcAnnounceWaveComplete(currentWave.Value);

            yield return new WaitForSeconds(timeBetweenWaves);
        }

        isSpawning = false;
    }

    private IEnumerator SpawnWave(int enemyCount)
    {
        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyTypes.Length == 0 || spawnPoints.Length == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPosition = spawnPoint.position + (Vector3)Random.insideUnitCircle * spawnRadius;

        EnemyType selectedType = GetRandomEnemyType();

        if (selectedType?.enemyPrefab == null) return;

        GameObject enemy = Instantiate(selectedType.enemyPrefab, spawnPosition, Quaternion.identity);
        ServerManager.Spawn(enemy);

        Enemy enemyScript = enemy.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.scoreValue = selectedType.scoreValue;
        }

        spawnedEnemies.Add(enemy);
        enemiesAlive.Value++;
    }

    private EnemyType GetRandomEnemyType()
    {
        int totalWeight = 0;
        foreach (var type in enemyTypes)
        {
            totalWeight += type.spawnWeight;
        }

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var type in enemyTypes)
        {
            currentWeight += type.spawnWeight;
            if (randomValue < currentWeight)
            {
                return type;
            }
        }

        return enemyTypes[0];
    }

    [ObserversRpc]
    private void RpcAnnounceWave(int wave)
    {
        Debug.Log($"Wave {wave} startet!");
    }

    [ObserversRpc]
    private void RpcAnnounceWaveComplete(int wave)
    {
        Debug.Log($"Wave {wave} abgeschlossen!");
    }

    public int GetCurrentWave() => currentWave.Value;
    public int GetEnemiesAlive() => enemiesAlive.Value;
    public bool IsWaveInProgress() => waveInProgress.Value;

    [Server]
    public void StopSpawning()
    {
        StopAllCoroutines();
        isSpawning = false;
    }

    // ← FIX 2: Neue Methode zum Zurücksetzen der Waves
    [Server]
    public void ResetWaves()
    {
        StopAllCoroutines();
        isSpawning = false;
        currentWave.Value = 0;
        enemiesAlive.Value = 0;
        waveInProgress.Value = false;
        spawnedEnemies.Clear();

        Debug.Log("[WaveSpawn] Waves reset to 0");
    }
}