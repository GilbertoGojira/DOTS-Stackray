namespace Stackray.Entities {
  public class SortUtility {
    public static int CalculateTotalSlices(int initialSlices) {
      return initialSlices <= 1 ? initialSlices : (initialSlices + CalculateTotalSlices(initialSlices / 2) + (initialSlices % 2 != 0 ? 1 : 0));
    }
  }
}
