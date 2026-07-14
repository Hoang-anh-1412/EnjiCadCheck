using System;
using System.Collections.Generic;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Exporters
{
    /// <summary>
    /// Builds summary counts from exported entities.
    /// </summary>
    public static class SummaryExporter
    {
        /// <summary>
        /// Counts entities by type using the exported <see cref="EntityInfo.Type"/> values.
        /// </summary>
        public static SummaryInfo Export(IList<EntityInfo> entities)
        {
            var summary = new SummaryInfo();
            if (entities == null)
            {
                return summary;
            }

            summary.TotalEntity = entities.Count;

            foreach (var entity in entities)
            {
                if (entity == null || string.IsNullOrEmpty(entity.Type))
                {
                    continue;
                }

                var type = entity.Type;

                if (string.Equals(type, "Line", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Line++;
                }
                else if (string.Equals(type, "Circle", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Circle++;
                }
                else if (string.Equals(type, "Arc", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Arc++;
                }
                else if (string.Equals(type, "Ellipse", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Ellipse++;
                }
                else if (IsPolyline(type))
                {
                    summary.Polyline++;
                }
                else if (string.Equals(type, "Spline", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Spline++;
                }
                else if (string.Equals(type, "Hatch", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Hatch++;
                }
                else if (string.Equals(type, "DBText", StringComparison.OrdinalIgnoreCase))
                {
                    summary.DBText++;
                }
                else if (string.Equals(type, "MText", StringComparison.OrdinalIgnoreCase))
                {
                    summary.MText++;
                }
                else if (IsDimension(type))
                {
                    summary.Dimension++;
                }
                else if (IsLeader(type))
                {
                    summary.Leader++;
                }
                else if (string.Equals(type, "BlockReference", StringComparison.OrdinalIgnoreCase))
                {
                    summary.BlockReference++;
                }
            }

            return summary;
        }

        private static bool IsPolyline(string type)
        {
            return string.Equals(type, "Polyline", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Polyline2d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Polyline3d", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDimension(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return false;
            }

            return type.IndexOf("Dimension", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(type, "AlignedDimension", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "RotatedDimension", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "RadialDimension", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "DiametricDimension", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "OrdinateDimension", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "ArcDimension", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLeader(string type)
        {
            return string.Equals(type, "Leader", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "MLeader", StringComparison.OrdinalIgnoreCase);
        }
    }
}
