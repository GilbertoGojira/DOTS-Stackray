using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Mathematics {
  public static class Extensions {
    public static float3 Position(this float4x4 m) {
      return m.c3.xyz;
    }

    public static float3 Forward(this float4x4 m) {
      return m.c2.xyz;
    }

    public static float3 Up(this float4x4 m) {
      return m.c1.xyz;
    }

    public static float3 Right(this float4x4 m) {
      return m.c0.xyz;
    }

    /// <summary>
    /// Extracts the scale vector from the matrix
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public static float3 Scale(this float4x4 m) {
      return new float3(math.length(m.c0), math.length(m.c1), math.length(m.c2));
    }

    /// <summary>
    /// Extracts rotation from the matrix
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public static quaternion Rotation(this float4x4 m) {
      return quaternion.LookRotationSafe(m.Forward(), m.Up());
    }

    public static float3 Euler(this quaternion quaternion) {
      var sqw = quaternion.value.w * quaternion.value.w;
      var sqx = quaternion.value.x * quaternion.value.x;
      var sqy = quaternion.value.y * quaternion.value.y;
      var sqz = quaternion.value.z * quaternion.value.z;
      var unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
      var test = quaternion.value.x * quaternion.value.w - quaternion.value.y * quaternion.value.z;
      float3 v;
      if (test > 0.4995f * unit) { // singularity at north pole
        v.y = 2f * math.atan2(quaternion.value.y, quaternion.value.x);
        v.x = (float)(math.PI / 2f);
        v.z = 0;
        return NormalizeAngles(math.degrees(v));
      }
      if (test < -0.4995f * unit) { // singularity at south pole
        v.y = -2f * math.atan2(quaternion.value.y, quaternion.value.x);
        v.x = (float)(-math.PI / 2);
        v.z = 0;
        return NormalizeAngles(math.degrees(v));
      }
      var q = new quaternion(quaternion.value.w, quaternion.value.z, quaternion.value.x, quaternion.value.y);
      v.y = math.atan2(2f * q.value.x * q.value.w + 2f * q.value.y * q.value.z, 1 - 2f * (q.value.z * q.value.z + q.value.w * q.value.w));     // Yaw
      v.x = math.asin(2f * (q.value.x * q.value.z - q.value.w * q.value.y));                             // Pitch
      v.z = math.atan2(2f * q.value.x * q.value.y + 2f * q.value.z * q.value.w, 1 - 2f * (q.value.y * q.value.y + q.value.z * q.value.z));      // Roll
      return NormalizeAngles(math.degrees(v));
    }

    static float3 NormalizeAngles(float3 angles) {
      angles.x = NormalizeAngle(angles.x);
      angles.y = NormalizeAngle(angles.y);
      angles.z = NormalizeAngle(angles.z);
      return angles;
    }

    static float NormalizeAngle(float angle) {
      while (angle > 360)
        angle -= 360;
      while (angle < 0)
        angle += 360;
      return angle;
    }

    public static float4x4 RotateAround(this float4x4 localToWorld, float3 point, float3 angles) {
      return localToWorld.RotateAround(point, localToWorld.Up(), angles.x)
        .RotateAround(point, localToWorld.Right(), angles.y)
        .RotateAround(point, localToWorld.Forward(), angles.z);
    }

    public static float4x4 RotateAround(this float4x4 localToWorld, float3 center, float3 axis, float angle) {
      var initialRot = localToWorld.Rotation();
      var rotAmount = quaternion.AxisAngle(axis, angle);
      var finalPos = center + math.mul(rotAmount, localToWorld.Position() - center);
      var finalRot = math.mul(math.mul(initialRot, math.mul(math.inverse(initialRot), rotAmount)), initialRot);
      return new float4x4(finalRot, finalPos);
    }

    /// <summary>
    /// Gets the axis of a vector
    /// eg. (3, 1) -> (1, 0) and (1, 1) -> (0, 0)
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="threshold">the min length of the vector that will allow the axis to be detected</param>
    /// <returns></returns>
    public static float2 GetAxis(this float2 vector, float threshold) {
      return math.lengthsq(vector) > threshold * threshold && vector.x != vector.y ?
        math.abs(vector.x) > math.abs(vector.y) ? new float2(1, 0) : new float2(0, 1) :
        new float2();
    }

    public static float3 Right(this float3 forward) {
      return math.normalize(math.cross(forward, new float3(0, 1, 0)));
    }

    public static float3 Up(this float3 forward) {
      return math.normalize(math.cross(forward.Right(), forward));
    }

    public static float Max(this float3 f) {
      return f.x > f.y && f.x > f.z ? f.x : f.y > f.z ? f.y : f.z;
    }

    public static int MaxAbsAxis(this float3 f) {
      return math.abs(f.x) > math.abs(f.y) && math.abs(f.x) > math.abs(f.z) ? 0 : math.abs(f.y) > math.abs(f.z) ? 1 : 2;
    }

    public static int MinAbsAxis(this float3 f) {
      return math.abs(f.x) < math.abs(f.y) && math.abs(f.x) < math.abs(f.z) ? 0 : math.abs(f.y) < math.abs(f.z) ? 1 : 2;
    }

    public static float3 Proj(this float3 f1, float3 f2) {
      return (math.dot(f1, f2) / math.lengthsq(f2)) * f2;
    }

    public static float3 ProjOnPlane(this float3 f, float3 planeNormal) {
      return f - Proj(f, planeNormal);
    }

    public static float3 MultiplyPoint(this float4x4 m, float3 v) {
      var v4 = math.mul(m, new float4(v, 1));
      v4 *= 1f / v4.w;
      return v4.xyz;
    }

    public static Color ToColor(this float4 f) {
      return new Color(f.x, f.y, f.z, f.w);
    }

    public static Color ToColor(this half4 f) {
      return new Color(f.x, f.y, f.z, f.w);
    }

    public static float4 ToFloat4(this Color color) {
      return new float4(color.r, color.g, color.b, color.a);
    }

    public static float4 ToFloat4(this Color32 color) {
      return new float4(color.r, color.g, color.b, color.a) / 255.0f;
    }
  }
}
