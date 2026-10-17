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
        // 今回クリアできていない場合は進捗を更新しない
        if (!session.IsFinished || !session.IsCleared)
            return 0;

        // 過去のステージ進捗を取得する。なければ新規作成する
        if (!saveData.Stages.TryGetValue(
            session.StageId, out StageProgressData progress))
        {
            progress = new StageProgressData();
            saveData.Stages.Add(session.StageId, progress);
        }

        progress.IsCleared = true;
        int newStarCount = 0;
        // 過去の進捗と今回の結果を比較して、星の獲得状況を更新する
        for (int i = 0; i < 3; i++)
        {
            // 過去に獲得していない星で、今回獲得した星があれば進捗を更新する
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
