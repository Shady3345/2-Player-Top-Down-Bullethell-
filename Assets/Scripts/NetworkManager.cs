using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Lobby UI")]
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

    [Header("GameOver UI")]
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text finalWaveText;
    [SerializeField] private TMP_Text gameOverMessageText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button returnToLobbyButton;

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
        Debug.Log("=== NetworkGameManager Awake ===");

        if (Instance == null)
        {
            Instance = this;
            Debug.Log("NetworkGameManager Instance set");
        }
        else
        {
            Debug.LogWarning("Duplicate NetworkGameManager found! Destroying...");
            Destroy(gameObject);
            return;
        }

        // Event-Listener für SyncVars
        gameState.OnChange += OnStateChanged;
        totalScore.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        currentWave.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        enemiesKilled.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        healthP1.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        healthP2.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();

        Player1.OnChange += (oldVal, newVal, asServer) =>
        {
            Debug.Log($"Player1 name changed: {oldVal} -> {newVal}");
            if (player1NameText != null)
                player1NameText.text = string.IsNullOrEmpty(newVal) ? "Waiting..." : newVal;
        };
        Player2.OnChange += (oldVal, newVal, asServer) =>
        {
            Debug.Log($"Player2 name changed: {oldVal} -> {newVal}");
            if (player2NameText != null)
                player2NameText.text = string.IsNullOrEmpty(newVal) ? "Waiting..." : newVal;
        };

        // Button Listeners
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartButtonClicked);
        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyButtonClicked);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("=== NetworkGameManager OnStartServer ===");
        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"=== NetworkGameManager OnStartClient === IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}");
        UpdateStateText();
        UpdateGameUI();

        // Setze initiale Texte
        if (player1NameText != null) player1NameText.text = "Waiting...";
        if (player2NameText != null) player2NameText.text = "Waiting...";

        // Zeige Lobby Panel, verstecke andere Panels
        ShowPanel(lobbyPanel);
    }

    #region Lobby & Ready System

    public void SetPlayerReady()
    {
        Debug.Log("=== SetPlayerReady called ===");

        if (PlayerNameField == null)
        {
            Debug.LogError("PlayerNameField is NULL!");
            return;
        }

        string playerName = PlayerNameField.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("Please enter a name!");
            return;
        }

        // Finde den lokalen Spieler
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                Debug.Log($"Found local player, setting ready with name: {playerName}");

                // Toggle Ready State visuell
                if (ReadyButton != null)
                {
                    bool isCurrentlyReady = ReadyButton.GetComponent<Image>()?.color == Color.green;
                    bool newReadyState = !isCurrentlyReady;

                    var image = ReadyButton.GetComponent<Image>();
                    var text = ReadyButton.GetComponentInChildren<TMP_Text>();

                    if (image != null)
                        image.color = newReadyState ? Color.green : Color.white;
                    if (text != null)
                        text.text = newReadyState ? "READY!" : "Ready?";
                }

                // Sende Ready-State an Server
                player.SetReadyStateServerRpc(playerName);

                // Deaktiviere Name Field nach erster Eingabe
                if (PlayerNameField.interactable)
                {
                    PlayerNameField.interactable = false;
                }

                break;
            }
        }
    }

    [Server]
    public void CheckAndStartGame()
    {
        Debug.Log("=== CheckAndStartGame ===");

        if (CurrentState != GameState.WaitingForPlayers) return;

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        // Zähle ready players
        int readyCount = 0;
        foreach (var player in players)
        {
            if (player.IsReady)
                readyCount++;
        }

        Debug.Log($"Players: {players.Length}, Ready: {readyCount}");

        // Starte Spiel wenn mindestens 2 Spieler bereit sind
        if (players.Length >= 2 && players.All(p => p.IsReady))
        {
            Debug.Log("✓✓✓ Both players ready - Starting game! ✓✓✓");
            StartGame();
        }
    }

    [Server]
    private void StartGame()
    {
        Debug.Log("=== GAME STARTING ===");
        gameState.Value = GameState.Playing;

        // Initialisiere Spieler-Health
        healthP1.Value = 100;
        healthP2.Value = 100;

        // Respawn Spieler falls nötig
        RespawnAllPlayers();

        Debug.Log("Wave Spawner will start automatically");
    }

    [TargetRpc]
    public void DisableNameField(NetworkConnection con, bool isOff)
    {
        if (PlayerNameField != null)
            PlayerNameField.gameObject.SetActive(!isOff);
    }

    #endregion

    #region Health System

    [Server]
    public void SetPlayerHealth(int playerIndex, int health)
    {
        Debug.Log($"SetPlayerHealth called: Player {playerIndex} -> {health} HP");

        if (playerIndex == 0)
        {
            healthP1.Value = health;
        }
        else if (playerIndex == 1)
        {
            healthP2.Value = health;
        }

        // Check for Game Over
        if (healthP1.Value <= 0 && healthP2.Value <= 0)
        {
            GameOver();
        }
    }

    [Server]
    public void DamagePlayer(int playerIndex, int damage)
    {
        if (gameState.Value != GameState.Playing) return;

        Debug.Log($"DamagePlayer: Player {playerIndex} takes {damage} damage");

        var playerStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        if (playerIndex >= 0 && playerIndex < playerStats.Length)
        {
            playerStats[playerIndex].TakeDamage(damage);
        }
    }

    [Server]
    public void HealPlayer(int playerIndex, int healAmount)
    {
        if (gameState.Value != GameState.Playing) return;

        var playerStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        if (playerIndex >= 0 && playerIndex < playerStats.Length)
        {
            playerStats[playerIndex].Heal(healAmount);
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

        Debug.Log($"Enemy killed! Total Score: {totalScore.Value}, Enemies Killed: {enemiesKilled.Value}");
    }

    [Server]
    public void SetCurrentWave(int wave)
    {
        currentWave.Value = wave;
        Debug.Log($"Current Wave set to: {wave}");
    }

    #endregion

    #region Game State Management

    private void OnStateChanged(GameState oldState, GameState newState, bool asServer)
    {
        Debug.Log($"Game State Changed: {oldState} -> {newState}");
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
        Debug.Log("=== GAME OVER ===");
        gameState.Value = GameState.Finished;

        // Stoppe Wave-Spawner
        if (waveSpawner != null)
        {
            waveSpawner.StopSpawning();
        }

        // Entferne alle verbleibenden Enemies
        DestroyAllEnemies();
    }

    [Server]
    private void DestroyAllEnemies()
    {
        // Finde alle Enemies und zerstöre sie
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                ServerManager.Despawn(enemy.gameObject);
            }
        }
        Debug.Log($"Destroyed {enemies.Length} remaining enemies");
    }

    [Server]
    private void ResetGame()
    {
        totalScore.Value = 0;
        currentWave.Value = 0;
        enemiesKilled.Value = 0;
        healthP1.Value = 100;
        healthP2.Value = 100;
        Player1.Value = "";
        Player2.Value = "";
    }

    [Server]
    private void RestartGame()
    {
        Debug.Log("=== RESTARTING GAME ===");

        // Reset alle Stats
        ResetGame();

        // Setze alle Spieler zurück auf "not ready"
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ResetReadyState();
        }

        // Respawn alle Spieler
        RespawnAllPlayers();

        // Starte das Spiel direkt neu
        StartGame();
    }

    [Server]
    public void ReturnToLobby()
    {
        Debug.Log("=== RETURNING TO LOBBY ===");

        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();

        // Setze alle Spieler zurück auf "not ready"
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ResetReadyState();
        }

        // Respawn alle Spieler für die Lobby
        RespawnAllPlayers();

        RpcReturnToLobby();
    }

    [Server]
    private void RespawnAllPlayers()
    {
        var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player != null)
            {
                player.Respawn();
            }
        }
    }

    [ObserversRpc]
    private void RpcReturnToLobby()
    {
        Debug.Log("RpcReturnToLobby called");

        // Zurück zum Lobby Panel
        ShowPanel(lobbyPanel);

        // Reset UI
        if (PlayerNameField != null)
        {
            PlayerNameField.interactable = true;
            PlayerNameField.text = "";
        }
        if (ReadyButton != null)
        {
            var image = ReadyButton.GetComponent<Image>();
            var text = ReadyButton.GetComponentInChildren<TMP_Text>();
            if (image != null) image.color = Color.white;
            if (text != null) text.text = "Ready?";
        }
    }

    [ObserversRpc]
    private void RpcOnGameStart()
    {
        Debug.Log("✓✓✓ Game Started! ✓✓✓");
        ShowPanel(gamePanel);
    }

    [ObserversRpc]
    private void RpcOnGameEnd()
    {
        Debug.Log($"Game Over! Final Score: {totalScore.Value}, Waves Survived: {currentWave.Value}");

        // Zeige GameOver Panel
        ShowPanel(gameOverPanel);

        // Update GameOver Stats
        if (finalScoreText != null)
            finalScoreText.text = $"Final Score: {totalScore.Value}";
        if (finalWaveText != null)
            finalWaveText.text = $"Waves Survived: {currentWave.Value}";
        if (gameOverMessageText != null)
            gameOverMessageText.text = "GAME OVER\nBoth Players Defeated!";
    }

    private void ShowPanel(GameObject panelToShow)
    {
        if (lobbyPanel != null)
            lobbyPanel.SetActive(panelToShow == lobbyPanel);
        if (gamePanel != null)
            gamePanel.SetActive(panelToShow == gamePanel);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(panelToShow == gameOverPanel);
    }

    // Button Callbacks
    private void OnRestartButtonClicked()
    {
        Debug.Log("Restart button clicked");
        if (IsServerStarted)
        {
            RestartGame();
        }
        else
        {
            RequestRestartServerRpc();
        }
    }

    private void OnReturnToLobbyButtonClicked()
    {
        Debug.Log("Return to Lobby button clicked");
        if (IsServerStarted)
        {
            ReturnToLobby();
        }
        else
        {
            RequestReturnToLobbyServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRestartServerRpc()
    {
        RestartGame();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReturnToLobbyServerRpc()
    {
        ReturnToLobby();
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
                stateText.text = "";
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

    #region Public Getters

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