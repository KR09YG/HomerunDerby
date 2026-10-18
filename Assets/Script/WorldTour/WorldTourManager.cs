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
    private bool _isLoading;
    private StageData _currentStage;

    public PlayerSaveData SaveData => _saveData;
    public WorldTourConfig Config => _config;
    public WorldData CurrentWorld => _currentWorldData;
    public StageData CurrentStage => _currentStage;
    public bool IsLoading => _isLoading;

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

        if (_worldTourConfig == null)
        {
            Debug.LogError("[WorldTour] ワールド設定が未設定です。", this);
            return;
        }
        _config = _worldTourConfig.Config;
        if (_config == null)
        {
            Debug.LogError("[WorldTour] ワールド設定の内容が空です。", this);
            return;
        }
        _currentWorldData = _config.Worlds.Find(world => !string.IsNullOrEmpty(world.WorldId) && world.Stages.Count > 0);
        // 従来の確認用シーンだけは、初期ステージを直接開始できるようにする。
        if (SceneManager.GetActiveScene().name == "test_world_tour" && _currentWorldData?.Stages.Count > 0)
            StageSelect(_currentWorldData.Stages[0].StageId).Forget();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= StopDerbyManager;
        if (Instance == this) Instance = null;
    }

    public void SetData(WorldTourConfig config)
    {
        _config = config;
    }

    public void SetSaveData(PlayerSaveData saveData)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
    }

    public void WorldSelect(string worldId)
    {
        WorldData world = _config?.Worlds.Find(value => value.WorldId == worldId);
        if (world != null && IsWorldUnlocked(world)) _currentWorldData = world;
    }

    public bool IsWorldUnlocked(WorldData world)
    {
        return world != null && !string.IsNullOrEmpty(world.WorldId) &&
            world.Stages.Count > 0 && _config != null && _config.Worlds.Contains(world);
    }

    public bool IsStageUnlocked(StageData stage)
    {
        if (_currentWorldData == null || stage == null || !IsWorldUnlocked(_currentWorldData)) return false;
        // 開放条件を接続するまでは、登録済みのステージをすべて選べるようにする。
        return _currentWorldData.Stages.Contains(stage);
    }

    public void PlayStage(string stageId)
    {
        StageData stage = _currentWorldData?.Stages.Find(value => value.StageId == stageId);
        if (IsStageUnlocked(stage)) StageSelect(stageId).Forget();
    }

    public StageData GetNextStage()
    {
        if (_currentWorldData == null || _currentStage == null) return null;
        int index = _currentWorldData.Stages.IndexOf(_currentStage) + 1;
        return index > 0 && index < _currentWorldData.Stages.Count ? _currentWorldData.Stages[index] : null;
    }

    public void OpenWorldSelect() => OpenMenu("WorldSelect").Forget();
    public void OpenStageSelect() => OpenMenu("StageSelect").Forget();
    public void OpenTitle() => OpenMenu("Title").Forget();

    private async UniTask OpenMenu(string sceneName)
    {
        if (_isLoading) return;
        _isLoading = true;
        try { await SceneManager.LoadSceneAsync(sceneName); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally { _isLoading = false; }
    }

    private async UniTaskVoid StageSelect(string stageId)
    {
        if (_isLoading) return;
        StageData stage = _currentWorldData?.Stages.Find(s => s.StageId == stageId);
        if (stage == null)
        {
            Debug.LogError($"StageId {stageId} not found in the selected world.", this);
            return;
        }

        _isLoading = true;
        _currentStage = stage;
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
            _isLoading = false;
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
