using Stackray.Renderer;
using Unity.Entities;
using Unity.Mathematics;

[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<TileOffsetProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ScaleProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<PivotProperty, half2>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ColorProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<FlipProperty, int2>))]

class ConcreteJobs {
  ExtractValuesPerChunk<TileOffsetProperty, half4> genericJob01 = new ExtractValuesPerChunk<TileOffsetProperty, half4>();
  ExtractValuesPerChunk<ScaleProperty, half4> genericJob02 = new ExtractValuesPerChunk<ScaleProperty, half4>();
  ExtractValuesPerChunk<PivotProperty, half2> genericJob03 = new ExtractValuesPerChunk<PivotProperty, half2>();
  ExtractValuesPerChunk<ColorProperty, half4> genericJob04 = new ExtractValuesPerChunk<ColorProperty, half4>();
}


