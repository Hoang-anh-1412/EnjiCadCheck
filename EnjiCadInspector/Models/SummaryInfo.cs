namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Entity counts by type for the current drawing.
    /// </summary>
    public class SummaryInfo
    {
        public int Line { get; set; }

        public int Circle { get; set; }

        public int Arc { get; set; }

        public int Ellipse { get; set; }

        public int Polyline { get; set; }

        public int Spline { get; set; }

        public int Hatch { get; set; }

        public int DBText { get; set; }

        public int MText { get; set; }

        public int Dimension { get; set; }

        public int Leader { get; set; }

        public int BlockReference { get; set; }

        /// <summary>Total number of exported entities.</summary>
        public int TotalEntity { get; set; }
    }
}
