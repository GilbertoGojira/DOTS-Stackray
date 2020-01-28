using Stackray.Text;
using Stackray.Transforms;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(LimitActiveComponentSystem<TextRenderer>.LimitActive<TextRenderer>))]

namespace Stackray.Text {
  public class LimitActiveTextSystem : LimitActiveComponentSystem<TextRenderer> { }
}