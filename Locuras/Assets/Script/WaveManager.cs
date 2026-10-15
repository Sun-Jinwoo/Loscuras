using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class WaveManager : MonoBehaviour
{
    public enum State { Prep, Wave }

    [Header("Referencias")]
    [SerializeField] PhaseClock clock;
    [SerializeField] CrystalEnemy enemyPrefab;
    [SerializeField] Transform[] spawnPoints;

    [Header("Oleadas")]
    [SerializeField] int firstWaveEnemies = 4;
    [SerializeField] int extraEnemiesPerWave = 2;
    [SerializeField] float spawnInterval = 1.2f;
    [SerializeField] float prepTime = 20f;

    public State CurrentState { get; private set; }
    public int Wave { get; private set; }
    public int EnemiesLeft => toSpawn + alive;
    public float PrepTimeLeft { get; private set; }

    public event Action<int> OnWaveStarted;
    public event Action<int> OnPrepStarted;   // recibe el número de la próxima oleada

    int toSpawn, alive;

    void Start()
    {
        if (clock == null || enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: falta asignar Clock, Enemy Prefab o Spawn Points", this);
            enabled = false;
            return;
        }
        StartPrep();
    }

    void Update()
    {
        if (CurrentState == State.Prep)
        {
            PrepTimeLeft -= Time.deltaTime;
            bool skip = Keyboard.current.fKey.wasPressedThisFrame;
            if (PrepTimeLeft <= 0f || skip) StartWave();
        }
        else if (EnemiesLeft == 0)
        {
            StartPrep();
        }
    }

    void StartPrep()
    {
        CurrentState = State.Prep;
        PrepTimeLeft = prepTime;
        clock.SetRunning(false);          // el reloj se pausa mientras te preparas
        OnPrepStarted?.Invoke(Wave + 1);
    }

    void StartWave()
    {
        Wave++;
        CurrentState = State.Wave;
        toSpawn = firstWaveEnemies + extraEnemiesPerWave * (Wave - 1);

        clock.ResetClock();               // cada oleada empieza en ámbar
        clock.SetRunning(true);

        OnWaveStarted?.Invoke(Wave);
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (toSpawn > 0)
        {
            var point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            var enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.Init(clock);
            enemy.OnDied += HandleDied;

            toSpawn--;
            alive++;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void HandleDied(CrystalEnemy e) => alive--;
}