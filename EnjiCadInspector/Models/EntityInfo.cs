using System.Collections.Generic;
using Newtonsoft.Json;

namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Universal entity export model. Type-specific fields are filled when applicable.
    /// Null type-specific properties are omitted from JSON.
    /// </summary>
    public class EntityInfo
    {
        // ---- Common fields (every entity) ----

        public string Handle { get; set; }

        public string Type { get; set; }

        public string Layer { get; set; }

        public string Color { get; set; }

        public string Linetype { get; set; }

        public string LineWeight { get; set; }

        public bool Visible { get; set; }

        public BoundsInfo Bounds { get; set; }

        public string ObjectId { get; set; }

        // ---- Line ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo StartPoint { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo EndPoint { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Length { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Angle { get; set; }

        // ---- Circle ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo Center { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Radius { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Diameter { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Area { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Circumference { get; set; }

        // ---- Arc ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? StartAngle { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? EndAngle { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? ArcLength { get; set; }

        // ---- Polyline ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? Closed { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Point3dInfo> Vertices { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<double> Bulges { get; set; }

        // ---- Spline ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Degree { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Point3dInfo> ControlPoints { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Point3dInfo> FitPoints { get; set; }

        // ---- Ellipse ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo MajorAxis { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? MinorRadius { get; set; }

        // ---- DBText ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo Position { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Height { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Rotation { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Style { get; set; }

        // ---- MText ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Contents { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo Location { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Width { get; set; }

        // ---- Dimension (geometry for DIM recreate) ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Measurement { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DimensionText { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DimensionStyle { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo XLine1Point { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo XLine2Point { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo DimLinePoint { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo TextPosition { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo ChordPoint { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Point3dInfo FarChordPoint { get; set; }

        // ---- BlockReference ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string BlockName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? ScaleX { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? ScaleY { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? ScaleZ { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Attributes { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> DynamicProperties { get; set; }

        // ---- Hatch ----

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string PatternName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? PatternScale { get; set; }
    }
}
