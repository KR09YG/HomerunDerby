using System;
using UnityEngine;

/// <summary>
/// 球場モデルの地面、フェンス、観客席に打球用の当たり判定を設定する。
/// </summary>
[DisallowMultipleComponent]
public sealed class StadiumSurfaceColliders : MonoBehaviour
{
    private void Awake()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int fenceLayer = LayerMask.NameToLayer("Net");
        if (groundLayer < 0 || fenceLayer < 0)
        {
            Debug.LogError("球場の当たり判定に必要な Ground / Net レイヤーがありません。", this);
            return;
        }

        int groundCount = 0;
        int fenceCount = 0;
        int homeRunFenceCount = 0;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            string meshName = filter.gameObject.name;
            int layer;
            if (IsGroundSurface(meshName))
            {
                layer = groundLayer;
            }
            else if (IsWallSurface(meshName))
            {
                layer = fenceLayer;
            }
            else
            {
                continue;
            }

            if (filter.sharedMesh == null)
                continue;

            if (!Application.isEditor && !filter.sharedMesh.isReadable)
            {
                Debug.LogError($"[Stadium] {meshName} の当たり判定を生成できません。球場モデルのRead/Writeを有効にしてください。", filter);
                continue;
            }

            GameObject surface = filter.gameObject;
            surface.layer = layer;
            MeshCollider meshCollider = surface.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = surface.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = filter.sharedMesh;
            if (layer == groundLayer)
                groundCount++;
            else
                fenceCount++;
            if (meshName.StartsWith("WALL_Panel_", StringComparison.Ordinal)) homeRunFenceCount++;
        }

        if (groundCount == 0 || fenceCount == 0)
            Debug.LogError($"球場の当たり判定を設定できませんでした。地面: {groundCount}、フェンス: {fenceCount}", this);

        Physics.SyncTransforms();
        Debug.Log($"[Stadium] 当たり判定設定: Ground={groundCount}, Walls={fenceCount}, HomeRunFencePanels={homeRunFenceCount}", this);
    }

    private static bool IsGroundSurface(string name)
    {
        return name.StartsWith("FIELD_ContinuousSurface", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_Grass", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_InfieldGrass", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_InfieldSkin", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_WarningTrack", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_FoulApron_", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_FoulExtension", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_BackstopApron", StringComparison.Ordinal) ||
               name.StartsWith("FIELD_GradedMound", StringComparison.Ordinal);
    }

    private static bool IsWallSurface(string name)
    {
        return name.StartsWith("WALL_", StringComparison.Ordinal) ||
               name.StartsWith("STAND_", StringComparison.Ordinal) ||
               name.StartsWith("NET_", StringComparison.Ordinal) ||
               name.StartsWith("DUGOUT_", StringComparison.Ordinal) ||
               name.StartsWith("SCORE_", StringComparison.Ordinal) ||
               name.StartsWith("LIGHT_GroundedFooting", StringComparison.Ordinal) ||
               name.StartsWith("DETAIL_CameraRail_", StringComparison.Ordinal) ||
               name.StartsWith("DETAIL_CameraWell_", StringComparison.Ordinal);
    }
}
