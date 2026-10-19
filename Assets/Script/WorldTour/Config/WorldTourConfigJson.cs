using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>配信JSONの型と必須項目を確認し、Unityで使う設定へ復元する。</summary>
public static class WorldTourConfigJson
{
    public static WorldTourConfig Parse(string json)
    {
        try
        {
            JObject root = JObject.Parse(json);
            RequireType(root["ConfigVersion"], JTokenType.Integer, "ConfigVersion");
            if (root.Value<int>("ConfigVersion") < 1) throw new InvalidDataException("ConfigVersionは1以上が必要です。");
            foreach (string name in new[] { "NormalStarReward", "BossStarReward" })
            {
                RequireType(root[name], JTokenType.Integer, name);
                if (root.Value<int>(name) < 0) throw new InvalidDataException(name + "は0以上が必要です。");
            }
            JArray worlds = RequireArray(root["Worlds"], "Worlds");
            if (worlds.Count == 0) throw new InvalidDataException("Worldsが空です。");
            foreach (JToken world in worlds)
            {
                if (!(world is JObject)) throw new InvalidDataException("Worldの形式が不正です。");
                RequireString(world["WorldId"], "WorldId");
                RequireString(world["Name"], "World名");
                ValidateImageUrl(world);
                RequireString(world["RegionId"], "RegionId");
                RequireString(world["NextWorld"], "NextWorld", true);
                JArray stages = RequireArray(world["Stages"], "Stages");
                if (stages.Count == 0) throw new InvalidDataException("Stagesが空です。");
                foreach (JToken stage in stages)
                {
                    if (!(stage is JObject)) throw new InvalidDataException("Stageの形式が不正です。");
                    RequireString(stage["StageId"], "StageId");
                    RequireString(stage["Name"], "Stage名");
                    ValidateImageUrl(stage);
                    RequireType(stage["BallCount"], JTokenType.Integer, "BallCount");
                    RequireType(stage["IsBoss"], JTokenType.Boolean, "IsBoss");
                    foreach (JToken id in RequireArray(stage["NextStagesId"], "NextStagesId")) RequireString(id, "NextStagesId[]");
                    ValidateCondition(stage["ClearObjective"]);
                    JArray objectives = RequireArray(stage["StarObjectives"], "StarObjectives");
                    if (objectives.Count != 3) throw new InvalidDataException("StarObjectivesは3件必要です。");
                    foreach (JToken condition in objectives) ValidateCondition(condition);
                    foreach (JToken pitch in RequireArray(stage["PitchPool"], "PitchPool"))
                    {
                        if (!(pitch is JObject)) throw new InvalidDataException("PitchPoolの形式が不正です。");
                        RequireString(pitch["PitchId"], "PitchId");
                        RequireNumber(pitch["Weight"], "Weight");
                    }
                    JToken reward = stage["StarRewardOverride"];
                    if (reward != null && reward.Type != JTokenType.Null)
                    {
                        RequireType(reward, JTokenType.Integer, "StarRewardOverride");
                        if (reward.Value<int>() < 0) throw new InvalidDataException("StarRewardOverrideは0以上が必要です。");
                    }
                }
            }

            WorldTourConfig config = JsonUtility.FromJson<WorldTourConfig>(json);
            if (config == null || config.Worlds == null || config.Worlds.Count != worlds.Count)
                throw new InvalidDataException("WorldTourConfigを復元できません。");
            for (int wi = 0; wi < worlds.Count; wi++)
            {
                JArray stages = (JArray)worlds[wi]["Stages"];
                if (config.Worlds[wi]?.Stages?.Count != stages.Count) throw new InvalidDataException("Stagesの復元件数が一致しません。");
                for (int si = 0; si < stages.Count; si++)
                {
                    // JsonUtilityが扱えないnullable値だけを元JSONから補完する。
                    JToken reward = stages[si]["StarRewardOverride"];
                    config.Worlds[wi].Stages[si].StarRewardOverride = reward == null || reward.Type == JTokenType.Null ? (int?)null : reward.Value<int>();
                }
            }

            var errors = new List<string>();
            new WorldUnlock(errors.Add).SetConfig(config);
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            var worldIds = new HashSet<string>();
            WorldData connectedWorld = config.Worlds[0];
            while (connectedWorld != null && worldIds.Add(connectedWorld.WorldId))
                connectedWorld = config.Worlds.Find(world => world.WorldId == connectedWorld.NextWorld);
            if (worldIds.Count != config.Worlds.Count) throw new InvalidDataException("最初のワールドから到達できないWorldがあります。");
            foreach (WorldData world in config.Worlds)
                foreach (StageData stage in world.Stages)
                {
                    if (stage.BallCount <= 0) throw new InvalidDataException($"BallCountが不正です: Stage={stage.StageId}");
                    var conditions = new List<StageCondition>(stage.StarObjectives) { stage.ClearObjective };
                    foreach (StageCondition condition in conditions)
                        if (float.IsNaN(condition.TargetValue) || float.IsInfinity(condition.TargetValue))
                            throw new InvalidDataException($"条件の目標値が不正です: Stage={stage.StageId}");
                    bool hasPositiveWeight = false;
                    foreach (StagePitchEntry pitch in stage.PitchPool)
                    {
                        if (pitch.Weight < 0 || float.IsNaN(pitch.Weight) || float.IsInfinity(pitch.Weight))
                            throw new InvalidDataException($"抽選重みが不正です: Stage={stage.StageId}");
                        hasPositiveWeight |= pitch.Weight > 0;
                    }
                    if (!hasPositiveWeight) throw new InvalidDataException($"抽選できる球種がありません: Stage={stage.StageId}");
                }
            return config;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is Newtonsoft.Json.JsonException || exception is ArgumentException ||
            exception is InvalidCastException || exception is OverflowException || exception is FormatException)
        {
            throw new InvalidDataException("World Tour設定JSONの形式が不正です。", exception);
        }
    }

    private static void ValidateImageUrl(JToken item)
    {
        JToken value = item["ImageUrl"];
        // 旧配信JSONでは画像項目が存在しないため、省略とnullも受け付ける。
        if (value == null || value.Type == JTokenType.Null) return;
        RequireType(value, JTokenType.String, "ImageUrl");
    }

    private static void ValidateCondition(JToken condition)
    {
        if (!(condition is JObject)) throw new InvalidDataException("条件設定がありません。");
        RequireType(condition["Type"], JTokenType.Integer, "条件Type");
        RequireType(condition["Direction"], JTokenType.Integer, "条件Direction");
        RequireNumber(condition["TargetValue"], "条件TargetValue");
        int type = condition.Value<int>("Type"), direction = condition.Value<int>("Direction");
        if (!Enum.IsDefined(typeof(ChallengeConditionType), type) || !Enum.IsDefined(typeof(HomeRunDirection), direction) ||
            (type == (int)ChallengeConditionType.DirectionalHomeRunCount && direction == (int)HomeRunDirection.None) ||
            condition.Value<double>("TargetValue") < 0)
            throw new InvalidDataException("条件種別・方向・目標値が不正です。");
    }

    private static JArray RequireArray(JToken value, string name) =>
        value as JArray ?? throw new InvalidDataException(name + "は配列が必要です。");

    private static void RequireString(JToken value, string name, bool allowEmpty = false)
    {
        RequireType(value, JTokenType.String, name);
        if (!allowEmpty && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException(name + "が空です。");
    }

    private static void RequireNumber(JToken value, string name)
    {
        if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) ||
            double.IsNaN(value.Value<double>()) || double.IsInfinity(value.Value<double>()))
            throw new InvalidDataException(name + "は有限の数値が必要です。");
    }

    private static void RequireType(JToken value, JTokenType type, string name)
    {
        if (value == null || value.Type != type) throw new InvalidDataException(name + "の型が不正です。");
    }
}
