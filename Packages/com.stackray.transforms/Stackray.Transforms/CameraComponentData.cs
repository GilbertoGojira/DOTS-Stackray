using Stackray.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Transforms {
  public struct MainCameraComponentData : IComponentData { }

  public struct CameraComponentData : IComponentData {
    public float4x4 ProjectionMatrix;
    public float4x4 WorldToCameraMatrix;
    public Plane Plane;
    public float NearClipPlane;
    public float FarClipPlane;
    public float Aspect;

    public int Dimensionality {
      get => IsOrthographic ? 2 : 3;
    }

    public float FieldOfView {
      get { return CacheFieldOfView; }
      set {
        if (CacheFieldOfView != value)
          Cached = 0;
        CacheFieldOfView = value;
      }
    }
    public bool IsOrthographic {
      get { return CachedIsOrthographic == 1; }
      set {
        var v = value ? 1 : 0;
        if (CachedIsOrthographic != v)
          Cached = 0;
        CachedIsOrthographic = v;
      }
    }
    public float OrthographicSize {
      get { return CacheOrthographicSize; }
      set {
        if (CacheOrthographicSize != value)
          Cached = 0;
        CacheOrthographicSize = value;
      }
    }

    private float CacheFieldOfView;
    private int CachedIsOrthographic;
    private float CacheOrthographicSize;

    private int Cached;
    public float PerpectivePointsPerPixelFactor;
    public float OrthographicPointsPerPixel;

    public void CalcCachedPointsPerPixel(float4x4 localToWorld) {
      if (Cached == 1)
        return;
      Cached = 1;
      var position = localToWorld.c3.xyz;
      var forward = localToWorld.c2.xyz;
      PerpectivePointsPerPixelFactor = CameraUtility.WorldPointsPerPixelFactor(ProjectionMatrix, WorldToCameraMatrix, localToWorld);
      OrthographicPointsPerPixel = CameraUtility.WorldPointsPerPixel(ProjectionMatrix, WorldToCameraMatrix, localToWorld, position + forward);
    }

    public void UpdatePlane(float4x4 localToWorld) {
      Plane = new Plane(localToWorld.Forward(), localToWorld.Position());
    }

    public float Distance(Vector3 point) {
      return Plane.GetDistanceToPoint(point);
    }

    public float3 ScreenToWorldPoint(float4x4 localToWorld, float3 screenPos) {
      return CameraUtility.ScreenToWorldPoint(ProjectionMatrix, WorldToCameraMatrix, localToWorld, screenPos);
    }

    public float2 WorldToScreenPoint(float3 worldPos) {
      return CameraUtility.WorldToScreenPoint(ProjectionMatrix, WorldToCameraMatrix, worldPos);
    }

    public Ray ScreenPointToRay(float4x4 localToWorld, float3 forward, float3 screenPos) {
      return CameraUtility.ScreenPointToRay(ProjectionMatrix, WorldToCameraMatrix, localToWorld, forward, screenPos);
    }

    /// <summary>
    /// Gets how many world points is a pixel at a given position related to the camera
    /// </summary>
    /// <param name="position">position to calculate the ppp</param>
    /// <returns></returns>
    public float WorldPointsPerPixel(Vector3 worldPos) {
      return IsOrthographic ? OrthographicPointsPerPixel : PerpectivePointsPerPixelFactor * Mathf.Abs(Plane.GetDistanceToPoint(worldPos));
    }

    /// <summary>
    ///  Get how many world points is an inch
    /// </summary>
    /// <param name="worldPos">the position in the world where are calculating</param>
    /// <returns></returns>
    public float WorldUnitsPerInch(Vector3 worldPos, float dpi) {
      return WorldPointsPerPixel(worldPos) * dpi;
    }
  }
}

