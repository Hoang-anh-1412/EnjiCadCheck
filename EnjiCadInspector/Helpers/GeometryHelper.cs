using System;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Helpers
{
    /// <summary>
    /// Geometry conversion and safe extents helpers for DWG export.
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// Converts a CAD Point3d to a serializable DTO.
        /// </summary>
        public static Point3dInfo ToPoint(Point3d point)
        {
            return new Point3dInfo(point.X, point.Y, point.Z);
        }

        /// <summary>
        /// Converts a CAD Vector3d to a serializable point DTO (XYZ components).
        /// </summary>
        public static Point3dInfo ToPoint(Vector3d vector)
        {
            return new Point3dInfo(vector.X, vector.Y, vector.Z);
        }

        /// <summary>
        /// Reads GeometricExtents safely; returns null when extents are unavailable.
        /// </summary>
        public static BoundsInfo TryGetBounds(Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            try
            {
                var extents = entity.GeometricExtents;
                return new BoundsInfo
                {
                    Min = ToPoint(extents.MinPoint),
                    Max = ToPoint(extents.MaxPoint)
                };
            }
            catch (Exception)
            {
                // Some entities (empty hatch, degenerate polyline, etc.) throw here.
                return null;
            }
        }

        /// <summary>
        /// Merges two bounds; either side may be null.
        /// </summary>
        public static BoundsInfo MergeBounds(BoundsInfo a, BoundsInfo b)
        {
            if (a == null)
            {
                return b;
            }

            if (b == null)
            {
                return a;
            }

            return new BoundsInfo
            {
                Min = new Point3dInfo(
                    Math.Min(a.Min.X, b.Min.X),
                    Math.Min(a.Min.Y, b.Min.Y),
                    Math.Min(a.Min.Z, b.Min.Z)),
                Max = new Point3dInfo(
                    Math.Max(a.Max.X, b.Max.X),
                    Math.Max(a.Max.Y, b.Max.Y),
                    Math.Max(a.Max.Z, b.Max.Z))
            };
        }

        /// <summary>
        /// Converts Extents3d to BoundsInfo.
        /// </summary>
        public static BoundsInfo FromExtents(Extents3d extents)
        {
            return new BoundsInfo
            {
                Min = ToPoint(extents.MinPoint),
                Max = ToPoint(extents.MaxPoint)
            };
        }
    }
}
