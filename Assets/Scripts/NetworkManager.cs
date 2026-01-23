using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main networked game manager handling:
/// - Lobby flow
/// - Game state
/// - Player registration
/// - Health, waves, scoring
/// - UI synchronization
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    // Singleton instance
    public static NetworkGameManager Instance { get; private set; }

    #region Panels

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject gameOverPanel;

    #endregion

    #region Lobby UI

    [Header("Lobby UI")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;
    [SerializeField] private TMP_InputField PlayerNameField;
    [SerializeField] private Button ReadyButton;

    #endregion

    #region Game UI

    [Header("Game UI")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text healthP1Text;
    [SerializeField] private TMP_Text healthP2Text;

    #endregion

    #region Game Over UI

    [Header("GameOver UI")]
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text finalWaveText;
    [SerializeField] private TMP_Text gameOverMessageText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button returnToLobbyButton;

    #endregion

    #region Player Data (Synchronized)

    // Player names synced across the network
    public readonly SyncVar<string> Player1Name = new SyncVar<string>();
    public readonly SyncVar<string> Player2Name = new SyncVar<string>();

    #endregion

    #region Game Stats (Synchronized)

    private readonly SyncVar<int> totalScore = new SyncVar<int>();
    private readonly SyncVar<int> currentWave = new SyncVar<int>();
    private readonly SyncVar<int> enemiesKilled = new SyncVar<int>();

    // Player health synced from server
    private readonly SyncVar<int> player1Health = new SyncVar<int>(100);
    private readonly SyncVar<int> player2Health = new SyncVar<int>(100);

    #endregion

    #region Game State

    // Current game state synced to all clients
    private readonly SyncVar<GameState> gameState = new SyncVar<GameState>();
    public GameState CurrentState => gameState.Value;

    #endregion

    #region References

    [Header("References")]
    public WaveSpawn waveSpawner;
    public HighscoreManager highscoreManager;

    #endregion

    // List of registered players on the server
    private List<PlayerStats> registeredPlayers = new List<PlayerStats>();

    #region Unity Lifecycle

    private void Awake()
    {
        // Enforce singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Listen for state and value changes
        gameState.OnChange += OnStateChanged;

        totalScore.OnChange += (o, n, s) => UpdateGameUI();
        currentWave.OnChange += (o, n, s) => UpdateGameUI();
        enemiesKilled.OnChange += (o, n, s) => UpdateGameUI();
        player1Health.OnChange += (o, n, s) => UpdateGameUI();
        player2Health.OnChange += (o, n, s) => UpdateGameUI();

        // Update player name UI when names change
        Player1Name.OnChange += (o, n, s) =>
        {
            if (player1NameText != null)
                player1NameText.text = string.IsNullOrEmpty(n) ? "Waiting..." : n;
        };

        Player2Name.OnChange += (o, n, s) =>
        {
            if (player2NameText != null)
                player2NameText.text = string.IsNullOrEmpty(n) ? "Waiting..." : n;
        };

        // Button listeners
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartButtonClicked);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyButtonClicked);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Initialize game state on server
        gameState.Value = GameState.WaitingForPlayers;
        ResetGame();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Initialize UI on clients
        UpdateStateText();
        UpdateGameUI();

        if (player1NameText != null) player1NameText.text = "Waiting...";
        if (player2NameText != null) player2NameText.text = "Waiting...";

        ShowPanel(lobbyPanel);
    }

    #endregion

    #region Lobby & Ready System

    /// <summary>
    /// Called when player clicks the Ready button
    /// </summary>
    public void SetPlayerReady()
    {
        if (PlayerNameField == null) return;

        string playerName = PlayerNameField.text.Trim();
        if (string.IsNullOrEmpty(playerName)) return;

        // Find the local player
        foreach (var player in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                // Toggle ready button visuals
                bool isReady = ReadyButton.GetComponent<Image>()?.color == Color.green;
                bool newReadyState = !isReady;

                var image = ReadyButton.GetComponent<Image>();
                var text = ReadyButton.GetComponentInChildren<TMP_Text>();

                if (image != null)
                    image.color = newReadyState ? Color.green : Color.white;

                if (text != null)
                    text.text = newReadyState ? "READY!" : "Ready?";

                // Send ready state and name to server
