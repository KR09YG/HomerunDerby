using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldTourMenuView : MonoBehaviour
{
    public enum MenuScreen { World, Stage }
    [SerializeField] private MenuScreen _screen;
    private const int WorldSlots = 6;
    private const int StageSlots = 8;
    private readonly Dictionary<string, Transform> _elements = new Dictionary<string, Transform>();
    private readonly List<WorldData> _worlds = new List<WorldData>();
    private WorldTourManager _manager;
    private WorldData _selectedWorld;
    private StageData _selectedStage;
    private int _stagePage;

    private void Start()
    {
        foreach (Transform item in GetComponentsInChildren<Transform>(true)) _elements[item.name] = item;
        _manager = WorldTourManager.Instance;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (_manager?.Config == null)
        {
            Text("Status", "ワールド設定を確認してください");
            Button("PrimaryAction").interactable = false;
            return;
        }
        foreach (WorldData world in _manager.Config.Worlds)
            if (!string.IsNullOrEmpty(world.WorldId) && world.Stages.Count > 0) _worlds.Add(world);
        _selectedWorld = _manager.CurrentWorld;
        Button("Back").onClick.AddListener(() =>
        {
            if (_screen == MenuScreen.World) _manager.OpenTitle();
            else _manager.OpenWorldSelect();
        });
        Button("PrimaryAction").onClick.AddListener(OnConfirm);
        if (_screen == MenuScreen.World)
        {
            for (int i = 0; i < WorldSlots; i++)
            {
                int index = i;
                Button("WorldCard" + i).onClick.AddListener(() => SelectWorld(index));
                Button("WorldPin" + i).onClick.AddListener(() => SelectWorld(index));
            }
            SelectWorld(Mathf.Max(0, _worlds.IndexOf(_selectedWorld)));
        }
        else
        {
            for (int i = 0; i < StageSlots; i++)
            {
                int slot = i;
                Button("StageNode" + i).onClick.AddListener(() => SelectStage(_stagePage * StageSlots + slot));
            }
            Button("PreviousPage").onClick.AddListener(() => ChangePage(-1));
            Button("NextPage").onClick.AddListener(() => ChangePage(1));
            int selectedIndex = _selectedWorld?.Stages.IndexOf(_manager.CurrentStage) ?? -1;
            if (selectedIndex < 0) selectedIndex = 0;
            _stagePage = selectedIndex / StageSlots;
            SelectStage(selectedIndex);
        }
        RefreshTotals();
    }

    private void OnConfirm()
    {
        if (_manager.IsLoading) return;
        if (_screen == MenuScreen.World && _selectedWorld != null)
        {
            _manager.WorldSelect(_selectedWorld.WorldId);
            _manager.OpenStageSelect();
        }
        else if (_selectedStage != null)
        {
            Button("PrimaryAction").interactable = false;
            Text("Status", "スタジアムへ移動しています…");
            _manager.PlayStage(_selectedStage.StageId);
        }
    }

    private void SelectWorld(int index)
    {
        if (index < 0 || index >= _worlds.Count || !_manager.IsWorldUnlocked(_worlds[index])) return;
        _selectedWorld = _worlds[index];
        _manager.WorldSelect(_selectedWorld.WorldId);
        for (int i = 0; i < WorldSlots; i++)
        {
            WorldData world = i < _worlds.Count ? _worlds[i] : null;
            bool available = world != null && _manager.IsWorldUnlocked(world);
            Button("WorldCard" + i).interactable = available;
            Button("WorldPin" + i).interactable = available;
            Text("WorldName" + i, world?.Name ?? "準備中");
            Text("WorldCount" + i, world == null ? "COMING SOON" : $"{WorldStars(world, _manager.SaveData)} / {world.Stages.Count * 3}");
            Text("PinName" + i, world?.Name ?? RegionName(i));
            Icon("WorldLock" + i, TourIconGraphic.Symbol.Lock, !available);
            Icon("PinIcon" + i, available ? TourIconGraphic.Symbol.Baseball : TourIconGraphic.Symbol.Lock, true);
            SetSelected("WorldCard" + i, world == _selectedWorld);
            for (int star = 0; star < 3; star++)
                SetStar($"Card{i}Star{star}", world != null && WorldStars(world, _manager.SaveData) > star);
        }
        Text("DetailEyebrow", $"WORLD {index + 1:00}");
        Text("DetailTitle", _selectedWorld.Name);
        Text("DetailSubtitle", "ホームランで世界を巡ろう");
        Text("WorldDescription", string.IsNullOrWhiteSpace(_selectedWorld.Description)
            ? "さまざまな球種とステージ条件に挑戦。\nホームランを放ち、スターを集めよう！" : _selectedWorld.Description);
        Text("DetailStageCount", _selectedWorld.Stages.Count.ToString());
        Text("DetailStarCount", $"{WorldStars(_selectedWorld, _manager.SaveData)} / {_selectedWorld.Stages.Count * 3}");
        Text("Status", "ワールドを選んで冒険を始めよう");
        Button("PrimaryAction").interactable = true;
    }

    private void SelectStage(int index)
    {
        if (_selectedWorld == null || index < 0 || index >= _selectedWorld.Stages.Count) return;
        _selectedStage = _selectedWorld.Stages[index];
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
            StageCondition condition = i < _selectedStage.StarObjectives.Count ? _selectedStage.StarObjectives[i] : null;
            Text("MissionText" + i, ConditionText(condition));
            Text("MissionState" + i, HasStar(progress?.EarnedStars, i) ? "獲得済み" : "未獲得");
            SetStar("MissionStar" + i, HasStar(progress?.EarnedStars, i));
            SetStar("DetailStar" + i, HasStar(progress?.EarnedStars, i));
        }
        Text("RecordValue", progress?.IsCleared == true ? "CLEAR" : "NOT CLEAR");
        Text("Status", unlocked ? "すべてのステージを最初から選べます" : "このステージは準備中です");
        Text("ActionTitle", unlocked ? "START STAGE" : "LOCKED");
        Button("PrimaryAction").interactable = unlocked;
    }

    private void RefreshStageNodes()
    {
        for (int slot = 0; slot < StageSlots; slot++)
        {
            int index = _stagePage * StageSlots + slot;
            StageData stage = index < _selectedWorld.Stages.Count ? _selectedWorld.Stages[index] : null;
            bool unlocked = stage != null && _manager.IsStageUnlocked(stage);
            _elements["StageNode" + slot].gameObject.SetActive(stage != null);
            Button("StageNode" + slot).interactable = stage != null;
            Text("NodeNumber" + slot, stage == null ? "—" : stage.IsBoss ? "BOSS" : (index + 1).ToString());
            Icon("NodeIcon" + slot, unlocked ? TourIconGraphic.Symbol.Baseball : TourIconGraphic.Symbol.Lock, true);
            StageProgressData progress = stage != null ? Progress(stage.StageId, _manager.SaveData) : null;
            for (int star = 0; star < 3; star++) SetStar($"Node{slot}Star{star}", HasStar(progress?.EarnedStars, star));
            SetSelected("StageNode" + slot, stage != null && stage == _selectedStage);
        }
        int visibleNodes = Mathf.Clamp(_selectedWorld.Stages.Count - _stagePage * StageSlots, 0, StageSlots);
        for (int segment = 0; segment < StageSlots - 1; segment++)
            foreach (KeyValuePair<string, Transform> element in _elements)
                if (element.Key.StartsWith($"Path{segment}Dot", System.StringComparison.Ordinal))
                    element.Value.gameObject.SetActive(segment < visibleNodes - 1);
        Button("PreviousPage").interactable = _stagePage > 0;
        Button("NextPage").interactable = (_stagePage + 1) * StageSlots < _selectedWorld.Stages.Count;
        Text("PageLabel", $"{_stagePage + 1} / {Mathf.Max(1, Mathf.CeilToInt(_selectedWorld.Stages.Count / (float)StageSlots))}");
    }

    private void ChangePage(int amount)
    {
        int page = _stagePage + amount;
        if (page < 0 || page * StageSlots >= _selectedWorld.Stages.Count) return;
        _stagePage = page;
        SelectStage(page * StageSlots);
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
    public static bool HasStar(bool[] stars, int index) => stars != null && index < stars.Length && stars[index];
    public static StageProgressData Progress(string id, PlayerSaveData saveData) =>
        saveData != null && saveData.Stages.TryGetValue(id, out StageProgressData value) ? value : null;
    public static int StarCount(bool[] stars)
    {
        int count = 0;
        if (stars != null) foreach (bool earned in stars) if (earned) count++;
        return count;
    }
    public static int WorldStars(WorldData world, PlayerSaveData saveData)
    {
        int count = 0;
        foreach (StageData stage in world.Stages) count += StarCount(Progress(stage.StageId, saveData)?.EarnedStars);
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
    private static string RegionName(int index) => new[] { "北アメリカ", "ヨーロッパ", "アジア", "南アメリカ", "アフリカ", "オセアニア" }[index];
}
