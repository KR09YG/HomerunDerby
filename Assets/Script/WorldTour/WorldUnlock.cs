using System;
using System.Collections.Generic;

/// <summary>設定の接続と保存済みのクリア履歴から、選択できるワールド・ステージを求める。</summary>
public sealed class WorldUnlock
{
    private readonly Action<string> _warn;
    private readonly List<WorldData> _worlds = new List<WorldData>();
    private readonly Dictionary<string, WorldData> _worldById = new Dictionary<string, WorldData>();
    private readonly Dictionary<string, WorldData> _owners = new Dictionary<string, WorldData>();
    private readonly Dictionary<string, StageData> _stages = new Dictionary<string, StageData>();
    private readonly Dictionary<string, List<StageData>> _orderedStages = new Dictionary<string, List<StageData>>();
    private readonly HashSet<string> _invalidStageLinks = new HashSet<string>();
    private readonly HashSet<string> _invalidWorldLinks = new HashSet<string>();
    private readonly HashSet<string> _openWorlds = new HashSet<string>();
    private readonly HashSet<string> _openStages = new HashSet<string>();
    private PlayerSaveData _saveData;

    public IReadOnlyList<WorldData> Worlds => _worlds;

    public WorldUnlock(Action<string> warn = null) { _warn = warn; }

    public void SetConfig(WorldTourConfig config)
    {
        _worlds.Clear();
        _worldById.Clear();
        _owners.Clear();
        _stages.Clear();
        _orderedStages.Clear();
        _invalidStageLinks.Clear();
        _invalidWorldLinks.Clear();
        if (config?.Worlds == null) return;

        foreach (WorldData world in config.Worlds)
        {
            // 設定画面に残っている空欄は、未作成のワールドとして除外する。
            if (world == null || string.IsNullOrEmpty(world.WorldId)) continue;
            if (_worldById.ContainsKey(world.WorldId))
            {
                Warn($"ワールドIDが重複しています: {world.WorldId}");
                continue;
            }
            if (world.Stages == null || world.Stages.Count == 0)
            {
                Warn($"ステージがありません: World={world.WorldId}");
                continue;
            }
            _worlds.Add(world);
            _worldById.Add(world.WorldId, world);
            foreach (StageData stage in world.Stages)
            {
                if (stage == null || string.IsNullOrEmpty(stage.StageId))
                {
                    Warn($"ステージIDが未設定です: World={world.WorldId}");
                    continue;
                }
                if (_stages.ContainsKey(stage.StageId))
                {
                    Warn($"ステージIDが重複しています: {stage.StageId}");
                    continue;
                }
                _stages.Add(stage.StageId, stage);
                _owners.Add(stage.StageId, world);
            }
        }

        foreach (WorldData world in _worlds)
        {
            foreach (StageData stage in world.Stages)
            {
                if (!BelongsTo(stage, world)) continue;
                if (stage.IsBoss)
                {
                    if (stage.NextStagesId?.Count > 0)
                        Warn($"ボスの次はNextWorldで指定してください: Stage={stage.StageId}");
                    continue;
                }
                if (stage.NextStagesId == null || stage.NextStagesId.Count != 1 ||
                    !_stages.TryGetValue(stage.NextStagesId[0] ?? "", out StageData next) ||
                    !BelongsTo(next, world) || next == stage)
                {
                    _invalidStageLinks.Add(stage.StageId);
                    Warn($"通常ステージには同一ワールド内の次ステージを1件指定してください: Stage={stage.StageId}");
                }
            }
            var ordered = new List<StageData>();
            var visited = new HashSet<string>();
            StageData current = FirstStage(world);
            while (current != null && visited.Add(current.StageId))
            {
                ordered.Add(current);
                StageData next = NextInWorld(current);
                if (next != null && visited.Contains(next.StageId))
                {
                    _invalidStageLinks.Add(current.StageId);
                    Warn($"ステージの接続が循環しています: Stage={current.StageId}");
                    break;
                }
                current = next;
            }
            foreach (StageData stage in world.Stages)
                if (BelongsTo(stage, world) && !visited.Contains(stage.StageId))
                {
                    ordered.Add(stage);
                    Warn($"最初のステージから到達できません: Stage={stage.StageId}");
                }
            _orderedStages.Add(world.WorldId, ordered);
            if (!string.IsNullOrEmpty(world.NextWorld) &&
                (!_worldById.ContainsKey(world.NextWorld) || world.NextWorld == world.WorldId))
            {
                _invalidWorldLinks.Add(world.WorldId);
                Warn($"次ワールドのIDが不正です: World={world.WorldId}, Next={world.NextWorld}");
            }
        }
        foreach (WorldData start in _worlds)
        {
            var visited = new HashSet<string>();
            WorldData current = start;
            while (current != null && visited.Add(current.WorldId))
            {
                WorldData next = NextWorld(current);
                if (next != null && visited.Contains(next.WorldId))
                {
                    _invalidWorldLinks.Add(current.WorldId);
                    Warn($"ワールドの接続が循環しています: World={current.WorldId}");
                    break;
                }
                current = next;
            }
        }
    }

    public void SetSaveData(PlayerSaveData saveData) { _saveData = saveData; }

    public IReadOnlyList<StageData> GetStages(WorldData world) =>
        IsRegistered(world) && _orderedStages.TryGetValue(world.WorldId, out List<StageData> stages)
            ? stages : Array.Empty<StageData>();

    public WorldData FindWorld(string id) =>
        id != null && _worldById.TryGetValue(id, out WorldData world) ? world : null;

    public WorldData Owner(StageData stage) =>
        stage != null && stage.StageId != null && _stages.TryGetValue(stage.StageId, out StageData registered) && registered == stage
        && _owners.TryGetValue(stage.StageId, out WorldData world) ? world : null;

    public bool IsWorldUnlocked(WorldData world)
    {
        Refresh();
        return IsRegistered(world) && _openWorlds.Contains(world.WorldId);
    }

    public bool IsStageUnlocked(StageData stage)
    {
        Refresh();
        WorldData world = Owner(stage);
        return world != null && _openWorlds.Contains(world.WorldId) &&
            (_openStages.Contains(stage.StageId) || IsCleared(stage.StageId));
    }

    public bool IsWorldCleared(WorldData world)
    {
        foreach (StageData stage in GetStages(world))
            if (stage.IsBoss && IsCleared(stage.StageId) && IsStageUnlocked(stage)) return true;
        return false;
    }

    public StageData GetNextStage(StageData stage)
    {
        WorldData owner = Owner(stage);
        if (owner == null) return null;
        return stage.IsBoss ? FirstStage(NextWorld(owner)) : NextInWorld(stage);
    }

    private void Refresh()
    {
        _openWorlds.Clear();
        _openStages.Clear();
        WorldData world = _worlds.Count > 0 ? _worlds[0] : null;
        while (world != null && _openWorlds.Add(world.WorldId))
        {
            StageData stage = FirstStage(world);
            while (stage != null && _openStages.Add(stage.StageId))
            {
                if (!IsCleared(stage.StageId)) return;
                if (stage.IsBoss) break;
                stage = NextInWorld(stage);
            }
            if (stage == null || !stage.IsBoss || !IsCleared(stage.StageId)) return;
            world = NextWorld(world);
        }
    }

    private StageData FirstStage(WorldData world)
    {
        if (world?.Stages == null) return null;
        foreach (StageData stage in world.Stages)
            if (BelongsTo(stage, world)) return stage;
        return null;
    }

    private StageData NextInWorld(StageData stage)
    {
        if (stage.IsBoss || _invalidStageLinks.Contains(stage.StageId) || stage.NextStagesId?.Count != 1) return null;
        return _stages.TryGetValue(stage.NextStagesId[0] ?? "", out StageData next) &&
            Owner(next) == Owner(stage) ? next : null;
    }

    private WorldData NextWorld(WorldData world) =>
        _invalidWorldLinks.Contains(world.WorldId) ? null : FindWorld(world.NextWorld);

    private bool IsRegistered(WorldData world) =>
        world != null && world.WorldId != null && FindWorld(world.WorldId) == world;

    private bool BelongsTo(StageData stage, WorldData world) => Owner(stage) == world && stage != null;

    private bool IsCleared(string id) => _saveData?.Stages != null &&
        _saveData.Stages.TryGetValue(id, out StageProgressData progress) && progress?.IsCleared == true;

    private void Warn(string message) { _warn?.Invoke("[WorldTour] " + message); }
}
