#if UNITY_EDITOR
using System;
using Homerunderby;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PitchTypeData)), CanEditMultipleObjects]
public sealed class PitchTypeDataEditor : Editor
{
    private bool _showReferences;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_ballData"), true);
        EditorGUILayout.HelpBox("回転軸はワールド座標です。+Zが捕手方向の球場では、-Xがバックスピン、+Xがトップスピンです。軸の長さは回転数に影響しません。", MessageType.Info);

        EditorGUILayout.LabelField("回転軸の設定例（+Zへ投球）");
        using (new EditorGUILayout.HorizontalScope())
        {
            SetAxisButton("バックスピン", Vector3.left);
            SetAxisButton("トップスピン", Vector3.right);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            SetAxisButton("-X側への変化", Vector3.down);
            SetAxisButton("+X側への変化", Vector3.up);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            SetAxisButton("ジャイロ +Z", Vector3.forward);
            SetAxisButton("ジャイロ -Z", Vector3.back);
        }
        serializedObject.ApplyModifiedProperties();

        if (targets.Length == 1)
        {
            var ball = ((PitchTypeData)target).Data;
            try
            {
                if (float.IsNaN(ball.Speed) || float.IsInfinity(ball.Speed) || ball.Speed <= 0f)
                    throw new ArgumentException("球速は有限値かつ正の値で指定してください。");
                var spin = SpinState.Create(ball.SpinAxis, ball.RotateSpeed);
                Vector3 axis = ball.SpinAxis.sqrMagnitude >= 1e-12f ? ball.SpinAxis / ball.SpinAxis.magnitude : Vector3.zero;
                EditorGUILayout.LabelField("正規化した回転軸", axis.ToString("F4"));
                EditorGUILayout.LabelField("直交成分の割合（+Z基準）", spin.GetTransverseRatio(Vector3.forward).ToString("P1"));
                EditorGUILayout.LabelField("ジャイロ成分の割合（符号付き）", spin.GetSignedGyroRatio(Vector3.forward).ToString("F3"));
                EditorGUILayout.HelpBox("割合は軸と速度から求めた参考値です。実際の投球方向や飛翔中の速度に応じて変わります。", MessageType.None);
            }
            catch (ArgumentException e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
        }

        _showReferences = EditorGUILayout.Foldout(_showReferences, "設定の由来", true);
        if (_showReferences)
            foreach (string field in new[] { "_referencePitcher", "_referenceSeason", "_referencePeriod", "_sourceUrls", "_selectionReason", "_parameterNotes" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
        serializedObject.ApplyModifiedProperties();
    }

    private void SetAxisButton(string label, Vector3 axis)
    {
        if (GUILayout.Button(label))
            serializedObject.FindProperty("_ballData.SpinAxis").vector3Value = axis;
    }
}
#endif
