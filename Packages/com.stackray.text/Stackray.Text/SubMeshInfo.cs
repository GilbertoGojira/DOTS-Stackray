﻿using Unity.Entities;

namespace Stackray.Text {
  public struct SubMeshInfo : IBufferElementData {
    public int Offset;
    public int VertexCount;
    public int MaterialId;
  }
}