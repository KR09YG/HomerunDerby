namespace Homerunderby
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "PitchType", menuName = "Baseball/Pitch Type")]
    public sealed class PitchTypeData : ScriptableObject
    {
        [Tooltip("球速はkm/h、回転数はrpm、回転軸の角度は度で設定する。")]
        [SerializeField] private BallData _ballData;

        [Tooltip("自動投球で選ばれる重み。同じ値なら等確率、0なら選択しない。")]
        [Min(0f)]
        [SerializeField] private float _selectionWeight = 1f;

        [Header("参考にした投球データ")]
        [SerializeField] private string _referencePitcher;
        [SerializeField] private int _referenceSeason;
        [Tooltip("通年成績か途中集計か、球速を採用した範囲を記録する。")]
        [SerializeField] private string _referencePeriod;
        [SerializeField] private string[] _sourceUrls;
        [TextArea(2, 5)]
        [SerializeField] private string _selectionReason;
        [Tooltip("公開値と推定値を区別し、回転設定を調整した理由を残す。")]
        [TextArea(3, 8)]
        [SerializeField] private string _parameterNotes;

        // 計算側で値を変更しても、共有している球種アセットには影響しない。
        public BallData Data => _ballData;
        public float SelectionWeight => float.IsNaN(_selectionWeight) || float.IsInfinity(_selectionWeight)
            ? 0f : Mathf.Max(0f, _selectionWeight);
    }
}
