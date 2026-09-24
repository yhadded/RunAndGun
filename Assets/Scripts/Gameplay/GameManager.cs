using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static int Score { get; private set; }
    private static int scoreAtLevelStart;

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Flow")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private float gameOverDelay = 1.2f;
    [SerializeField] private float levelTransitionDelay = 1.5f;
    [SerializeField] private AudioClip levelCompleteClip;
    [SerializeField] private AudioClip gameOverClip;

    public event Action<int> ScoreChanged;
    public bool IsGameOver { get; private set; }

    private bool levelEnding;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        scoreAtLevelStart = Score;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (levelCompletePanel) levelCompletePanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        ScoreChanged?.Invoke(Score);
    }

    public static void ResetRun()
    {
        Score = 0;
        scoreAtLevelStart = 0;
    }

    public void AddScore(int amount)
    {
        if (IsGameOver) return;
        Score += amount;
        ScoreChanged?.Invoke(Score);
    }

    public void PlayerDied()
    {
        if (IsGameOver || levelEnding) return;
        IsGameOver = true;
        StartCoroutine(ShowAfter(gameOverPanel, gameOverDelay, gameOverClip));
    }

    public void CompleteLevel(string nextSceneName)
    {
        if (levelEnding || IsGameOver) return;
        levelEnding = true;
        StartCoroutine(LevelTransition(nextSceneName));
    }

    private IEnumerator LevelTransition(string nextSceneName)
    {
        if (levelCompleteClip) AudioSource.PlayClipAtPoint(levelCompleteClip, Camera.main ? Camera.main.transform.position : Vector3.zero);

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        bool hasNext = !string.IsNullOrEmpty(nextSceneName) || nextIndex < SceneManager.sceneCountInBuildSettings;

        if (!hasNext)
        {
            if (victoryPanel) victoryPanel.SetActive(true);
            yield break;
        }

        if (levelCompletePanel) levelCompletePanel.SetActive(true);
        yield return new WaitForSeconds(levelTransitionDelay);

        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
        else SceneManager.LoadScene(nextIndex);
    }

    private IEnumerator ShowAfter(GameObject panel, float delay, AudioClip clip)
    {
        yield return new WaitForSeconds(delay);
        if (clip) AudioSource.PlayClipAtPoint(clip, Camera.main ? Camera.main.transform.position : Vector3.zero);
        if (panel) panel.SetActive(true);
    }

    public void Retry()
    {
        Score = scoreAtLevelStart;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        ResetRun();
        SceneManager.LoadScene(mainMenuScene);
    }
}
