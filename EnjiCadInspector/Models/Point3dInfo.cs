namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Serializable 3D point used in JSON export.
    /// </summary>
    public class Point3dInfo
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public Point3dInfo()
        {
        }

        public Point3dInfo(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
