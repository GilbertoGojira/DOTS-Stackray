namespace Stackray.Entities {
  public interface IComponentValue<T> where T : struct {
    T Value { get; set; }
  }
}