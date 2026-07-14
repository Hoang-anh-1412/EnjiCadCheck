namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Top-level drawing metadata exported to JSON.
    /// </summary>
    public class DrawingInfo
    {
        /// <summary>File name of the DWG (without path).</summary>
        public string DrawingName { get; set; }

        /// <summary>DWG file format version as saved by the host.</summary>
        public string DwgVersion { get; set; }

        /// <summary>Insertion units (Insunits).</summary>
        public string InsertionUnits { get; set; }

        /// <summary>Overall geometric extents of the drawing.</summary>
        public BoundsInfo GeometricExtents { get; set; }
    }
}
