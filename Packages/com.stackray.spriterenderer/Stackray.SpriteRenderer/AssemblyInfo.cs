using Stackray.Entities;
using Stackray.SpriteRenderer;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[assembly: RegisterGenericComponentType(typeof(ChunkHashcode<LocalToWorld>))]

[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<TileOffsetProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ScaleProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<PivotProperty, half2>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ColorProperty, half4>))]


