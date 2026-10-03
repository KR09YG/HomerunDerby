using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Deforms the authored arc once per swing; never chases the cursor with the moving bat.
[DefaultExecutionOrder(1000)]
public class BatSwingAimIK : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private Transform _batSweetSpot;
    [SerializeField] private OnBattingInputEvent _inputEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private string _swingStateName = "Swing";
    [SerializeField] private AnimationClip _swingClip;
    [SerializeField, Range(0f, 30f)] private float _maxTorsoAngle = 18f;
    [SerializeField, Range(0f, 15f)] private float _maxWristAngle = 8f;
    [SerializeField, Min(0f)] private float _maxHandOffset = 1.1f;

    private readonly IKSolverTrigonometric _gripArm = new IKSolverTrigonometric();
    private readonly IKSolverTrigonometric _supportArm = new IKSolverTrigonometric();
    private Transform _spine, _gripHand, _supportHand;
    private Transform[] _modifiedBones;
    private Quaternion[] _authoredRotations;
    private bool _poseApplied, _ready, _swinging;
    private Quaternion _torsoCorrection, _wristCorrection;
    private Vector3 _handOffset;
    private float _contactTime;
    private GameObject _reference;
    private Transform _referenceSpine, _referenceGripHand, _referenceSweetSpot;
    private Transform _referenceSupportHand, _referenceGripShoulder, _referenceSupportShoulder;
    private PlayableGraph _graph;
    private AnimationClipPlayable _clipPlayable;
    private bool _contactRequested;
    private Vector3 _supportHandLocalPosition;
    private Quaternion _supportHandLocalRotation;

    public float ContactClipFrame => _swingClip != null ? _contactTime * _swingClip.frameRate : -1f;
    public float ContactRewindSeconds
    {
        get
        {
            if (!_ready || !_swinging) return 0f;
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            float speed = _animator.speed * state.speed * state.speedMultiplier;
            if (!state.IsName(_swingStateName) || speed <= 0f) return 0f;
            float overshoot = (state.normalizedTime * _swingClip.length - _contactTime) / speed;
            return Mathf.Clamp(overshoot, 0f, Time.deltaTime);
        }
    }

    public bool RequestContactPose()
    {
        if (!_ready || !_swinging || !isActiveAndEnabled) return false;
        _contactRequested = true;
        return true;
    }

    public bool TryGetSweetSpot(out Vector3 position)
    {
        position = _batSweetSpot != null ? _batSweetSpot.position : Vector3.zero;
        return _ready && _poseApplied && !_contactRequested;
    }

    private void Awake()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        _inputEvent?.RegisterListener(OnSwing);
        _atBatResetEvent?.RegisterListener(ResetSwing);
    }

    private void Start()
    {
        if (_animator == null || !_animator.isHuman || _batSweetSpot == null || _swingClip == null)
        {
            Debug.LogError("BatSwingAimIK: assign humanoid, sweet spot and swing clip.", this);
            return;
        }
        _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        bool heldInLeftHand = _batSweetSpot.IsChildOf(left);
        if (!heldInLeftHand && !_batSweetSpot.IsChildOf(right))
        {
            Debug.LogError("BatSwingAimIK: sweet spot must belong to the hand holding the bat.", this);
            return;
        }
        // The prefab attaches the bat to LEFT hand. Always solve that hand as the
        // rigid grip; locking it as the support hand changes the bat after planning.
        _gripHand = heldInLeftHand ? left : right;
        _supportHand = heldInLeftHand ? right : left;
        Transform ru = _animator.GetBoneTransform(heldInLeftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform rf = _animator.GetBoneTransform(heldInLeftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform lu = _animator.GetBoneTransform(heldInLeftHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform lf = _animator.GetBoneTransform(heldInLeftHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        if (!_gripArm.SetChain(ru, rf, _gripHand, _animator.transform) ||
            !_supportArm.SetChain(lu, lf, _supportHand, _animator.transform)) return;
        _modifiedBones = new[] { _spine, ru, rf, _gripHand, lu, lf, _supportHand };
        _authoredRotations = new Quaternion[_modifiedBones.Length];
        _contactTime = -1f;
        foreach (AnimationEvent e in _swingClip.events)
            if (e.functionName == "StartBattingCalculate") { _contactTime = e.time; break; }
        if (_contactTime <= 0f || _contactTime >= _swingClip.length)
        {
            Debug.LogError("BatSwingAimIK: swing clip needs a contact event.", this);
            return;
        }

        // Bone-only replica: no renderers, gameplay behaviours or animation events.
        var map = new Dictionary<Transform, Transform>();
        _reference = CopyHierarchy(_animator.transform, null, map).gameObject;
        _reference.name = "Swing contact reference";
        _reference.hideFlags = HideFlags.HideAndDontSave;
        _referenceSpine = map[_spine];
        _referenceGripHand = map[_gripHand];
        _referenceSupportHand = map[_supportHand];
        _referenceGripShoulder = map[ru];
        _referenceSupportShoulder = map[lu];
        _referenceSweetSpot = map[_batSweetSpot];
        var referenceAnimator = _reference.AddComponent<Animator>();
        referenceAnimator.avatar = _animator.avatar;
        referenceAnimator.applyRootMotion = true;
        referenceAnimator.fireEvents = false;
        referenceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _graph = PlayableGraph.Create("Batter contact pose");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        _clipPlayable = AnimationClipPlayable.Create(_graph, _swingClip);
        AnimationPlayableOutput.Create(_graph, "Contact", referenceAnimator).SetSourcePlayable(_clipPlayable);
        _graph.Play();
        _ready = true;
    }

    private static Transform CopyHierarchy(Transform source, Transform parent, Dictionary<Transform, Transform> map)
    {
        var copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        map.Add(source, copy);
        foreach (Transform child in source) CopyHierarchy(child, copy, map);
        return copy;
    }

    private void OnSwing()
    {
        if (!_ready || _cursor == null) return;
        RestorePose();
        // Keep the support hand on the handle, not at its independently animated wrist.
        // Sample the clip's load pose so input/Animator update order cannot change the grip.
        _clipPlayable.SetTime(0);
        _graph.Evaluate(0);
        _supportHandLocalPosition = _referenceGripHand.InverseTransformPoint(_referenceSupportHand.position);
        _supportHandLocalRotation = Quaternion.Inverse(_referenceGripHand.rotation) * _referenceSupportHand.rotation;
        _reference.transform.SetPositionAndRotation(_animator.transform.position, _animator.transform.rotation);
        _reference.transform.localScale = _animator.transform.lossyScale;
        _graph.Evaluate(_contactTime);
        Vector3 pivot = _referenceSpine.position;
        Vector3 sweet = _referenceSweetSpot.position;
        Vector3 hand = _referenceGripHand.position;
        Vector3 target = _cursor.CurrentPos;
        PlanContact(pivot, sweet, hand, target);
        _swinging = true;
    }

    // Plan ONCE on the unmodified contact pose. A direction-only aim can leave one arm
    // unreachable; choose a small torso/wrist adjustment that both arms can actually hold.
    private void PlanContact(Vector3 pivot, Vector3 sweet, Vector3 hand, Vector3 target)
    {
        Quaternion aim = LimitedRotation(sweet - pivot, target - pivot, _maxTorsoAngle);
        float bestCost = float.PositiveInfinity;
        bool found = false;
        for (int x = -2; x <= 2; x++)
        for (int y = -2; y <= 2; y++)
        for (int z = -2; z <= 2; z++)
        {
            Quaternion torso = Quaternion.RotateTowards(Quaternion.identity,
                Quaternion.Euler(x * 6f, y * 6f, z * 6f) * aim, _maxTorsoAngle);
            Vector3 rh = pivot + torso * (hand - pivot);
            Vector3 support = _referenceGripHand.TransformPoint(_supportHandLocalPosition);
            Vector3 lh = pivot + torso * (support - pivot);
            Vector3 rs = pivot + torso * (_referenceGripShoulder.position - pivot);
            Vector3 ls = pivot + torso * (_referenceSupportShoulder.position - pivot);
            Vector3 barrel = pivot + torso * (sweet - pivot);
            Quaternion wristAim = LimitedRotation(barrel - rh, target - rh, _maxWristAngle);
            for (int w = -1; w <= 2; w++)
            {
                Quaternion wrist = Quaternion.SlerpUnclamped(Quaternion.identity, wristAim, w * .5f);
                Vector3 offset = target - (rh + wrist * (barrel - rh));
                Vector3 leftTarget = rh + wrist * (lh - rh) + offset;
                bool feasible = offset.magnitude <= _maxHandOffset &&
                    WithinReach(_gripArm, rs, rh + offset) && WithinReach(_supportArm, ls, leftTarget);
                float cost = offset.sqrMagnitude + .0003f * Mathf.Pow(Quaternion.Angle(Quaternion.identity, torso), 2f)
                    + .0002f * Mathf.Pow(Quaternion.Angle(Quaternion.identity, wrist), 2f);
                if ((found && !feasible) || (feasible == found && cost >= bestCost)) continue;
                found = feasible;
                bestCost = cost;
                _torsoCorrection = torso;
                _wristCorrection = wrist;
                _handOffset = Vector3.ClampMagnitude(offset, _maxHandOffset);
            }
        }
        if (!found) Debug.LogWarning("BatSwingAimIK: cursor is outside the supported contact reach.", this);
    }

    private static bool WithinReach(IKSolverTrigonometric arm, Vector3 shoulder, Vector3 target)
    {
        float a = Vector3.Distance(arm.bone1.transform.position, arm.bone2.transform.position);
        float b = Vector3.Distance(arm.bone2.transform.position, arm.bone3.transform.position);
        float distance = Vector3.Distance(shoulder, target);
        return distance < (a + b) * .99f && distance > Mathf.Abs(a - b) + .01f;
    }

    private static Quaternion LimitedRotation(Vector3 from, Vector3 to, float degrees)
    {
        if (from.sqrMagnitude < 0.000001f || to.sqrMagnitude < 0.000001f) return Quaternion.identity;
        return Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(from, to), degrees);
    }

    // Remove last frame's additive pose BEFORE Animator evaluates. This also prevents drift
    // when the animation-event pause stops Animator at the same time for many rendered frames.
    private void Update() => RestorePose();

    private void LateUpdate()
    {
        if (!_ready || !_swinging) return;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName(_swingStateName) || state.normalizedTime >= 1f) return;
        if (_contactRequested)
        {
            // Animation events are delivered on the frame that crosses their time.
            // Evaluate the exact contact pose once so a low frame rate cannot skip it.
            bool fireEvents = _animator.fireEvents;
            _animator.fireEvents = false;
            try
            {
                _animator.Play(state.fullPathHash, 0, _contactTime / _swingClip.length);
                _animator.Update(0f);
                _animator.transform.SetPositionAndRotation(_reference.transform.position, _reference.transform.rotation);
            }
            finally { _animator.fireEvents = fireEvents; }
            _contactRequested = false;
            state = _animator.GetCurrentAnimatorStateInfo(0);
        }
        float weight = Envelope(state.normalizedTime, _contactTime / _swingClip.length);
        for (int i = 0; i < _modifiedBones.Length; i++)
            _authoredRotations[i] = _modifiedBones[i].localRotation;
        _poseApplied = true;
        // Spine pivot changes the swing plane without moving the pelvis, planted feet or root.
        _spine.rotation = Quaternion.Slerp(Quaternion.identity, _torsoCorrection, weight) * _spine.rotation;
        Quaternion wrist = Quaternion.Slerp(Quaternion.identity, _wristCorrection, weight);
        Vector3 right = _gripHand.position;
        // Retain the grip through impact, then let the authored hand release in follow-through.
        float gripWeight = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(_contactTime + .04f, _contactTime + .14f,
                state.normalizedTime * _swingClip.length));
        Vector3 supportPosition = Vector3.Lerp(_supportHand.position,
            _gripHand.TransformPoint(_supportHandLocalPosition), gripWeight);
        Quaternion supportRotation = Quaternion.Slerp(_supportHand.rotation,
            _gripHand.rotation * _supportHandLocalRotation, gripWeight);

        Vector3 offset = _handOffset * weight;
        // Reduce the shared correction if either arm would have to stretch. Never move a bat alone.
        float amount = 1f;
        if (!CanApplyHands(right, supportPosition, wrist, offset, amount))
        {
            float low = 0f, high = 1f;
            for (int i = 0; i < 14; i++)
            {
                float middle = (low + high) * 0.5f;
                if (CanApplyHands(right, supportPosition, wrist, offset, middle)) low = middle;
                else high = middle;
            }
            amount = low;
        }
        if (amount <= 0f) return;
        wrist = Quaternion.Slerp(Quaternion.identity, wrist, amount);
        Vector3 left = right + wrist * (supportPosition - right) + offset * amount;
        Quaternion rightRotation = wrist * _gripHand.rotation;
        Quaternion leftRotation = wrist * supportRotation;
        Solve(_gripArm, right + offset * amount, rightRotation);
        Solve(_supportArm, left, leftRotation);
    }

    private bool CanApplyHands(Vector3 right, Vector3 supportPosition, Quaternion wrist, Vector3 offset, float amount)
    {
        Vector3 left = right + Quaternion.Slerp(Quaternion.identity, wrist, amount) *
            (supportPosition - right) + offset * amount;
        return Reachable(_gripArm, right + offset * amount) && Reachable(_supportArm, left);
    }

    private static bool Reachable(IKSolverTrigonometric arm, Vector3 point)
    {
        float a = Vector3.Distance(arm.bone1.transform.position, arm.bone2.transform.position);
        float b = Vector3.Distance(arm.bone2.transform.position, arm.bone3.transform.position);
        float d = Vector3.Distance(arm.bone1.transform.position, point);
        return d <= a + b - 0.0001f && d >= Mathf.Abs(a - b) + 0.0001f;
    }

    private static void Solve(IKSolverTrigonometric arm, Vector3 position, Quaternion rotation)
    {
        arm.SetBendPlaneToCurrent();
        arm.IKPositionWeight = 1f;
        arm.IKRotationWeight = 1f;
        arm.IKPosition = position;
        arm.IKRotation = rotation;
        arm.Update();
    }

    private static float Envelope(float time, float contact)
    {
        // Keep aiming out of the load pose near the head. The support-hand grip is
        // still solved at zero aim weight, and correction reaches full weight before impact.
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(contact * .6f, contact * .95f, time));
        float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(contact + 0.15f, 1f, time));
        return rise * fall;
    }

    private void RestorePose()
    {
        if (!_poseApplied) return;
        for (int i = 0; i < _modifiedBones.Length; i++)
            _modifiedBones[i].localRotation = _authoredRotations[i];
        _poseApplied = false;
    }

    private void ResetSwing() { RestorePose(); _swinging = false; _contactRequested = false; }
    private void OnDisable() => ResetSwing();
    private void OnDestroy()
    {
        _inputEvent?.UnregisterListener(OnSwing);
        _atBatResetEvent?.UnregisterListener(ResetSwing);
        if (_graph.IsValid()) _graph.Destroy();
        if (_reference != null) Destroy(_reference);
    }
}

