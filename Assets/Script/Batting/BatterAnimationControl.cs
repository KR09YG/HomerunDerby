using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using UnityEngine;

public class BatterAnimationControl : MonoBehaviour
{
    [SerializeField] Animator _animator;
    [SerializeField] OnBattingInputEvent _inputEvent;
    [SerializeField] OnPitchBallReleaseEvent _ballReleaseEvent;
    [SerializeField] OnBallLandedEvent _ballLandedEvent;
    [SerializeField] OnAtBatResetEvent _atBatReset;

    private Vector3 _startPos;
    private Vector3 _startRote;
    public bool IsFinSwing => _isFinSwing;
    private bool _isFinSwing = false;
    public bool IsSwinging => _isSwinging;
    private bool _isSwinging = false;

    private void Awake()
    {
        _startPos = transform.position;
        _startRote = transform.eulerAngles;

        if (_inputEvent != null) _inputEvent.RegisterListener(OnInput);
        else Debug.LogError("OnBattingInputEvent が未設定");

        if (_ballReleaseEvent != null) _ballReleaseEvent.RegisterListener(OnRelease);
        else Debug.LogError("OnPitchBallReleaseEvent が未設定");

        if (_ballLandedEvent != null) _ballLandedEvent.RegisterListener(ResetAtBat);
        else Debug.LogError("OnBallLandedEvent が未設定");

        if (_atBatReset != null) _atBatReset.RegisterListener(ResetAtBat);
        else Debug.LogError("OnAtBatResetEvent が未設定");
    }

    /// <summary>
    /// 打席をリセットする
    /// </summary>
    private void ResetAtBat()
    {
        Debug.Log("打席をリセット");
        _animator.SetTrigger("Reset");
        _isFinSwing = false;
        _isSwinging = false;
        WaitForAnimationTransitionWrapper().Forget();
    }

    public void FinishSwing()
    {
        _isFinSwing = true;
    }

    // unitaskを上記関数で呼ぶためのラッパー関数
    private async UniTask WaitForAnimationTransitionWrapper()
    {
        await UniTask.WaitForSeconds(0.5f);
        transform.position = _startPos;
        transform.eulerAngles = _startRote;
    }

    /// <summary>
    /// ピッチャーがボールをリリースしたときに呼ばれる
    /// </summary>
    /// <param name="ball"></param>
    private void OnRelease(PitchBallMove ball)
    {
        _animator.SetTrigger("Prepare");
    }

    /// <summary>
    /// バッティングの入力を受けたときに呼ばれる
    /// </summary>
    private void OnInput()
    {
        _animator.SetTrigger("Swing");
        _isSwinging = true;
    }
}
