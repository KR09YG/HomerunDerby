using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KanKikuchi.AudioManager;

public sealed class WorldTourMenuView : MonoBehaviour
{
    public enum MenuScreen { World, Stage }
    [SerializeField] private MenuScreen _screen;
    [SerializeField, Range(0f, 1f)] private float _bgmVolumeRate = 0.3f;
    private int _worldSlots;
    private int _stageSlots;
    private readonly Dictionary<string, Transform> _elements = new Dictionary<string, Transform>();
    private readonly List<WorldData> _worlds = new List<WorldData>();
    private readonly List<StageData> _stages = new List<StageData>();
    private WorldTourManager _manager;
    private WorldData _selectedWorld;
    private StageData _selectedStage;
    private readonly List<Vector2> _stagePattern = new List<Vector2>();
    private readonly List<List<TourIconGraphic>> _stagePaths = new List<List<TourIconGraphic>>();
    private ScrollRect _stageScroll;
    private RectTransform _stageContent;
    private float _stagePatternHeight;
    private const float StageMapPadding = 44f;
    private const float StagePatternGap = 80f;
    private const float PathDotSpacing = 25f;
    private const float PathDotSize = 13f;
    private int _pinSlots;
    private ScrollRect _worldScroll;
    private RectTransform _worldContent;
    private readonly Dictionary<int, GameObject> _comingSoon = new Dictionary<int, GameObject>();
    private float _cardWidth;
    private float _cardHeight;
    private float _cardStride;
    private WorldTourImageLoader _images;
    private WorldTourScreenFade _screenFade;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private int _revealRevision;

    private void Awake() => _screenFade = WorldTourScreenFade.Create(transform, "読み込み中…");

    private void Start()
    {
        string bgmPath = _screen == MenuScreen.World
            ? "BGM/racing_game_menu_bpm165" : "BGM/Funked Up";
        string bgmName = _screen == MenuScreen.World
            ? "racing_game_menu_bpm165" : "Funked Up";
        BGMManager bgm = BGMManager.Instance;
        if (!bgm.GetCurrentAudioNames().Contains(bgmName))
            bgm.Play(bgmPath, volumeRate: _bgmVolumeRate);

        foreach (Transform item in GetComponentsInChildren<Transform>(true)) _elements[item.name] = item;
        _manager = WorldTourManager.Instance;
        _images = GetComponent<WorldTourImageLoader>();
        if (_images == null) _images = gameObject.AddComponent<WorldTourImageLoader>();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (_manager == null)
        {
            Text("Status", "ワールド設定を確認してください");
            Button("PrimaryAction").interactable = false;
            RevealMenu();
            return;
        }
        while (_elements.ContainsKey("WorldCard" + _worldSlots)) _worldSlots++;
        while (_elements.ContainsKey("StageNode" + _stageSlots)) _stageSlots++;
        while (_elements.ContainsKey("WorldPin" + _pinSlots)) _pinSlots++;
        _manager.StateChanged += RefreshMenu;
        Button("Back").onClick.AddListener(() =>
        {
            if (_screen == MenuScreen.World) _manager.OpenTitle();
            else _manager.OpenWorldSelect();
        });
        Button("PrimaryAction").onClick.AddListener(OnConfirm);
        if (_screen == MenuScreen.World)
        {
            BuildWorldScroll();
            for (int i = 0; i < _pinSlots; i++)
            {
                int index = i;
                Button("WorldPin" + i).onClick.AddListener(() => { SelectWorld(index); RevealWorld(index); });
            }
        }
        else
        {
            BuildStageScroll();
            Text("LegendLocked", "未開放 / クリアで次へ");
        }
        RefreshMenu();
    }

    private void OnDestroy()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        if (_manager != null) _manager.StateChanged -= RefreshMenu;
    }

    private void RefreshMenu()
    {
        RevealMenu();
        if (!_manager.IsConfigReady)
        {
            foreach (var path in _stagePaths)
                foreach (var dot in path) dot.gameObject.SetActive(false);
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
        if (_screen == MenuScreen.World) EnsureWorldCards(_worlds.Count + 1);
        _selectedWorld = _manager.CurrentWorld;
        _stages.Clear();
        _stages.AddRange(_manager.GetStages(_selectedWorld));
        if (_screen == MenuScreen.Stage) EnsureStageNodes(_stages.Count);
        if (_screen == MenuScreen.World && _worldSlots > 0 && _worlds.Count > 0)
        {
            int index = Mathf.Max(0, _worlds.IndexOf(_selectedWorld));
            SelectWorld(index);
            RevealWorld(index);
        }
        else if (_screen == MenuScreen.Stage && _stageSlots > 0 && _stages.Count > 0)
        {
            int index = _stages.IndexOf(_manager.CurrentStage);
            if (index < 0 || !_manager.IsStageUnlocked(_stages[index]))
                index = _stages.FindIndex(stage => _manager.IsStageUnlocked(stage));
            if (index < 0)
            {
                ClearStageSelection();
                RefreshTotals();
                return;
            }
            SelectStage(index);
            RevealStage(index);
        }
        else
        {
            _selectedStage = null;
            Text("Status", "選択できる設定がありません");
            Button("PrimaryAction").interactable = false;
            for (int i = 0; i < _worldSlots; i++) _elements["WorldCard" + i].gameObject.SetActive(false);
            RefreshWorldPins();
            if (_screen == MenuScreen.World && _worldContent != null) ShowComingSoon(_worlds.Count);
            for (int i = 0; i < _stageSlots; i++) _elements["StageNode" + i].gameObject.SetActive(false);
            foreach (var path in _stagePaths)
                foreach (var dot in path) dot.gameObject.SetActive(false);
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
        if (index < 0 || index >= _worlds.Count || !_manager.IsWorldUnlocked(_worlds[index])) return;
        RevealMenu();
        _selectedWorld = _worlds[index];
        for (int i = 0; i < _worldSlots; i++)
        {
            int worldIndex = i;
            WorldData world = worldIndex < _worlds.Count ? _worlds[worldIndex] : null;
            bool available = world != null && _manager.IsWorldUnlocked(world);
            _elements["WorldCard" + i].gameObject.SetActive(world != null);

            SetAvailable("WorldCard" + i, available);

            Text("CardNumber" + i, $"WORLD {worldIndex + 1}");
            Text("WorldName" + i, world?.Name);
            Picture("CardArt" + i, world?.ImageUrl);
            TMP_Text count = _elements["WorldCount" + i].GetComponent<TMP_Text>();
            count.enabled = true;
            count.gameObject.SetActive(world != null);
            count.text = world == null ? "" : $"{WorldStars(world, _manager.SaveData)} / {_manager.GetStages(world).Count * 3}";
            Icon("WorldLock" + i, TourIconGraphic.Symbol.Lock, !available);

            bool selected = world != null && world == _selectedWorld;
            SetWorldSelected("WorldCard" + i, selected);
            if (_comingSoon.TryGetValue(i, out GameObject overlay)) overlay.SetActive(false);
            _elements["CardNumber" + i].GetComponent<TMP_Text>().color = selected ? TourUI.Gold : Color.white;
            for (int star = 0; star < 3; star++)
                _elements[$"Card{i}Star{star}"].gameObject.SetActive(false);
        }
        RefreshWorldPins();
        ShowComingSoon(_worlds.Count);
        Text("DetailEyebrow", $"WORLD {index + 1:00}");
        Text("DetailTitle", _selectedWorld.Name);
        Picture("Background", _selectedWorld.ImageUrl);
        // WorldMapFocus keeps its scene-authored local map texture and UV rect.
        Picture("DetailArt", _selectedWorld.ImageUrl);
        Text("DetailSubtitle", _manager.IsWorldCleared(_selectedWorld) ? "WORLD CLEAR" :
            _manager.IsWorldUnlocked(_selectedWorld) ? "挑戦可能" : "前のワールドのボスをクリアすると開放");
        Text("WorldDescription", string.IsNullOrWhiteSpace(_selectedWorld.Description)
            ? "さまざまな球種とステージ条件に挑戦。\nホームランを放ち、スターを集めよう！" : _selectedWorld.Description);
        Text("DetailStageCount", _manager.GetStages(_selectedWorld).Count.ToString());
        Text("DetailStarCount", $"{WorldStars(_selectedWorld, _manager.SaveData)} / {_manager.GetStages(_selectedWorld).Count * 3}");
        Text("Status", "ワールドを選んで冒険を始めよう ／ 左右にドラッグしてスクロール");
        Button("PrimaryAction").interactable = _manager.IsWorldUnlocked(_selectedWorld);
    }

    private void SelectStage(int index)
    {
        if (_selectedWorld == null || index < 0 || index >= _stages.Count || !_manager.IsStageUnlocked(_stages[index])) return;
        RevealMenu();
        _selectedStage = _stages[index];
        RefreshStageNodes();
        bool unlocked = _manager.IsStageUnlocked(_selectedStage);
        Text("WorldHeading", _selectedWorld.Name);
        Text("DetailEyebrow", _selectedStage.IsBoss ? "BOSS STAGE" : $"STAGE {index + 1:00}");
        Text("DetailTitle", _selectedStage.Name);
        string imageUrl = string.IsNullOrWhiteSpace(_selectedStage.ImageUrl) ? _selectedWorld.ImageUrl : _selectedStage.ImageUrl;
        Picture("Background", imageUrl);
        Picture("DetailArt", imageUrl);
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
        Text("Status", unlocked ? "ステージを選択 ／ 上下にドラッグ・スクロール" : "前のステージをクリアすると開放されます");
        Text("ActionTitle", unlocked ? "START STAGE" : "LOCKED");
        Text("ActionSubtitle", unlocked ? "このステージをプレイ" : "前のステージをクリアすると開放");
        Button("PrimaryAction").interactable = unlocked;
    }

    private void RefreshStageNodes()
    {
        for (int slot = 0; slot < _stageSlots; slot++)
        {
            int index = slot;
            StageData stage = index < _stages.Count ? _stages[index] : null;
            bool unlocked = stage != null && _manager.IsStageUnlocked(stage);
            _elements["StageNode" + slot].gameObject.SetActive(stage != null);
            SetAvailable("StageNode" + slot, unlocked);
            StageProgressData progress = stage != null ? Progress(stage.StageId, _manager.SaveData) : null;
            string number = stage?.IsBoss == true ? "BOSS" : (index + 1).ToString();
            Text("NodeNumber" + slot, stage == null ? "—" : progress?.IsCleared == true ? number + " CLEAR" : number);
            Icon("NodeIcon" + slot, !unlocked ? TourIconGraphic.Symbol.Lock : stage.IsBoss ? TourIconGraphic.Symbol.Crown : TourIconGraphic.Symbol.Baseball, stage != null);
            for (int star = 0; star < 3; star++) SetStar($"Node{slot}Star{star}", HasStar(progress?.EarnedStars, star));
            SetSelected("StageNode" + slot, stage != null && stage == _selectedStage);
        }
        for (int segment = 0; segment < _stagePaths.Count; segment++)
        {
            bool connected = segment + 1 < _stages.Count &&
                _manager.GetNextStage(_stages[segment]) == _stages[segment + 1];
            foreach (TourIconGraphic dot in _stagePaths[segment]) dot.gameObject.SetActive(connected);
        }
    }

    private void BuildStageScroll()
    {
        if (_stageSlots == 0) return;
        RectTransform first = (RectTransform)_elements["StageNode0"];
        float minX = first.anchoredPosition.x;
        float maxX = minX + first.sizeDelta.x;
        float minY = -first.anchoredPosition.y;
        float maxY = minY;
        for (int i = 0; i < _stageSlots; i++)
        {
            RectTransform node = (RectTransform)_elements["StageNode" + i];
            Vector2 position = new Vector2(node.anchoredPosition.x, -node.anchoredPosition.y);
            _stagePattern.Add(position);
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x + node.sizeDelta.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
        }
        Vector2 origin = new Vector2(minX - StageMapPadding, minY - StageMapPadding);
        float width = maxX - minX + StageMapPadding * 2f;
        float bottom = -((RectTransform)_elements["StageLegend"]).anchoredPosition.y - 8f;
        float height = bottom - origin.y;
        _stagePatternHeight = maxY - minY + first.sizeDelta.y + StagePatternGap;
        RectTransform viewport = TourUI.Rect(first.parent, "StageMapViewport", origin.x, origin.y, width, height);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        _stageContent = TourUI.Rect(viewport, "StageMapContent", 0, 0, width, height);
        _stageScroll = viewport.gameObject.AddComponent<ScrollRect>();
        _stageScroll.viewport = viewport;
        _stageScroll.content = _stageContent;
        _stageScroll.horizontal = false;
        _stageScroll.vertical = true;
        _stageScroll.movementType = ScrollRect.MovementType.Clamped;
        _stageScroll.scrollSensitivity = 65f;
        for (int i = 0; i < _stagePattern.Count; i++)
        {
            _stagePattern[i] -= origin;
            _elements["StageNode" + i].SetParent(_stageContent, false);
            BindStageNode(i);
        }
        // 配置済みの点線は使わず、追加されたステージを含めて接続を描き直す。
        foreach (var element in _elements)
            if (element.Key.StartsWith("Path", System.StringComparison.Ordinal)) element.Value.gameObject.SetActive(false);
        foreach (string name in new[] { "PreviousPage", "NextPage", "PageLabel" })
            if (_elements.TryGetValue(name, out Transform element)) element.gameObject.SetActive(false);
    }

    private void BindStageNode(int index)
    {
        Button button = Button("StageNode" + index);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectStage(index));
    }

    private Vector2 StagePosition(int index)
    {
        Vector2 position = _stagePattern[index % _stagePattern.Count];
        position.y += index / _stagePattern.Count * _stagePatternHeight;
        return position;
    }

    private void EnsureStageNodes(int required)
    {
        if (_stageContent == null) return;
        while (_stageSlots < required)
        {
            int index = _stageSlots;
            int templateIndex = Mathf.Min(1, _stagePattern.Count - 1);
            Transform clone = Instantiate(_elements["StageNode" + templateIndex], _stageContent);
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            {
                string name = child == clone ? "StageNode" + index : child.name;
                if (child != clone)
                {
                    string starPrefix = "Node" + templateIndex + "Star";
                    if (name.StartsWith(starPrefix, System.StringComparison.Ordinal))
                        name = "Node" + index + "Star" + name.Substring(starPrefix.Length);
                    else if (name == "StageNode" + templateIndex + "Title") name = "StageNode" + index + "Title";
                    else if (name.EndsWith(templateIndex.ToString(), System.StringComparison.Ordinal))
                        name = name.Substring(0, name.Length - templateIndex.ToString().Length) + index;
                }
                child.name = name;
                _elements[name] = child;
            }
            BindStageNode(index);
            _stageSlots++;
        }
        float contentHeight = _stageScroll.viewport.sizeDelta.y;
        for (int i = 0; i < _stageSlots; i++)
        {
            RectTransform node = (RectTransform)_elements["StageNode" + i];
            Vector2 position = StagePosition(i);
            node.anchoredPosition = new Vector2(position.x, -position.y);
            if (i < required) contentHeight = Mathf.Max(contentHeight, position.y + node.sizeDelta.y + StageMapPadding);
        }
        _stageContent.sizeDelta = new Vector2(_stageContent.sizeDelta.x, contentHeight);
        for (int segment = _stagePaths.Count; segment < required - 1; segment++)
        {
            RectTransform from = (RectTransform)_elements["StageNode" + segment];
            RectTransform to = (RectTransform)_elements["StageNode" + (segment + 1)];
            Vector2 start = StagePosition(segment) + from.sizeDelta * 0.5f;
            Vector2 end = StagePosition(segment + 1) + to.sizeDelta * 0.5f;
            float distance = Vector2.Distance(start, end);
            float inset = Mathf.Max(from.sizeDelta.x, from.sizeDelta.y) * 0.5f + PathDotSize;
            int count = Mathf.Max(0, Mathf.FloorToInt((distance - inset * 2f) / PathDotSpacing));
            var dots = new List<TourIconGraphic>();
            for (int dot = 0; dot < count; dot++)
            {
                float t = (inset + (distance - inset * 2f) * (dot + 1f) / (count + 1f)) / distance;
                Vector2 point = Vector2.Lerp(start, end, t) - Vector2.one * PathDotSize * 0.5f;
                TourIconGraphic icon = TourUI.Icon(_stageContent, $"StagePath{segment}Dot{dot}", TourIconGraphic.Symbol.Dot,
                    point.x, point.y, PathDotSize);
                icon.transform.SetAsFirstSibling();
                dots.Add(icon);
            }
            _stagePaths.Add(dots);
        }
    }

    private void RevealStage(int index)
    {
        if (_stageScroll == null) return;
        _stageScroll.StopMovement();
        float height = _stageScroll.viewport.sizeDelta.y;
        float offset = Mathf.Clamp(StagePosition(index).y - height * 0.5f,
            0, Mathf.Max(0, _stageContent.sizeDelta.y - height));
        _stageContent.anchoredPosition = new Vector2(0, offset);
    }

    private void ClearStageSelection()
    {
        _selectedStage = null;
        RefreshStageNodes();
        Text("DetailEyebrow", "LOCKED");
        Text("DetailTitle", "未開放");
        Text("DetailSubtitle", "前のステージをクリアすると開放されます");
        Text("ClearCondition", "");
        Text("RecordValue", "—");
        Text("ActionTitle", "LOCKED");
        Text("ActionSubtitle", "前のステージをクリアすると開放");
        Text("Status", "開放済みのステージがありません");
        Picture("DetailArt", null);
        for (int i = 0; i < 3; i++)
        {
            Text("MissionText" + i, "—");
            Text("MissionState" + i, "未開放");
            SetStar("MissionStar" + i, false);
            SetStar("DetailStar" + i, false);
        }
        Button("PrimaryAction").interactable = false;
    }

    private void BuildWorldScroll()
    {
        if (_worldSlots == 0) return;
        RectTransform first = (RectTransform)_elements["WorldCard0"];
        RectTransform last = (RectTransform)_elements["WorldCard" + (_worldSlots - 1)];
        _cardWidth = first.sizeDelta.x;
        _cardHeight = first.sizeDelta.y;
        _cardStride = _worldSlots > 1
            ? ((RectTransform)_elements["WorldCard1"]).anchoredPosition.x - first.anchoredPosition.x
            : _cardWidth + 16f;
        float width = last.anchoredPosition.x - first.anchoredPosition.x + _cardWidth;
        RectTransform viewport = TourUI.Rect(first.parent, "WorldCardViewport",
            first.anchoredPosition.x, -first.anchoredPosition.y, width, _cardHeight);
        Image hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        _worldContent = TourUI.Rect(viewport, "WorldCardContent", 0, 0, width, _cardHeight);
        _worldScroll = viewport.gameObject.AddComponent<ScrollRect>();
        _worldScroll.viewport = viewport;
        _worldScroll.content = _worldContent;
        _worldScroll.horizontal = true;
        _worldScroll.vertical = false;
        _worldScroll.movementType = ScrollRect.MovementType.Clamped;
        _worldScroll.scrollSensitivity = 35f;
        for (int i = 0; i < _worldSlots; i++)
        {
            RectTransform card = (RectTransform)_elements["WorldCard" + i];
            card.SetParent(_worldContent, false);
            card.anchoredPosition = new Vector2(i * _cardStride, 0);
            BindWorldCard(i);
        }

        // A visible handle also makes the strip usable without a drag gesture.
        RectTransform track = TourUI.Rect(viewport.parent, "WorldCardScrollbar",
            viewport.anchoredPosition.x, -viewport.anchoredPosition.y + _cardHeight + 8f, width, 12f);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.015f, 0.055f, 0.16f, 0.8f);
        RectTransform handle = TourUI.Rect(track, "Handle", 0, 0, 0, 0);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = TourUI.Gold;
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        _worldScroll.horizontalScrollbar = scrollbar;
        _worldScroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void BindWorldCard(int index)
    {
        Button button = Button("WorldCard" + index);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectWorld(index));
        TMP_Text count = _elements["WorldCount" + index].GetComponent<TMP_Text>();
        count.text = string.Empty;
        count.enabled = true;
        count.rectTransform.anchoredPosition = new Vector2(12f, -251f);
        count.rectTransform.sizeDelta = new Vector2(_cardWidth - 24f, 36f);
        count.fontSize = 26f;
        count.fontSizeMin = 18f;
        count.fontSizeMax = 26f;
        count.alignment = TextAlignmentOptions.Center;
        count.textWrappingMode = TextWrappingModes.NoWrap;
        count.gameObject.SetActive(false);
        for (int star = 0; star < 3; star++)
            _elements[$"Card{index}Star{star}"].gameObject.SetActive(false);
    }

    private void EnsureWorldCards(int required)
    {
        if (_worldContent == null) return;
        while (_worldSlots < required)
        {
            int index = _worldSlots;
            // Reuse the authored spare card's local artwork and styling.
            int templateIndex = _worldSlots - 1;
            Transform template = _elements["WorldCard" + templateIndex];
            Transform clone = Instantiate(template, _worldContent);
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child == null) continue;
                string name = child == clone ? "WorldCard" + index : child.name;
                if (name.StartsWith("ComingSoonOverlay", System.StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                    continue;
                }
                if (child != clone)
                {
                    string starPrefix = "Card" + templateIndex + "Star";
                    if (name.StartsWith(starPrefix, System.StringComparison.Ordinal))
                        name = "Card" + index + "Star" + name.Substring(starPrefix.Length);
                    else if (name == "WorldCard" + templateIndex + "Title") name = "WorldCard" + index + "Title";
                    else if (name.EndsWith(templateIndex.ToString(), System.StringComparison.Ordinal))
                        name = name.Substring(0, name.Length - templateIndex.ToString().Length) + index;
                }
                child.name = name;
                _elements[name] = child;
            }
            ((RectTransform)clone).anchoredPosition = new Vector2(index * _cardStride, 0);
            BindWorldCard(index);
            _worldSlots++;
        }
        _worldContent.sizeDelta = new Vector2(
            Mathf.Max(_worldScroll.viewport.rect.width, (required - 1) * _cardStride + _cardWidth), _cardHeight);
    }

    private void RefreshWorldPins()
    {
        // Keep the authored map positions independent of the scrolling cards.
        for (int i = 0; i < _pinSlots; i++)
        {
            WorldData world = i < _worlds.Count ? _worlds[i] : null;
            _elements["WorldPin" + i].gameObject.SetActive(world != null);
            _elements["PinName" + i].gameObject.SetActive(world != null);

            Text("PinName" + i, world?.Name);
            bool available = world != null && _manager.IsWorldUnlocked(world);
            SetAvailable("WorldPin" + i, available);
            Icon("PinIcon" + i, available ? TourIconGraphic.Symbol.Baseball : TourIconGraphic.Symbol.Lock, true);
            SetWorldSelected("WorldPin" + i, world != null && world == _selectedWorld);
        }
    }

    private void ShowComingSoon(int index)
    {
        Transform card = _elements["WorldCard" + index];
        card.gameObject.SetActive(true);
        SetAvailable("WorldCard" + index, false);
        _elements["WorldCount" + index].gameObject.SetActive(false);
        SetWorldSelected("WorldCard" + index, false);
        Text("CardNumber" + index, $"WORLD {index + 1}");
        _elements["CardNumber" + index].GetComponent<TMP_Text>().color = Color.white;
        Text("WorldName" + index, "準備中");
        Picture("CardArt" + index, null);
        Icon("WorldLock" + index, TourIconGraphic.Symbol.Lock, false);
        for (int star = 0; star < 3; star++) _elements[$"Card{index}Star{star}"].gameObject.SetActive(false);
        if (!_comingSoon.TryGetValue(index, out GameObject overlay))
        {
            RectTransform art = (RectTransform)_elements["CardArt" + index];
            RectTransform shade = TourUI.Rect(card, "ComingSoonOverlay" + index,
                art.anchoredPosition.x, -art.anchoredPosition.y, art.sizeDelta.x, art.sizeDelta.y);
            Image tint = shade.gameObject.AddComponent<Image>();
            tint.color = new Color(0f, 0.02f, 0.08f, 0.65f);
            tint.raycastTarget = false;
            TMP_Text heading = _elements["CardNumber" + index].GetComponent<TMP_Text>();
            TMP_Text label = TourUI.Text(shade, "ComingSoonText", "COMING\nSOON", 8, 0,
                art.sizeDelta.x - 16f, art.sizeDelta.y, 30f, heading.font, Color.white, true);
            label.fontSizeMin = 18f;
            overlay = shade.gameObject;
            _comingSoon.Add(index, overlay);
        }
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    private void RevealWorld(int index)
    {
        if (_worldScroll == null) return;
        Canvas.ForceUpdateCanvases();
        _worldScroll.StopMovement();
        float max = Mathf.Max(0, _worldContent.rect.width - _worldScroll.viewport.rect.width);
        float offset = Mathf.Clamp(index * _cardStride - (_worldScroll.viewport.rect.width - _cardWidth) * 0.5f, 0, max);
        _worldContent.anchoredPosition = new Vector2(-offset, 0);
    }

    private void RefreshTotals()
    {
        int count = 0, total = 0;
        foreach (WorldData world in _worlds) { count += WorldStars(world, _manager.SaveData); total += world.Stages.Count * 3; }
        Text("TotalStars", $"{count} / {total}");
    }

    private void RevealMenu()
    {
        _screenFade?.Cover("読み込み中…");
        RevealMenuAsync(++_revealRevision, _lifetime.Token).Forget();
    }

    private async UniTask RevealMenuAsync(int revision, CancellationToken cancellationToken)
    {
        try
        {
            // 同じ更新内で登録される画像をまとめて待つ。
            await UniTask.NextFrame(cancellationToken: cancellationToken);
            await UniTask.WaitUntil(() => revision != _revealRevision ||
                ((_manager == null || (_manager.ConfigState != WorldTourConfigState.Loading && !_manager.IsSaveInitializing)) &&
                 (_images == null || !_images.IsLoading)), cancellationToken: cancellationToken);
            if (revision != _revealRevision || this == null) return;
            await _screenFade.RevealAsync(.25f, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void Picture(string name, string url)
    {
        // Never bind remote world/stage images to the fixed world map.
        if (name == "WorldMapFocus") return;
        if (_elements.TryGetValue(name, out Transform element))
            _images.SetImage(element.GetComponent<RawImage>(), url);
    }

    private void Text(string name, string value)
    {
        if (_elements.TryGetValue(name, out Transform element)) element.GetComponent<TMP_Text>().text = value ?? "—";
    }
    private Button Button(string name) => _elements[name].GetComponent<Button>();
    private void SetAvailable(string name, bool available)
    {
        Button button = Button(name);
        button.interactable = available;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        // Fade artwork and labels together; keep raycasts so locked cards can still be dragged.
        group.alpha = available ? 1f : 0.45f;
        group.interactable = available;
        group.blocksRaycasts = true;
    }

    private void SetWorldSelected(string name, bool selected)
    {
        SetSelected(name, selected);
        TourPanelGraphic panel = _elements[name].GetComponent<TourPanelGraphic>();
        panel.TopColor = selected
            ? new Color(1f, 0.89f, 0.33f, 1f)
            : new Color(0.025f, 0.2f, 0.48f, 0.97f);
        panel.BottomColor = selected
            ? new Color(0.95f, 0.54f, 0.025f, 1f)
            : new Color(0.008f, 0.035f, 0.115f, 0.98f);
        panel.SetVerticesDirty();
    }
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
        icon.gameObject.SetActive(true);
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
