namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Axis-aligned bounding box for an entity (GeometricExtents).
    /// </summary>
    public class BoundsInfo
    {
        public Point3dInfo Min { get; set; }

        public Point3dInfo Max { get; set; }
    }
}
