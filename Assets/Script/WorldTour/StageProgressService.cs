public static class StageProgressService
{
    /// <summary>
    /// 過去の進捗と現在の結果を比較して、進捗をマージする
    /// </summary>
    /// <param name="saveData">保存中のプレイヤー進捗</param>
    /// <param name="session">今回のステージ結果</param>
    /// <returns>新たに獲得した星の数</returns>
    public static int MergeStageProgress(PlayerSaveData saveData, StageSessionData session)
    {
        // 失敗時は既存のクリア状態と星を維持する。
        if (!session.IsFinished || !session.IsCleared)
            return 0;

        if (!saveData.Stages.TryGetValue(
            session.StageId, out StageProgressData progress))
        {
            progress = new StageProgressData();
            saveData.Stages.Add(session.StageId, progress);
        }

        progress.IsCleared = true;
        int newStarCount = 0;
        for (int i = 0; i < StageData.StarCount; i++)
        {
            // 再挑戦では新しく達成した星だけを加える。
            if (!progress.EarnedStars[i] &&
                session.AchievedStars[i])
            {
                progress.EarnedStars[i] = true;
                newStarCount++;
            }
        }

        return newStarCount;
    }
}
