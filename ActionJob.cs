using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;

namespace Stackray.Jobs {
  public delegate void Action();

  /// <summary>
  /// This job will execute an action
  /// </summary>
  [BurstCompile]
  public struct ActionJob : IJob {

    public static JobHandle Schedule(Action action, JobHandle inputDeps) {
      return new ActionJob {
        Action = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(action))
      }.Schedule(inputDeps);
    }

    FunctionPointer<Action> Action;

    public void Execute() {
      Action.Invoke();
    }
  }
}