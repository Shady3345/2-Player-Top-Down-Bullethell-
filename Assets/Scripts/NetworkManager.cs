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
    public readonly SyncVar<string> Player1Name = new SyncVar<string>();
    public readonly SyncVar<string> Player2Name = new SyncVar<string>();

    [Header("Game Stats")]
    private readonly SyncVar<int> totalScore = new SyncVar<int>();
    private readonly SyncVar<int> currentWave = new SyncVar<int>();
    private readonly SyncVar<int> enemiesKilled = new SyncVar<int>();

    private readonly SyncVar<int> player1Health = new SyncVar<int>(100);
    private readonly SyncVar<int> player2Health = new SyncVar<int>(100);

    [Header("Game State")]
    private readonly SyncVar<GameState> gameState = new SyncVar<GameState>();
    public GameState CurrentState => gameState.Value;

    [Header("References")]
    public WaveSpawn waveSpawner;
    public HighscoreManager highscoreManager; // ← NEU: Highscore Manager Referenz

    private List<PlayerStats> registeredPlayers = new List<PlayerStats>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        gameState.OnChange += OnStateChanged;
        totalScore.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        currentWave.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        enemiesKilled.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        player1Health.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();
        player2Health.OnChange += (oldVal, newVal, asServer) => UpdateGameUI();

        Player1Name.OnChange += (oldVal, newVal, asServer) =>
        {
            if (player1NameText != null)
                player1NameText.text = string.IsNullOrEmpty(newVal) ? "Waiting..." : newVal;
        };
        Player2Name.OnChange += (oldVal, newVal, asServer) =>
        {
            if (player2NameText != null)
                player2NameText.text = string.IsNullOrEmpty(newVal) ? "Waiting..." : newVal;
        };

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartButtonClicked);
        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyButtonClicked);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateStateText();
        UpdateGameUI();

        if (player1NameText != null) player1NameText.text = "Waiting...";
        if (player2NameText != null) player2NameText.text = "Waiting...";

        ShowPanel(lobbyPanel);
    }

    #region Lobby & Ready System

    public void SetPlayerReady()
    {
        if (PlayerNameField == null) return;

        string playerName = PlayerNameField.text.Trim();
        if (string.IsNullOrEmpty(playerName)) return;

        foreach (var player in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
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

                player.SetReadyServerRpc(playerName);

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
        if (CurrentState != GameState.WaitingForPlayers) return;

        var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        if (players.Length >= 2 && players.All(p => p.IsReady))
        {
            Debug.Log("✓ Both players ready - Starting game!");
            StartGame();
        }
    }

    [Server]
    private void StartGame()
    {
        gameState.Value = GameState.Playing;
        RespawnAllPlayers();
    }

    #endregion

    #region Player Registration

    [Server]
    public void RegisterPlayer(PlayerStats player)
    {
        if (player == null || registeredPlayers.Contains(player)) return;

        registeredPlayers.Add(player);
        int index = registeredPlayers.Count - 1;
        player.SetPlayerIndex(index);

        Debug.Log($"[NetworkGameManager] Player registered with Index {index}");
    }

    [Server]
    public void UnregisterPlayer(PlayerStats player)
    {
        if (registeredPlayers.Contains(player))
        {
            registeredPlayers.Remove(player);
        }
    }

    [Server]
    public void SetPlayerName(int playerIndex, string name)
    {
        if (playerIndex == 0)
        {
            Player1Name.Value = name;
        }
        else if (playerIndex == 1)
        {
            Player2Name.Value = name;
        }
    }

    #endregion

    #region Health System

    [Server]
    public void OnPlayerHealthChanged(int playerIndex, int newHealth)
    {
        if (playerIndex == 0)
        {
            player1Health.Value = newHealth;
        }
        else if (playerIndex == 1)
        {
            player2Health.Value = newHealth;
        }

        Debug.Log($"[NetworkGameManager] Player {playerIndex} health updated to {newHealth}");
    }

    [Server]
    public void DamagePlayer(int playerIndex, int damage)
    {
        if (gameState.Value != GameState.Playing) return;

        if (playerIndex < 0 || playerIndex >= registeredPlayers.Count)
        {
            Debug.LogError($"[NetworkGameManager] Invalid player index: {playerIndex}");
            return;
        }

        PlayerStats targetPlayer = registeredPlayers[playerIndex];

        if (targetPlayer != null && targetPlayer.IsAlive())
        {
            targetPlayer.TakeDamage(damage);
        }
    }

    [Server]
    public void HealPlayer(int playerIndex, int healAmount)
    {
        if (gameState.Value != GameState.Playing) return;

        if (playerIndex < 0 || playerIndex >= registeredPlayers.Count) return;

        PlayerStats targetPlayer = registeredPlayers[playerIndex];

        if (targetPlayer != null)
        {
            targetPlayer.Heal(healAmount);
        }
    }

    public int GetPlayerHealth(int playerIndex)
    {
        return playerIndex == 0 ? player1Health.Value : player2Health.Value;
    }

    [Server]
    public void CheckGameOver()
    {
        bool allDead = registeredPlayers.Count >= 2 &&
                       registeredPlayers.All(p => p != null && !p.IsAlive());

        if (allDead)
        {
            GameOver();
        }
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

        if (waveSpawner != null)
        {
            waveSpawner.StopSpawning();
        }

        DestroyAllEnemies();
        DestroyAllEnemyBullets(); // ← FIX 1: Destroy enemy bullets

        // ← NEU: Highscore hochladen
        SubmitHighscore();
    }

    [Server]
    private void DestroyAllEnemies()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                ServerManager.Despawn(enemy.gameObject);
            }
        }
    }

    // ← FIX 1: Neue Methode zum Zerstören aller Enemy Bullets
    [Server]
    private void DestroyAllEnemyBullets()
    {
        var bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in bullets)
        {
            if (bullet != null)
            {
                ServerManager.Despawn(bullet.gameObject);
            }
        }
        Debug.Log($"[NetworkGameManager] Destroyed {bullets.Length} enemy bullets");
    }

    [Server]
    private void ResetGame()
    {
        totalScore.Value = 0;
        currentWave.Value = 0;
        enemiesKilled.Value = 0;
        player1Health.Value = 100;
        player2Health.Value = 100;
        Player1Name.Value = "";
        Player2Name.Value = "";
    }

    [Server]
    private void RestartGame()
    {
        ResetGame();

        // ← FIX 2: Reset WaveSpawner
        if (waveSpawner != null)
        {
            waveSpawner.ResetWaves();
        }

        var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ResetReady();
        }

        RespawnAllPlayers();
        StartGame();
    }

    [Server]
    public void ReturnToLobby()
    {
        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();

        // ← FIX 2: Reset WaveSpawner auch bei Return to Lobby
        if (waveSpawner != null)
        {
            waveSpawner.ResetWaves();
        }

        var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ResetReady();
        }

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
        ShowPanel(lobbyPanel);

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
        ShowPanel(gamePanel);
    }

    [ObserversRpc]
    private void RpcOnGameEnd()
    {
        ShowPanel(gameOverPanel);

        if (finalScoreText != null)
            finalScoreText.text = $"Final Score: {totalScore.Value}";
        if (finalWaveText != null)
            finalWaveText.text = $"Waves Survived: {currentWave.Value}";
        if (gameOverMessageText != null)
            gameOverMessageText.text = "GAME OVER\nBoth Players Defeated!";

        // ← NEU: Lade Highscores nach kurzer Pause
        Invoke(nameof(LoadHighscores), 0.5f);
    }

    private void LoadHighscores()
    {
        if (highscoreManager != null)
        {
            highscoreManager.RefreshHighscores();
        }
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

    private void OnRestartButtonClicked()
    {
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

    #region Highscore System

    [Server]
    private void SubmitHighscore()
    {
        if (highscoreManager == null)
        {
            Debug.LogWarning("[NetworkGameManager] HighscoreManager not assigned!");
            return;
        }

        // Erstelle Team-Namen aus beiden Spielern
        string player1 = string.IsNullOrEmpty(Player1Name.Value) ? "Player1" : Player1Name.Value;
        string player2 = string.IsNullOrEmpty(Player2Name.Value) ? "Player2" : Player2Name.Value;
        string teamName = $"{player1} & {player2}";

        int finalScore = totalScore.Value;

        Debug.Log($"[NetworkGameManager] Submitting Highscore: {teamName} - {finalScore} points");

        // Sende Score an Server (nur vom Server aus)
        highscoreManager.SubmitScore(teamName, finalScore);
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
        if (scoreText != null)
            scoreText.text = $"Score: {totalScore.Value}";

        if (waveText != null)
            waveText.text = $"Wave: {currentWave.Value}";

        if (healthP1Text != null)
            healthP1Text.text = $"P1: {player1Health.Value} HP";

        if (healthP2Text != null)
            healthP2Text.text = $"P2: {player2Health.Value} HP";
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