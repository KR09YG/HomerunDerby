using UnityEngine;

public static class DerbyScoreCalculator
{
    private const float ScoreRoundingFactor = 100f;

    public static ResultDisplayData Calculate(
        float distanceMeters,
        int consecutiveHomeRunCount,
        bool isFoul,
        bool isMiss,
        float previousTotalScore)
    {
        float scoredDistance = isFoul || isMiss
            ? 0f
            : Mathf.Round(distanceMeters * ScoreRoundingFactor) / ScoreRoundingFactor;
        float finalScore = scoredDistance * Mathf.Max(1, consecutiveHomeRunCount);

        return new ResultDisplayData
        {
            Distance = scoredDistance,
            ConsecutiveHomeRunCount = consecutiveHomeRunCount,
            IsFoul = isFoul,
            FinalScore = finalScore,
            TotalScore = previousTotalScore + finalScore
        };
    }
}
