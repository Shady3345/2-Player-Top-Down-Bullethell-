using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class HighscoreEntry
{
    public string playerName;
    public int score;
    public int kills;
    public int time;
    public string timestamp;
}

[System.Serializable]
public class HighscoreList
{
    public List<HighscoreEntry> highscores;
}

public class HighscoreManager : MonoBehaviour
{
    public static HighscoreManager Instance;

    [Header("Backend Settings")]
    public string serverURL = "https://yourserver.com/api/";

    [Header("UI References")]
    public GameObject highscorePanel;
    public Transform highscoreContent;
    public GameObject highscoreEntryPrefab;
    public TMP_InputField nameInput;

    private List<HighscoreEntry> currentHighscores = new List<HighscoreEntry>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Highscores vom Server laden
    public void LoadHighscores()
    {
        StartCoroutine(LoadHighscoresCoroutine());
    }

    IEnumerator LoadHighscoresCoroutine()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(serverURL + "get_highscores.php"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = "{\"highscores\":" + request.downloadHandler.text + "}";
                HighscoreList list = JsonUtility.FromJson<HighscoreList>(json);
                currentHighscores = list.highscores;
                DisplayHighscores();
            }
            else
            {
                Debug.LogError("Fehler beim Laden der Highscores: " + request.error);
            }
        }
    }

    // Highscore speichern
    public void CheckAndSaveHighscore(int score, int kills, int time)
    {
        string playerName = string.IsNullOrEmpty(nameInput.text) ? "PLAYER" : nameInput.text;
        StartCoroutine(SaveHighscoreCoroutine(playerName, score, kills, time));
    }

    IEnumerator SaveHighscoreCoroutine(string playerName, int score, int kills, int time)
    {
        WWWForm form = new WWWForm();
        form.AddField("playerName", playerName);
        form.AddField("score", score);
        form.AddField("kills", kills);
        form.AddField("time", time);

        using (UnityWebRequest request = UnityWebRequest.Post(serverURL + "save_highscore.php", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Highscore gespeichert!");
                LoadHighscores(); // Aktualisierte Liste laden
            }
            else
            {
                Debug.LogError("Fehler beim Speichern: " + request.error);
            }
        }
    }

    // Highscores in UI anzeigen
    void DisplayHighscores()
    {
        // Alte Einträge löschen
        foreach (Transform child in highscoreContent)
        {
            Destroy(child.gameObject);
        }

        // Neue Einträge erstellen
        for (int i = 0; i < currentHighscores.Count; i++)
        {
            GameObject entry = Instantiate(highscoreEntryPrefab, highscoreContent);
            HighscoreEntry data = currentHighscores[i];

            // Texte setzen (passe an dein Prefab an)
            TextMeshProUGUI[] texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
            texts[0].text = (i + 1).ToString(); // Rank
            texts[1].text = data.playerName;
            texts[2].text = data.score.ToString("N0");
            texts[3].text = $"K:{data.kills} T:{FormatTime(data.time)}";
        }

        highscorePanel.SetActive(true);
    }

    string FormatTime(int seconds)
    {
        int mins = seconds / 60;
        int secs = seconds % 60;
        return $"{mins}:{secs:00}";
    }

    public void ShowHighscores()
    {
        LoadHighscores();
    }

    public void CloseHighscores()
    {
        highscorePanel.SetActive(false);
    }
}

