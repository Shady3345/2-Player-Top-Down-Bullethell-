using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro; // Wichtig für TextMeshPro

public class HighscoreManager : MonoBehaviour
{
    [Header("Einstellungen")]
    public string addScoreURL = "http://localhost/highscore/addscore.php";
    public string getScoreURL = "http://localhost/highscore/getscores.php";
    public string secretKey = "Key1234";

    [Header("UI Referenzen")]
    public Transform scoreContainer; // Der "ScoreContainer" von oben
    public GameObject scoreItemPrefab; // Dein Text-Prefab

    // 1. SCORE SENDEN (Aufrufen wenn der Spieler stirbt / gewinnt)
    public void SubmitScore(string name, int score)
    {
        StartCoroutine(PostScore(name, score));
    }

    private IEnumerator PostScore(string name, int score)
    {
        string hash = Md5Sum(name + score + secretKey);

        WWWForm form = new WWWForm();
        form.AddField("name", name);
        form.AddField("score", score);
        form.AddField("hash", hash);

        using (UnityWebRequest www = UnityWebRequest.Post(addScoreURL, form))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success) Debug.Log("Server: " + www.downloadHandler.text);
        }
    }

    // 2. LISTE LADEN
    public void RefreshHighscores()
    {
        StartCoroutine(GetScores());
    }

    private IEnumerator GetScores()
    {
        // Erstmal alte Einträge in der UI löschen
        foreach (Transform child in scoreContainer) { Destroy(child.gameObject); }

        using (UnityWebRequest www = UnityWebRequest.Get(getScoreURL))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // JSON parsen
                HighscoreData data = JsonUtility.FromJson<HighscoreData>(www.downloadHandler.text);

                foreach (ScoreEntry entry in data.items)
                {
                    // Neues Text-Objekt erzeugen
                    GameObject go = Instantiate(scoreItemPrefab, scoreContainer);
                    go.GetComponent<TextMeshProUGUI>().text = $"{entry.username}: {entry.score}";
                }
            }
        }
    }

    // Hilfsfunktion für den Sicherheits-Hash (MD5)
    private string Md5Sum(string strToEncrypt)
    {
        System.Text.UTF8Encoding ue = new System.Text.UTF8Encoding();
        byte[] bytes = ue.GetBytes(strToEncrypt);
        System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
        byte[] hashBytes = md5.ComputeHash(bytes);
        string hashString = "";
        for (int i = 0; i < hashBytes.Length; i++)
        {
            hashString += System.Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
        }
        return hashString.PadLeft(32, '0');
    }
}

// Daten-Klassen für JSON (Müssen außerhalb der Hauptklasse stehen)
[System.Serializable] public class ScoreEntry { public string username; public int score; }
[System.Serializable] public class HighscoreData { public ScoreEntry[] items; }