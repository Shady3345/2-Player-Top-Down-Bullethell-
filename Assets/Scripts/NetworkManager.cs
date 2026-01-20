using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;
    [SerializeField] private TMP_InputField PlayerNameField;
    [SerializeField] private Button ReadyButton;

    [Header("Game UI")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text healthP1Text;
    [SerializeField] private TMP_Text healthP2Text;

    [Header("Player Data")]
    public readonly SyncVar<string> Player1 = new SyncVar<string>();
    public readonly SyncVar<string> Player2 = new SyncVar<string>();
    private readonly SyncVar<int> healthP1 = new SyncVar<int>(100);
    private readonly SyncVar<int> healthP2 = new SyncVar<int>(100);

    [Header("Game Stats")]
    private readonly SyncVar<int> totalScore = new SyncVar<int>();
    private readonly SyncVar<int> currentWave = new SyncVar<int>();
    private readonly SyncVar<int> enemiesKilled = new SyncVar<int>();

    [Header("Game State")]
    private readonly SyncVar<GameState> gameState = new SyncVar<GameState>();
    public GameState CurrentState => gameState.Value;

    [Header("References")]
    public WaveSpawn waveSpawner;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Event-Listener für SyncVars
        gameState.OnChange += OnStateChanged;
        totalScore.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        currentWave.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        enemiesKilled.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        healthP1.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        healthP2.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();

        Player1.OnChange += (oldVal, newVal, asServer) =>
        {
            if (player1NameText != null)
                player1NameText.text = newVal;
        };
        Player2.OnChange += (oldVal, newVal, asServer) =>
        {
            if (player2NameText != null)
                player2NameText.text = newVal;
        };
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();
    }

    #region Lobby & Ready System

    [Server]
    public void CheckAndStartGame()
    {
        if (CurrentState != GameState.WaitingForPlayers) return;

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        if (players.Length >= 2 && players.All(p => p.IsReady))
        {
            StartGame();
        }
    }

    [Server]
    private void StartGame()
    {
        gameState.Value = GameState.Playing;

        // Initialisiere Spieler-Health
        healthP1.Value = 100;
        healthP2.Value = 100;

        // Starte Wave-System
        if (waveSpawner != null)
        {
            Debug.Log("Starting Wave Spawner");
        }
        else
        {
            Debug.LogWarning("WaveSpawner reference is missing!");
        }
    }

    public void SetPlayerReady()
    {
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                if (!player.IsReady)
                    ReadyButton.image.color = Color.green;
                else
                    ReadyButton.image.color = Color.white;

                player.SetReadyStateServerRpc(PlayerNameField.text);
            }
        }
    }

    [TargetRpc]
    public void DisableNameField(NetworkConnection con, bool isOff)
    {
        PlayerNameField.gameObject.SetActive(!isOff);
    }

    #endregion

    #region Health System

    [Server]
    public void DamagePlayer(int playerIndex, int damage)
    {
        if (gameState.Value != GameState.Playing) return;

        if (playerIndex == 0)
        {
            healthP1.Value = Mathf.Max(0, healthP1.Value - damage);
        }
        else if (playerIndex == 1)
        {
            healthP2.Value = Mathf.Max(0, healthP2.Value - damage);
        }

        // Check for Game Over
        if (healthP1.Value <= 0 && healthP2.Value <= 0)
        {
            GameOver();
        }
    }

    [Server]
    public void HealPlayer(int playerIndex, int healAmount)
    {
        if (gameState.Value != GameState.Playing) return;

        if (playerIndex == 0)
        {
            healthP1.Value = Mathf.Min(100, healthP1.Value + healAmount);
        }
        else if (playerIndex == 1)
        {
            healthP2.Value = Mathf.Min(100, healthP2.Value + healAmount);
        }
    }

    public int GetPlayerHealth(int playerIndex)
    {
        return playerIndex == 0 ? healthP1.Value : healthP2.Value;
    }

    #endregion

    #region Scoring & Wave System

    [Server]
    public void EnemyKilled(int scoreValue = 10)
    {
        if (gameState.Value != GameState.Playing) return;

        enemiesKilled.Value++;
        totalScore.Value += scoreValue;
    }

    [Server]
    public void SetCurrentWave(int wave)
    {
        currentWave.Value = wave;
    }

    #endregion

    #region Game State Management

    private void OnStateChanged(GameState oldState, GameState newState, bool asServer)
    {
        UpdateStateText();
        UpdateGameUI();

        // Spiel-spezifische State-Änderungen
        if (newState == GameState.Playing)
        {
            RpcOnGameStart();
        }
        else if (newState == GameState.Finished)
        {
            RpcOnGameEnd();
        }
    }

    [Server]
    private void GameOver()
    {
        gameState.Value = GameState.Finished;

        // Stoppe Wave-Spawner (wenn vorhanden)
        if (waveSpawner != null)
        {
            // waveSpawner könnte eine Stop-Methode haben
        }
    }

    [Server]
    private void ResetGame()
    {
        totalScore.Value = 0;
        currentWave.Value = 0;
        enemiesKilled.Value = 0;
        healthP1.Value = 100;
        healthP2.Value = 100;
    }

    [ObserversRpc]
    private void RpcOnGameStart()
    {
        Debug.Log("Game Started!");
        // Hier kannst du UI-Animationen, Sounds, etc. triggern
    }

    [ObserversRpc]
    private void RpcOnGameEnd()
    {
        Debug.Log($"Game Over! Final Score: {totalScore.Value}, Waves Survived: {currentWave.Value}");
        // Hier kannst du Game Over UI anzeigen
    }

    #endregion

    #region UI Updates

    private void UpdateStateText()
    {
        if (stateText == null) return;

        switch (gameState.Value)
        {
            case GameState.WaitingForPlayers:
                stateText.text = "Waiting for Players...";
                break;
            case GameState.Playing:
                stateText.text = "FIGHT!";
                break;
            case GameState.Finished:
                stateText.text = "GAME OVER";
                break;
        }
    }

    private void UpdateGameUI()
    {
        // Score
        if (scoreText != null)
            scoreText.text = $"Score: {totalScore.Value}";

        // Wave
        if (waveText != null)
            waveText.text = $"Wave: {currentWave.Value}";

        // Health
        if (healthP1Text != null)
            healthP1Text.text = $"P1: {healthP1.Value} HP";

        if (healthP2Text != null)
            healthP2Text.text = $"P2: {healthP2.Value} HP";
    }

    #endregion

    #region Public Getters (für andere Scripts)

    public int GetTotalScore() => totalScore.Value;
    public int GetCurrentWave() => currentWave.Value;
    public int GetEnemiesKilled() => enemiesKilled.Value;
    public bool IsGamePlaying() => gameState.Value == GameState.Playing;

    #endregion
}

public enum GameState
{
    WaitingForPlayers,
    Playing,
    Finished
}