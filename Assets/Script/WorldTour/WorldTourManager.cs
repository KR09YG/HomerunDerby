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
    private SaveTest _saveTest;
    private WorldUnlock _progress;

    public event Action StateChanged;

    public PlayerSaveData SaveData => _saveData;
    public WorldTourConfig Config => _config;
    public WorldData CurrentWorld => _currentWorldData;
    public StageData CurrentStage => _currentStage;
    public bool IsLoading => _isLoading;
    public IReadOnlyList<WorldData> Worlds => Progress.Worlds;

    private WorldUnlock Progress
    {
        get
        {
            if (_progress == null)
            {
                _progress = new WorldUnlock(message => Debug.LogWarning(message, this));
                _progress.SetConfig(_config);
                _progress.SetSaveData(_saveData);
            }
            return _progress;
        }
    }

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

        _saveTest = GetComponent<SaveTest>();
        if (_saveTest == null) _saveTest = gameObject.AddComponent<SaveTest>();

        if (_worldTourConfig == null)
        {
            Debug.LogError("[WorldTour] ワールド設定が未設定です。", this);
            return;
        }
        if (_worldTourConfig.Config == null)
        {
            Debug.LogError("[WorldTour] ワールド設定の内容が空です。", this);
            return;
        }
        SetData(_worldTourConfig.Config);
        // 従来の確認用シーンだけは、初期ステージを直接開始できるようにする。
        if (SceneManager.GetActiveScene().name == "test_world_tour" && GetStages(_currentWorldData).Count > 0)
            PlayStage(GetStages(_currentWorldData)[0].StageId);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= StopDerbyManager;
        if (Instance == this) Instance = null;
    }

    public void SetData(WorldTourConfig config)
    {
        if (_isLoading)
        {
            Debug.LogWarning("[WorldTour] シーン移動中は設定を差し替えられません。", this);
            return;
        }
        string worldId = _currentWorldData?.WorldId;
        string stageId = _currentStage?.StageId;
        _config = config;
        _progress = new WorldUnlock(message => Debug.LogWarning(message, this));
        _progress.SetConfig(config);
        RefreshSelection(worldId, stageId);
        StateChanged?.Invoke();
    }

    /// <summary>復元した進捗を共有し、選択中の設定参照も更新する。</summary>
    public void SetSaveData(PlayerSaveData saveData)
    {
        // プレイ中はInGameManagerも同じインスタンスを参照している。
        if (_isLoading || string.Equals(SceneManager.GetActiveScene().name, "Homerun", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[WorldTour] 進捗の復元はステージ開始前に行ってください。", this);
            return;
        }
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        Progress.SetSaveData(_saveData);
        RefreshSelection(_currentWorldData?.WorldId, _currentStage?.StageId);
        StateChanged?.Invoke();
    }

    [ContextMenu("仮セーブから進捗を復元")]
    public void RestoreProgress()
    {
        PlayerSaveDataDto dto = _saveTest != null ? _saveTest.LoadData() : null;
        if (dto != null) SetSaveData(PlayerSaveDataConverter.FromDto(dto));
    }

    private void RefreshSelection(string worldId, string stageId)
    {
        Progress.SetSaveData(_saveData);
        _currentWorldData = Progress.FindWorld(worldId);
        if (!Progress.IsWorldUnlocked(_currentWorldData))
            _currentWorldData = Worlds.Count > 0 ? Worlds[0] : null;
        _currentStage = null;
        foreach (StageData stage in GetStages(_currentWorldData))
            if (stage.StageId == stageId && IsStageUnlocked(stage)) _currentStage = stage;
    }

    public void SetStageProgress(StageSessionData session, int newStarCount)
    {
        if (session == null || !session.IsFinished || _currentStage?.StageId != session.StageId) return;
        // 進捗のマージはInGameManagerで完了している。ここでは保存と画面への通知だけ行う。
        Debug.Log($"[WorldTour] 新たに獲得した星: {newStarCount}", this);
        PlayerSaveDataDto saveData = PlayerSaveDataConverter.ToDto(_saveData);
        if (_saveTest != null) _saveTest.SaveData(saveData);
        StateChanged?.Invoke();
    }

    public void WorldSelect(string worldId)
    {
        WorldData world = Progress.FindWorld(worldId);
        if (world == null || !IsWorldUnlocked(world) || _isLoading) return;
        if (_currentWorldData != world) _currentStage = null;
        _currentWorldData = world;
    }

    public bool IsWorldUnlocked(WorldData world)
    {
        return Progress.IsWorldUnlocked(world);
    }

    public bool IsStageUnlocked(StageData stage)
    {
        return Progress.IsStageUnlocked(stage);
    }

    public bool IsWorldCleared(WorldData world) => Progress.IsWorldCleared(world);
    public IReadOnlyList<StageData> GetStages(WorldData world) => Progress.GetStages(world);

    public void PlayStage(string stageId)
    {
        StageData stage = FindSelectedStage(stageId);
        if (IsStageUnlocked(stage)) StageSelect(stageId).Forget();
    }

    public StageData GetNextStage()
    {
        return Progress.GetNextStage(_currentStage);
    }

    public StageData GetNextStage(StageData stage) => Progress.GetNextStage(stage);

    public void PlayNextStage()
    {
        StageData next = GetNextStage();
        if (!IsStageUnlocked(next)) return;
        WorldSelect(Progress.Owner(next).WorldId);
        PlayStage(next.StageId);
    }

    private StageData FindSelectedStage(string id)
    {
        foreach (StageData stage in GetStages(_currentWorldData))
            if (stage.StageId == id) return stage;
        return null;
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
        StageData stage = FindSelectedStage(stageId);
        if (stage == null || !IsStageUnlocked(stage))
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
