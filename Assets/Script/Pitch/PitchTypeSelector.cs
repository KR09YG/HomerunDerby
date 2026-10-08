namespace Homerunderby
{
    using System;
    using System.Collections.Generic;

    internal static class PitchTypeSelector
    {
        public static PitchTypeData Select(IReadOnlyList<PitchTypeData> pitches, float randomValue)
        {
            if (pitches == null) throw new ArgumentNullException(nameof(pitches));
            if (float.IsNaN(randomValue) || randomValue < 0f || randomValue > 1f)
                throw new ArgumentOutOfRangeException(nameof(randomValue));

            double totalWeight = 0;
            PitchTypeData lastAvailable = null;
            foreach (var pitch in pitches)
            {
                if (pitch == null || pitch.SelectionWeight <= 0f) continue;
                totalWeight += pitch.SelectionWeight;
                lastAvailable = pitch;
            }
            if (lastAvailable == null)
                throw new InvalidOperationException("投球できる球種がありません。球種リストと選択の重みを確認してください。");

            double remaining = randomValue * totalWeight;
            foreach (var pitch in pitches)
            {
                if (pitch == null || pitch.SelectionWeight <= 0f) continue;
                remaining -= pitch.SelectionWeight;
                if (remaining < 0) return pitch;
            }
            // Random.value は1を返すことがあるため、末尾も選択範囲に含める。
            return lastAvailable;
        }
    }
}
