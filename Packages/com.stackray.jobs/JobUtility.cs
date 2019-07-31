using System.Collections.Generic;
using Unity.Jobs;

namespace Stackray.Jobs {
  public class JobUtility {
    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2, JobHandle job3) {
      var deps0 = JobHandle.CombineDependencies(job0, job1, job2);
      return JobHandle.CombineDependencies(deps0, job3);
    }

    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2, JobHandle job3, JobHandle job4) {
      var deps0 = JobHandle.CombineDependencies(job0, job1, job2);
      return JobHandle.CombineDependencies(deps0, job3, job4);
    }

    public static JobHandle CombineDependencies(List<JobHandle> jobs) {
      var deps = default(JobHandle);
      for (var i = 0; i < jobs.Count; ++i)
        deps = JobHandle.CombineDependencies(deps, jobs[i]);
      return deps;
    }
  }
}
