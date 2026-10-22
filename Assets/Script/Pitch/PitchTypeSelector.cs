namespace Homerunderby
{
    using System;
    using System.Collections.Generic;

    internal static class PitchTypeSelector
    {
        /// <summary>
        /// 指定された球種リストから、重みに基づいてランダムに球種を選択します。
        /// </summary>
        /// <param name="pitches">球種のリスト</param>
        /// <param name="randomValue">0から1の範囲のランダム値</param>
        /// <returns>選択された球種</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static PitchTypeData Select(IReadOnlyList<WeightedPitch> pitches, float randomValue)
        {
            if (pitches == null) throw new ArgumentNullException(nameof(pitches));
            if (float.IsNaN(randomValue) || randomValue < 0f || randomValue > 1f)
                throw new ArgumentOutOfRangeException(nameof(randomValue));

            double totalWeight = 0;
            PitchTypeData lastAvailable = null;
            foreach (var pitch in pitches)
            {
                if (pitch == null || pitch.Weight <= 0f) continue;
                totalWeight += pitch.Weight;
                lastAvailable = pitch.PitchType;
            }
            if (lastAvailable == null)
                throw new InvalidOperationException("投球できる球種がありません。球種リストと選択の重みを確認してください。");

            double remaining = randomValue * totalWeight;
            foreach (var pitch in pitches)
            {
                if (pitch == null || pitch.Weight <= 0f) continue;
                remaining -= pitch.Weight;
                if (remaining < 0) return pitch.PitchType;
            }
            // Random.value は1を返すことがあるため、末尾も選択範囲に含める。
            return lastAvailable;
        }
    }
}
