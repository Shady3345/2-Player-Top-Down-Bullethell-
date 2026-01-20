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
        public int spawnWeight = 1; // Höherer Wert = häufigeres Spawnen
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(WaveCoroutine());
    }

    private IEnumerator WaveCoroutine()
    {
        yield return new WaitForSeconds(2f); // Kurze Pause vor erster Wave

        while (true)
        {
            currentWave.Value++;
            waveInProgress.Value = true;

            RpcAnnounceWave(currentWave.Value);

            int enemiesToSpawn = Mathf.RoundToInt(startingEnemiesPerWave + (currentWave.Value - 1) * enemyIncreasePerWave);

            yield return StartCoroutine(SpawnWave(enemiesToSpawn));

            // Warte bis alle Gegner besiegt sind
            while (enemiesAlive.Value > 0)
            {
                // Entferne zerstörte Gegner aus der Liste
                spawnedEnemies.RemoveAll(enemy => enemy == null);
                enemiesAlive.Value = spawnedEnemies.Count;

                yield return new WaitForSeconds(0.5f);
            }

            waveInProgress.Value = false;
            RpcAnnounceWaveComplete(currentWave.Value);

            // Pause zwischen Waves 
            yield return new WaitForSeconds(timeBetweenWaves);
        }
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

        // Wähle zufälligen Spawn-Punkt
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPosition = spawnPoint.position + (Vector3)Random.insideUnitCircle * spawnRadius;

        // Wähle Gegnertyp basierend auf Gewichtung
        EnemyType selectedType = GetRandomEnemyType();

        if (selectedType?.enemyPrefab == null) return;

        // Spawne Gegner
        GameObject enemy = Instantiate(selectedType.enemyPrefab, spawnPosition, Quaternion.identity);
        ServerManager.Spawn(enemy);

        spawnedEnemies.Add(enemy);
        enemiesAlive.Value++;
    }

    private EnemyType GetRandomEnemyType()
    {
        // Berechne Gesamtgewicht
        int totalWeight = 0;
        foreach (var type in enemyTypes)
        {
            totalWeight += type.spawnWeight;
        }

        // Wähle zufällig basierend auf Gewichtung
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

        return enemyTypes[0]; // Fallback
    }

    [ObserversRpc]
    private void RpcAnnounceWave(int wave)
    {
        Debug.Log($"Wave {wave} startet!");
        // Hier kannst du UI-Updates machen
    }

    [ObserversRpc]
    private void RpcAnnounceWaveComplete(int wave)
    {
        Debug.Log($"Wave {wave} abgeschlossen!");
        // Hier kannst du UI-Updates machen
    }

    // Optionale Hilfsmethode für UI
    public int GetCurrentWave() => currentWave.Value;
    public int GetEnemiesAlive() => enemiesAlive.Value;
    public bool IsWaveInProgress() => waveInProgress.Value;
}