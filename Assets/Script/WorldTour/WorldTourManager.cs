using UnityEngine;
using System;
using System.Collections.Generic;
using Homerunderby;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class WorldTourManager : MonoBehaviour
{
    [SerializeField] private WorldTourConfigAsset _worldTourConfig;
    [SerializeField] private PitchCatalog _pitchCatalog;
    public static WorldTourManager Instance { get; private set; }
    private WorldTourConfig _config;
    private WorldData _currentWorldData;
    private PlayerSaveData _saveData = new PlayerSaveData();

    public PlayerSaveData SaveData => _saveData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _config = JsonTest.LoadConfig(_worldTourConfig.ToJson());
        WorldSelect("01");
        StageSelect("0001").Forget();
    }

    public void SetData(WorldTourConfig config)
    {
        _config = config;
    }

    public void SetSaveData(PlayerSaveData saveData)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
    }

    private void WorldSelect(string worldId)
    {
        _currentWorldData = _config.Worlds.Find(world => world.WorldId == worldId);
    }

    private async UniTaskVoid StageSelect(string stageId)
    {
        StageData stage = _currentWorldData?.Stages.Find(s => s.StageId == stageId);
        if (stage == null)
        {
            Debug.LogError($"StageId {stageId} not found in the selected world.", this);
            return;
        }

        SceneManager.sceneLoaded += StopDerbyManager;
        try
        {
            await SceneManager.LoadSceneAsync("Homerun");
            await UniTask.NextFrame();

            if (!SetPitchBall(stage)) return;

            WorldTourInGameManager gameManager = FindFirstObjectByType<WorldTourInGameManager>(FindObjectsInactive.Include);
            if (gameManager == null)
            {
                Debug.LogError("WorldTourInGameManager not found in the Homerun scene.", this);
                return;
            }

            gameManager.enabled = true;
            gameManager.BeginStage(stage, _saveData);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            SceneManager.sceneLoaded -= StopDerbyManager;
        }
    }

    private void StopDerbyManager(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Homerun") return;

        HomeRunDerbyManager derbyManager = FindFirstObjectByType<HomeRunDerbyManager>();
        if (derbyManager == null) return;

        derbyManager.enabled = false;
        Destroy(derbyManager);
    }

    private bool SetPitchBall(StageData data)
    {
        if (_pitchCatalog == null || data.PitchPool == null)
        {
            Debug.LogError("PitchCatalog or stage pitch pool is not set.", this);
            return false;
        }

        List<WeightedPitch> ballList = new List<WeightedPitch>();

        foreach (var ball in data.PitchPool)
        {
            if (!_pitchCatalog.TryGetPitch(ball.PitchId, out PitchTypeData pitchData))
            {
                Debug.LogError($"PitchId {ball.PitchId} not found in PitchCatalog.");
                continue;
            }
            WeightedPitch weightedPitch = new WeightedPitch
            {
                PitchType = pitchData,
                Weight = ball.Weight
            };
            ballList.Add(weightedPitch);
        }

        if (ballList.Count == 0 || !ballList.Exists(ball => ball.Weight > 0f))
        {
            Debug.LogError($"StageId {data.StageId} has no selectable pitches.", this);
            return false;
        }

        PitcherController pitcher = FindFirstObjectByType<PitcherController>();
        if (pitcher == null)
        {
            Debug.LogError("PitcherController not found in the scene.", this);
            return false;
        }

        pitcher.SetBallList(ballList);
        return true;
    }
}
