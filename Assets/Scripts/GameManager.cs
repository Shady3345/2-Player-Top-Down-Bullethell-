using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Player Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int score = 0;
    public int kills = 0;
    public float survivalTime = 0f;

    [Header("UI References")]
    public Slider hpSlider;
    public Image hpFillImage;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI killsText;
    public TextMeshProUGUI timeText;

    [Header("Game State")]
    public bool gameActive = false;
    public GameObject gameOverPanel;
    public GameObject startPanel;

    private const int POINTS_PER_SECOND = 10;
    private const int BASE_KILL_POINTS = 100;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (!gameActive) return;

        // Überlebenszeit erhöhen
        survivalTime += Time.deltaTime;

        // Jede Sekunde Überlebenspunkte
        if (Time.frameCount % 60 == 0) // ~1 Sekunde bei 60 FPS
        {
            AddScore(POINTS_PER_SECOND);
        }

    

        UpdateUI();
    }

    public void StartGame()
    {
        currentHP = maxHP;
        score = 0;
        kills = 0;
        survivalTime = 0f;
        gameActive = true;

        startPanel.SetActive(false);
        gameOverPanel.SetActive(false);

        UpdateUI();
    }

    public void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
        UpdateHPBar();

        if (currentHP <= 0)
        {
            EndGame();
        }
    }

    public void AddScore(int points)
    {
        score += points;
        scoreText.text = score.ToString("N0");
    }


    void UpdateUI()
    {
        // HP Bar
        UpdateHPBar();

        // Score
        scoreText.text = score.ToString("N0");

        // Kills
        killsText.text = $"Kills: {kills}";

        // Time
        int minutes = Mathf.FloorToInt(survivalTime / 60f);
        int seconds = Mathf.FloorToInt(survivalTime % 60f);
        timeText.text = $"Time: {minutes}:{seconds:00}";
    }

    void UpdateHPBar()
    {
        float hpPercent = (float)currentHP / maxHP;
        hpSlider.value = hpPercent;
        hpText.text = $"{currentHP}/{maxHP}";

        // HP Bar Farbe ändern
        if (hpPercent > 0.6f)
            hpFillImage.color = Color.green;
        else if (hpPercent > 0.3f)
            hpFillImage.color = Color.yellow;
        else
            hpFillImage.color = Color.red;
    }

    void EndGame()
    {
        gameActive = false;
        gameOverPanel.SetActive(true);

        // Highscore speichern
        HighscoreManager.Instance.CheckAndSaveHighscore(score, kills, Mathf.FloorToInt(survivalTime));
    }
}


