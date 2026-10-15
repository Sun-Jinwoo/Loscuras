using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLives : MonoBehaviour
{
    [SerializeField] int maxLives = 3;
    public int Lives { get; private set; }
    public event Action<int> OnLivesChanged;

    void Awake() => Lives = maxLives;

    public void LoseLife()
    {
        Lives--;
        OnLivesChanged?.Invoke(Lives);
        Debug.Log("Vidas: " + Lives);

        if (Lives <= 0)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}