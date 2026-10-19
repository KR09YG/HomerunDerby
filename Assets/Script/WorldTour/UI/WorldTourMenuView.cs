using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldTourMenuView : MonoBehaviour
{
    public enum MenuScreen { World, Stage }
    [SerializeField] private MenuScreen _screen;
    private int _worldSlots;
    private int _stageSlots;
    private readonly Dictionary<string, Transform> _elements = new Dictionary<string, Transform>();
    private readonly List<WorldData> _worlds = new List<WorldData>();
    private readonly List<StageData> _stages = new List<StageData>();
    private WorldTourManager _manager;
    private WorldData _selectedWorld;
    private StageData _selectedStage;
    private int _stagePage;
    private int _worldPage;
    private Button _previousWorldPage;
    private Button _nextWorldPage;

    private void Start()
    {
        foreach (Transform item in GetComponentsInChildren<Transform>(true)) _elements[item.name] = item;
        _manager = WorldTourManager.Instance;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (_manager == null)
        {
            Text("Status", "ワールド設定を確認してください");
            Button("PrimaryAction").interactable = false;
            return;
        }
        while (_elements.ContainsKey("WorldCard" + _worldSlots)) _worldSlots++;
        while (_elements.ContainsKey("StageNode" + _stageSlots)) _stageSlots++;
        _manager.StateChanged += RefreshMenu;
        Button("Back").onClick.AddListener(() =>
        {
            if (_screen == MenuScreen.World) _manager.OpenTitle();
            else _manager.OpenWorldSelect();
        });
        Button("PrimaryAction").onClick.AddListener(OnConfirm);
        if (_screen == MenuScreen.World)
        {
            for (int i = 0; i < _worldSlots; i++)
            {
                int index = i;
                Button("WorldCard" + i).onClick.AddListener(() => SelectWorld(_worldPage * _worldSlots + index));
                Button("WorldPin" + i).onClick.AddListener(() => SelectWorld(_worldPage * _worldSlots + index));
            }
        }
        else
        {
            for (int i = 0; i < _stageSlots; i++)
            {
                int slot = i;
                Button("StageNode" + i).onClick.AddListener(() => SelectStage(_stagePage * _stageSlots + slot));
            }
            Button("PreviousPage").onClick.AddListener(() => ChangePage(-1));
            Button("NextPage").onClick.AddListener(() => ChangePage(1));
            Text("LegendLocked", "未開放 / クリアで次へ");
        }
        RefreshMenu();
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.StateChanged -= RefreshMenu;
    }

    private void RefreshMenu()
    {
        if (!_manager.IsConfigReady)
        {
            bool failed = _manager.ConfigState == WorldTourConfigState.ConnectionError || _manager.ConfigState == WorldTourConfigState.InvalidData;
            Text("Status", failed ? _manager.ConfigError : "CloudFrontから設定を取得しています…");
            Text("DetailTitle", failed ? "設定の取得に失敗しました" : "読み込み中");
            Text("DetailSubtitle", failed ? "再試行ボタンで取得し直せます" : "通信完了までお待ちください");
            if (_screen == MenuScreen.World) Text("PrimaryActionTitle", failed ? "再試行" : "取得中…");
            else
            {
                Text("ActionTitle", failed ? "RETRY" : "LOADING");
                Text("ActionSubtitle", failed ? "設定を取得し直す" : "設定を取得しています");
            }
            Button("PrimaryAction").interactable = failed;
            foreach (Button button in GetComponentsInChildren<Button>(true))
                if (button != Button("PrimaryAction") && button != Button("Back")) button.interactable = false;
            foreach (var element in _elements)
                if (element.Key.StartsWith("WorldCard", System.StringComparison.Ordinal) ||
                    element.Key.StartsWith("WorldPin", System.StringComparison.Ordinal) ||
                    element.Key.StartsWith("PinName", System.StringComparison.Ordinal) ||
                    element.Key.StartsWith("StageNode", System.StringComparison.Ordinal) ||
                    element.Key.StartsWith("Path", System.StringComparison.Ordinal)) element.Value.gameObject.SetActive(false);
            return;
        }
        if (_screen == MenuScreen.World) Text("PrimaryActionTitle", "このワールドを選ぶ");
        _worlds.Clear();
        _worlds.AddRange(_manager.Worlds);
        _selectedWorld = _manager.CurrentWorld;
        _stages.Clear();
        _stages.AddRange(_manager.GetStages(_selectedWorld));
        if (_screen == MenuScreen.World && _worldSlots > 0 && _worlds.Count > 0)
        {
            int index = Mathf.Max(0, _worlds.IndexOf(_selectedWorld));
            _worldPage = index / _worldSlots;
            SelectWorld(index);
            RefreshWorldPager();
        }
        else if (_screen == MenuScreen.Stage && _stageSlots > 0 && _stages.Count > 0)
        {
            int index = Mathf.Max(0, _stages.IndexOf(_manager.CurrentStage));
            _stagePage = index / _stageSlots;
            SelectStage(index);
        }
        else
        {
            _selectedStage = null;
            Text("Status", "選択できる設定がありません");
            Button("PrimaryAction").interactable = false;
            for (int i = 0; i < _worldSlots; i++)
            {
                _elements["WorldCard" + i].gameObject.SetActive(false);
                _elements["WorldPin" + i].gameObject.SetActive(false);
                _elements["PinName" + i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _stageSlots; i++) _elements["StageNode" + i].gameObject.SetActive(false);
            foreach (var element in _elements)
                if (element.Key.StartsWith("Path", System.StringComparison.Ordinal)) element.Value.gameObject.SetActive(false);
        }
        RefreshTotals();
    }

    private void OnConfirm()
    {
        if (_manager.IsLoading) return;
        if (!_manager.IsConfigReady)
        {
            _manager.RetryConfigDownload();
            return;
        }
        if (_screen == MenuScreen.World && _manager.IsWorldUnlocked(_selectedWorld))
        {
            _manager.WorldSelect(_selectedWorld.WorldId);
            _manager.OpenStageSelect();
        }
        else if (_screen == MenuScreen.Stage && _manager.IsStageUnlocked(_selectedStage))
        {
            _manager.PlayStage(_selectedStage.StageId);
        }
    }

    private void SelectWorld(int index)
    {
        if (index < 0 || index >= _worlds.Count) return;
        _selectedWorld = _worlds[index];
        for (int i = 0; i < _worldSlots; i++)
        {
            int worldIndex = _worldPage * _worldSlots + i;
            WorldData world = worldIndex < _worlds.Count ? _worlds[worldIndex] : null;
            bool available = world != null && _manager.IsWorldUnlocked(world);
            _elements["WorldCard" + i].gameObject.SetActive(world != null);
            _elements["WorldPin" + i].gameObject.SetActive(world != null);
            _elements["PinName" + i].gameObject.SetActive(world != null);
            Button("WorldCard" + i).interactable = world != null;
            Button("WorldPin" + i).interactable = world != null;
            Text("CardNumber" + i, $"WORLD {worldIndex + 1}");
            Text("WorldName" + i, world?.Name);
            Text("WorldCount" + i, world == null ? "" :
                $"{(_manager.IsWorldCleared(world) ? "CLEAR " : "")}{WorldStars(world, _manager.SaveData)} / {_manager.GetStages(world).Count * 3}");
            Text("PinName" + i, world?.Name);
            Icon("WorldLock" + i, TourIconGraphic.Symbol.Lock, !available);
            Icon("PinIcon" + i, available ? TourIconGraphic.Symbol.Baseball : TourIconGraphic.Symbol.Lock, true);
            SetSelected("WorldCard" + i, world == _selectedWorld);
            for (int star = 0; star < 3; star++)
            {
                bool allEarned = world != null && _manager.GetStages(world).Count > 0;
                foreach (StageData stage in _manager.GetStages(world))
                    allEarned &= HasStar(Progress(stage.StageId, _manager.SaveData)?.EarnedStars, star);
                SetStar($"Card{i}Star{star}", allEarned);
            }
        }
        Text("DetailEyebrow", $"WORLD {index + 1:00}");
        Text("DetailTitle", _selectedWorld.Name);
        Text("DetailSubtitle", _manager.IsWorldCleared(_selectedWorld) ? "WORLD CLEAR" :
            _manager.IsWorldUnlocked(_selectedWorld) ? "挑戦可能" : "前のワールドのボスをクリアすると開放");
        Text("WorldDescription", string.IsNullOrWhiteSpace(_selectedWorld.Description)
            ? "さまざまな球種とステージ条件に挑戦。\nホームランを放ち、スターを集めよう！" : _selectedWorld.Description);
        Text("DetailStageCount", _manager.GetStages(_selectedWorld).Count.ToString());
        Text("DetailStarCount", $"{WorldStars(_selectedWorld, _manager.SaveData)} / {_manager.GetStages(_selectedWorld).Count * 3}");
        Text("Status", $"ワールドを選んで冒険を始めよう  {_worldPage + 1} / {Mathf.Max(1, Mathf.CeilToInt(_worlds.Count / (float)_worldSlots))}");
        Button("PrimaryAction").interactable = _manager.IsWorldUnlocked(_selectedWorld);
    }

    private void SelectStage(int index)
    {
        if (_selectedWorld == null || index < 0 || index >= _stages.Count) return;
        _selectedStage = _stages[index];
        RefreshStageNodes();
        bool unlocked = _manager.IsStageUnlocked(_selectedStage);
        Text("WorldHeading", _selectedWorld.Name);
        Text("DetailEyebrow", _selectedStage.IsBoss ? "BOSS STAGE" : $"STAGE {index + 1:00}");
        Text("DetailTitle", _selectedStage.Name);
        Text("DetailSubtitle", $"{_selectedStage.BallCount}球で挑戦  /  STAGE {_selectedStage.StageId}");
        Text("ClearCondition", "クリア条件：" + ConditionText(_selectedStage.ClearObjective));
        StageProgressData progress = Progress(_selectedStage.StageId, _manager.SaveData);
        for (int i = 0; i < 3; i++)
        {
            StageCondition condition = _selectedStage.StarObjectives != null && i < _selectedStage.StarObjectives.Count ? _selectedStage.StarObjectives[i] : null;
            Text("MissionText" + i, ConditionText(condition));
            Text("MissionState" + i, HasStar(progress?.EarnedStars, i) ? "獲得済み" : "未獲得");
            SetStar("MissionStar" + i, HasStar(progress?.EarnedStars, i));
            SetStar("DetailStar" + i, HasStar(progress?.EarnedStars, i));
        }
        Text("RecordValue", progress?.IsCleared == true ? "CLEAR" : "NOT CLEAR");
        Text("Status", unlocked ? (progress?.IsCleared == true ? "クリア済みのステージに再挑戦できます" : "このステージに挑戦できます") : "前のステージをクリアすると開放されます");
        Text("ActionTitle", unlocked ? "START STAGE" : "LOCKED");
        Button("PrimaryAction").interactable = unlocked;
    }

    private void RefreshStageNodes()
    {
        for (int slot = 0; slot < _stageSlots; slot++)
        {
            int index = _stagePage * _stageSlots + slot;
            StageData stage = index < _stages.Count ? _stages[index] : null;
            bool unlocked = stage != null && _manager.IsStageUnlocked(stage);
            _elements["StageNode" + slot].gameObject.SetActive(stage != null);
            Button("StageNode" + slot).interactable = stage != null;
            StageProgressData progress = stage != null ? Progress(stage.StageId, _manager.SaveData) : null;
            string number = stage?.IsBoss == true ? "BOSS" : (index + 1).ToString();
            Text("NodeNumber" + slot, stage == null ? "—" : progress?.IsCleared == true ? number + " CLEAR" : number);
            Icon("NodeIcon" + slot, !unlocked ? TourIconGraphic.Symbol.Lock : stage.IsBoss ? TourIconGraphic.Symbol.Crown : TourIconGraphic.Symbol.Baseball, true);
            for (int star = 0; star < 3; star++) SetStar($"Node{slot}Star{star}", HasStar(progress?.EarnedStars, star));
            SetSelected("StageNode" + slot, stage != null && stage == _selectedStage);
        }
        int visibleNodes = Mathf.Clamp(_stages.Count - _stagePage * _stageSlots, 0, _stageSlots);
        for (int segment = 0; segment < _stageSlots - 1; segment++)
        {
            int index = _stagePage * _stageSlots + segment;
            bool connected = segment < visibleNodes - 1 &&
                _manager.GetNextStage(_stages[index]) == _stages[index + 1];
            foreach (KeyValuePair<string, Transform> element in _elements)
                if (element.Key.StartsWith($"Path{segment}Dot", System.StringComparison.Ordinal))
                    element.Value.gameObject.SetActive(connected);
        }
        Button("PreviousPage").interactable = _stagePage > 0;
        Button("NextPage").interactable = (_stagePage + 1) * _stageSlots < _stages.Count;
        Text("PageLabel", $"{_stagePage + 1} / {Mathf.Max(1, Mathf.CeilToInt(_stages.Count / (float)_stageSlots))}");
    }

    private void ChangePage(int amount)
    {
        int page = _stagePage + amount;
        if (page < 0 || page * _stageSlots >= _stages.Count) return;
        _stagePage = page;
        SelectStage(page * _stageSlots);
    }

    private void RefreshWorldPager()
    {
        bool paged = _worlds.Count > _worldSlots;
        if (paged && _previousWorldPage == null)
        {
            _previousWorldPage = ClonePageButton("WorldPreviousPage", "<", 1070, () => ChangeWorldPage(-1));
            _nextWorldPage = ClonePageButton("WorldNextPage", ">", 1160, () => ChangeWorldPage(1));
            ((RectTransform)_elements["Status"]).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 680);
        }
        if (_previousWorldPage == null) return;
        _previousWorldPage.gameObject.SetActive(paged);
        _nextWorldPage.gameObject.SetActive(paged);
        _previousWorldPage.interactable = _worldPage > 0;
        _nextWorldPage.interactable = (_worldPage + 1) * _worldSlots < _worlds.Count;
    }

    private Button ClonePageButton(string name, string label, float x, UnityEngine.Events.UnityAction action)
    {
        Button button = Instantiate(Button("Back"), _elements["Back"].parent);
        button.name = name;
        RectTransform rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(x, -985);
        rect.sizeDelta = new Vector2(60, 60);
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].gameObject.SetActive(i == 0);
            if (i == 0)
            {
                labels[i].text = label;
                labels[i].rectTransform.anchoredPosition = Vector2.zero;
                labels[i].rectTransform.sizeDelta = rect.sizeDelta;
            }
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        return button;
    }

    private void ChangeWorldPage(int amount)
    {
        int page = _worldPage + amount;
        if (page < 0 || page * _worldSlots >= _worlds.Count) return;
        _worldPage = page;
        SelectWorld(page * _worldSlots);
        RefreshWorldPager();
    }

    private void RefreshTotals()
    {
        int count = 0, total = 0;
        foreach (WorldData world in _worlds) { count += WorldStars(world, _manager.SaveData); total += world.Stages.Count * 3; }
        Text("TotalStars", $"{count} / {total}");
        Text("TotalPoints", _manager.SaveData.Points.ToString("N0"));
    }

    private void Text(string name, string value)
    {
        if (_elements.TryGetValue(name, out Transform element)) element.GetComponent<TMP_Text>().text = value ?? "—";
    }
    private Button Button(string name) => _elements[name].GetComponent<Button>();
    private void SetSelected(string name, bool selected)
    {
        TourPanelGraphic panel = _elements[name].GetComponent<TourPanelGraphic>();
        panel.BorderColor = selected ? TourUI.Gold : TourUI.Cyan;
        panel.BorderWidth = selected ? 5f : 2f;
        panel.SetVerticesDirty();
    }
    private void Icon(string name, TourIconGraphic.Symbol symbol, bool visible)
    {
        TourIconGraphic icon = _elements[name].GetComponent<TourIconGraphic>();
        icon.Shape = symbol;
        icon.gameObject.SetActive(visible);
        icon.SetVerticesDirty();
    }
    private void SetStar(string name, bool lit)
    {
        TourIconGraphic icon = _elements[name].GetComponent<TourIconGraphic>();
        icon.Lit = lit;
        icon.SetVerticesDirty();
    }
    public static bool HasStar(bool[] stars, int index) => stars != null && index >= 0 && index < stars.Length && stars[index];
    public static StageProgressData Progress(string id, PlayerSaveData saveData) =>
        id != null && saveData?.Stages != null && saveData.Stages.TryGetValue(id, out StageProgressData value) ? value : null;
    public static int StarCount(bool[] stars)
    {
        int count = 0;
        if (stars != null) for (int i = 0; i < Mathf.Min(3, stars.Length); i++) if (stars[i]) count++;
        return count;
    }
    public static int WorldStars(WorldData world, PlayerSaveData saveData)
    {
        int count = 0;
        if (world?.Stages != null)
            foreach (StageData stage in world.Stages)
                if (stage != null) count += StarCount(Progress(stage.StageId, saveData)?.EarnedStars);
        return count;
    }
    public static string ConditionText(StageCondition condition)
    {
        if (condition == null) return "条件未設定";
        string target = condition.TargetValue.ToString("0.#");
        switch (condition.Type)
        {
            case ChallengeConditionType.HomeRunCount: return $"ホームランを{target}本打とう";
            case ChallengeConditionType.MaxHomeRunDistance: return $"{target}m以上のホームランを打とう";
            case ChallengeConditionType.TotalFairDistance: return $"総フェア飛距離{target}mを目指そう";
            case ChallengeConditionType.DirectionalHomeRunCount:
                string direction = condition.Direction == HomeRunDirection.Left ? "レフト" : condition.Direction == HomeRunDirection.Right ? "ライト" : "センター";
                return $"{direction}へホームランを{target}本打とう";
            default: return "条件未設定";
        }
    }
}
