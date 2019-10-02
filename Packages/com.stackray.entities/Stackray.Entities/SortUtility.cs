namespace Stackray.Entities {
  public class SortUtility {
    public static int CalculateTotalSlices(int initialSlices) {
      return initialSlices <= 0 ? 0 : initialSlices + CalculateTotalSlices(initialSlices / 2);
    }  
  }
}
