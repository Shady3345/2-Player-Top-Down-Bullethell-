using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class HighscoreManager : MonoBehaviour
{
    [Header("Settings")]
    public string addScoreURL = "http://localhost/highscore/addscore.php";
    public string getScoreURL = "http://localhost/highscore/getscores.php";
    public string secretKey = "Key1234";

    [Header("UI References")]
    public Transform scoreContainer;
    public GameObject scoreItemPrefab;

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

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Score successfully submitted");
            }
            else
            {
                Debug.LogError($"Failed to submit score: {www.error}");
            }
        }
    }

    public void RefreshHighscores()
    {
        StartCoroutine(GetScores());
    }

    private IEnumerator GetScores()
    {
        // Clear old entries
        foreach (Transform child in scoreContainer)
        {
            Destroy(child.gameObject);
        }

        using (UnityWebRequest www = UnityWebRequest.Get(getScoreURL))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonText = www.downloadHandler.text;

                if (string.IsNullOrEmpty(jsonText))
                {
                    Debug.LogError("Response is empty");
                    yield break;
                }

                try
                {
                    HighscoreData data = JsonUtility.FromJson<HighscoreData>(jsonText);

                    if (data == null || data.items == null)
                    {
                        Debug.LogError("Failed to parse JSON or items is null");
                        yield break;
                    }

                    foreach (ScoreEntry entry in data.items)
                    {
                        GameObject go = Instantiate(scoreItemPrefab, scoreContainer);
                        go.GetComponent<TextMeshProUGUI>().text = $"{entry.username}: {entry.score}";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"JSON Parse Error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"Web Request Failed: {www.error}");
            }
        }
    }

    private string Md5Sum(string strToEncrypt)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(strToEncrypt);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}

// Data classes for JSON
[System.Serializable]
public class ScoreEntry
{
    public string username;
    public int score;
}

[System.Serializable]
public class HighscoreData
{
    public ScoreEntry[] items;
}