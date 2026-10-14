using UnityEngine;

namespace Homerunderby
{
    public static class PitchTargetProjection
    {
        public static bool TryFromScreen(Camera camera, Vector2 screenPoint, Bounds zone, out Vector3 point)
        {
            point = default;
            if (camera == null || !camera.pixelRect.Contains(screenPoint)) return false;
            // 真横や真上では高さ・左右の一方を指定できないため、2Dパネルで操作する。
            if (Mathf.Abs(camera.transform.forward.z) < .03f) return false;
            Ray ray = camera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.forward, zone.center);
            if (!plane.Raycast(ray, out float distance)) return false;
            point = ClampToZone(ray.GetPoint(distance), zone);
            return true;
        }

        public static Vector3 ClampToZone(Vector3 point, Bounds zone)
        {
            return new Vector3(Mathf.Clamp(point.x, zone.min.x, zone.max.x),
                Mathf.Clamp(point.y, zone.min.y, zone.max.y), zone.center.z);
        }

        public static Vector3 FromNormalized(Vector2 normalized, Bounds zone)
        {
            return new Vector3(Mathf.Lerp(zone.min.x, zone.max.x, normalized.x),
                Mathf.Lerp(zone.min.y, zone.max.y, normalized.y), zone.center.z);
        }

        public static Vector2 ToNormalized(Vector3 point, Bounds zone)
        {
            return new Vector2(Mathf.InverseLerp(zone.min.x, zone.max.x, point.x),
                Mathf.InverseLerp(zone.min.y, zone.max.y, point.y));
        }

        public static bool TryFromPanel(RectTransform panel, Vector2 screenPoint, Bounds zone, out Vector3 point)
        {
            point = default;
            if (panel == null || !RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screenPoint, null, out Vector2 local)) return false;
            Rect rect = panel.rect;
            point = FromNormalized(new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y)), zone);
            return true;
        }
    }
}
