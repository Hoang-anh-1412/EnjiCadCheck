using System;
using System.Collections.Generic;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Exporters
{
    /// <summary>
    /// Exports ModelSpace and PaperSpace entities with type-specific properties.
    /// </summary>
    public static class EntityExporter
    {
        /// <summary>
        /// Walks all layout block records and exports every readable entity.
        /// </summary>
        /// <param name="db">Active drawing database.</param>
        /// <param name="tr">Open transaction.</param>
        /// <param name="onError">Optional error callback (handle, message).</param>
        public static List<EntityInfo> Export(Database db, Transaction tr, Action<string, string> onError = null)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            var result = new List<EntityInfo>();
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId btrId in blockTable)
            {
                BlockTableRecord btr;
                try
                {
                    btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(btrId.ToString(), "Open BlockTableRecord failed: " + ex.Message);
                    continue;
                }

                // Export entities from layouts (ModelSpace + PaperSpace). Skip block definitions.
                if (!btr.IsLayout)
                {
                    continue;
                }

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased)
                    {
                        continue;
                    }

                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null)
                        {
                            continue;
                        }

                        result.Add(ExportEntity(entity, tr));
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke(id.ToString(), "Export entity failed: " + ex.Message);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Maps one CAD entity to <see cref="EntityInfo"/>.
        /// </summary>
        public static EntityInfo ExportEntity(Entity entity, Transaction tr)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var info = new EntityInfo
            {
                Handle = SafeHandle(entity),
                Type = Classify(entity),
                Layer = SafeString(() => entity.Layer),
                Color = SafeString(() => entity.Color != null ? entity.Color.ToString() : string.Empty),
                Linetype = SafeString(() => entity.Linetype),
                LineWeight = SafeString(() => entity.LineWeight.ToString()),
                Visible = SafeVisible(entity),
                Bounds = GeometryHelper.TryGetBounds(entity),
                ObjectId = SafeString(() => entity.ObjectId.ToString())
            };

            try
            {
                if (entity is Line line)
                {
                    FillLine(info, line);
                }
                else if (entity is Circle circle)
                {
                    FillCircle(info, circle);
                }
                else if (entity is Arc arc)
                {
                    FillArc(info, arc);
                }
                else if (entity is Ellipse ellipse)
                {
                    FillEllipse(info, ellipse);
                }
                else if (entity is Polyline polyline)
                {
                    FillPolyline(info, polyline);
                }
                else if (entity is Spline spline)
                {
                    FillSpline(info, spline);
                }
                else if (entity is Hatch hatch)
                {
                    FillHatch(info, hatch);
                }
                else if (entity is DBText dbText)
                {
                    FillDbText(info, dbText);
                }
                else if (entity is MText mText)
                {
                    FillMText(info, mText);
                }
                else if (entity is Dimension dimension)
                {
                    FillDimension(info, dimension);
                }
                else if (entity is BlockReference blockReference)
                {
                    FillBlockReference(info, blockReference, tr);
                }
                else if (entity is Leader || entity is MLeader)
                {
                    // Common fields already filled; Leader/MLeader have no extra required fields in the spec.
                }
            }
            catch (Exception)
            {
                // Keep common fields even if type-specific fill fails.
            }

            return info;
        }

        private static void FillLine(EntityInfo info, Line line)
        {
            info.StartPoint = GeometryHelper.ToPoint(line.StartPoint);
            info.EndPoint = GeometryHelper.ToPoint(line.EndPoint);
            info.Length = line.Length;
            info.Angle = line.Angle;
        }

        private static void FillCircle(EntityInfo info, Circle circle)
        {
            info.Center = GeometryHelper.ToPoint(circle.Center);
            info.Radius = circle.Radius;
            info.Diameter = circle.Diameter;
            info.Area = Math.PI * circle.Radius * circle.Radius;
            info.Circumference = 2.0 * Math.PI * circle.Radius;
        }

        private static void FillArc(EntityInfo info, Arc arc)
        {
            info.Center = GeometryHelper.ToPoint(arc.Center);
            info.Radius = arc.Radius;
            info.StartAngle = arc.StartAngle;
            info.EndAngle = arc.EndAngle;
            info.ArcLength = arc.Length;
        }

        private static void FillEllipse(EntityInfo info, Ellipse ellipse)
        {
            info.Center = GeometryHelper.ToPoint(ellipse.Center);
            info.MajorAxis = GeometryHelper.ToPoint(ellipse.MajorAxis);
            info.MinorRadius = ellipse.MinorRadius;
            info.StartAngle = ellipse.StartAngle;
            info.EndAngle = ellipse.EndAngle;
        }

        private static void FillPolyline(EntityInfo info, Polyline polyline)
        {
            info.Closed = polyline.Closed;
            info.Length = polyline.Length;

            try
            {
                info.Area = polyline.Area;
            }
            catch (Exception)
            {
                info.Area = null;
            }

            var vertices = new List<Point3dInfo>();
            var bulges = new List<double>();

            for (var i = 0; i < polyline.NumberOfVertices; i++)
            {
                try
                {
                    vertices.Add(GeometryHelper.ToPoint(polyline.GetPoint3dAt(i)));
                    bulges.Add(polyline.GetBulgeAt(i));
                }
                catch (Exception)
                {
                    // Skip corrupt vertex; continue remaining vertices.
                }
            }

            info.Vertices = vertices;
            info.Bulges = bulges;
        }

        private static void FillSpline(EntityInfo info, Spline spline)
        {
            info.Degree = spline.Degree;
            info.Closed = spline.Closed;

            var controlPoints = new List<Point3dInfo>();
            try
            {
                for (var i = 0; i < spline.NumControlPoints; i++)
                {
                    controlPoints.Add(GeometryHelper.ToPoint(spline.GetControlPointAt(i)));
                }
            }
            catch (Exception)
            {
                // Keep whatever was collected.
            }

            info.ControlPoints = controlPoints;

            var fitPoints = new List<Point3dInfo>();
            try
            {
                for (var i = 0; i < spline.NumFitPoints; i++)
                {
                    fitPoints.Add(GeometryHelper.ToPoint(spline.GetFitPointAt(i)));
                }
            }
            catch (Exception)
            {
                // Fit points are optional for some splines.
            }

            info.FitPoints = fitPoints;
        }

        private static void FillHatch(EntityInfo info, Hatch hatch)
        {
            info.PatternName = SafeString(() => hatch.PatternName);
            try
            {
                info.PatternScale = hatch.PatternScale;
            }
            catch (Exception)
            {
                info.PatternScale = null;
            }

            try
            {
                info.Area = hatch.Area;
            }
            catch (Exception)
            {
                info.Area = null;
            }
        }

        private static void FillDbText(EntityInfo info, DBText dbText)
        {
            info.Text = dbText.TextString;
            info.Position = GeometryHelper.ToPoint(dbText.Position);
            info.Height = dbText.Height;
            info.Rotation = dbText.Rotation;
            info.Style = SafeString(() => dbText.TextStyleName);
        }

        private static void FillMText(EntityInfo info, MText mText)
        {
            info.Contents = mText.Contents;
            info.Location = GeometryHelper.ToPoint(mText.Location);
            info.Width = mText.Width;
            info.Height = mText.TextHeight;
            info.Rotation = mText.Rotation;
        }

        private static void FillDimension(EntityInfo info, Dimension dimension)
        {
            try
            {
                info.Measurement = dimension.Measurement;
            }
            catch (Exception)
            {
                info.Measurement = null;
            }

            info.DimensionText = SafeString(() => dimension.DimensionText);
            info.DimensionStyle = SafeString(() => dimension.DimensionStyleName);

            try
            {
                info.TextPosition = GeometryHelper.ToPoint(dimension.TextPosition);
            }
            catch (Exception)
            {
                info.TextPosition = null;
            }

            if (dimension is AlignedDimension aligned)
            {
                TrySetPoint(() => aligned.XLine1Point, p => info.XLine1Point = p);
                TrySetPoint(() => aligned.XLine2Point, p => info.XLine2Point = p);
                TrySetPoint(() => aligned.DimLinePoint, p => info.DimLinePoint = p);
                return;
            }

            if (dimension is RotatedDimension rotated)
            {
                TrySetPoint(() => rotated.XLine1Point, p => info.XLine1Point = p);
                TrySetPoint(() => rotated.XLine2Point, p => info.XLine2Point = p);
                TrySetPoint(() => rotated.DimLinePoint, p => info.DimLinePoint = p);
                try
                {
                    info.Rotation = rotated.Rotation;
                }
                catch (Exception)
                {
                    info.Rotation = null;
                }

                return;
            }

            if (dimension is RadialDimension radial)
            {
                TrySetPoint(() => radial.Center, p => info.Center = p);
                TrySetPoint(() => radial.ChordPoint, p => info.ChordPoint = p);
                return;
            }

            if (dimension is DiametricDimension diametric)
            {
                TrySetPoint(() => diametric.ChordPoint, p => info.ChordPoint = p);
                TrySetPoint(() => diametric.FarChordPoint, p => info.FarChordPoint = p);
                return;
            }

            if (dimension is OrdinateDimension ordinate)
            {
                TrySetPoint(() => ordinate.DefiningPoint, p => info.XLine1Point = p);
                TrySetPoint(() => ordinate.LeaderEndPoint, p => info.DimLinePoint = p);
                TrySetPoint(() => ordinate.Origin, p => info.Center = p);
            }
        }

        private static void TrySetPoint(Func<Point3d> getter, Action<Point3dInfo> setter)
        {
            try
            {
                setter(GeometryHelper.ToPoint(getter()));
            }
            catch (Exception)
            {
                // Property may be unavailable on this host / dim subtype.
            }
        }

        private static void FillBlockReference(EntityInfo info, BlockReference br, Transaction tr)
        {
            info.BlockName = SafeBlockName(br);
            info.Position = GeometryHelper.ToPoint(br.Position);
            info.Rotation = br.Rotation;

            try
            {
                var scale = br.ScaleFactors;
                info.ScaleX = scale.X;
                info.ScaleY = scale.Y;
                info.ScaleZ = scale.Z;
            }
            catch (Exception)
            {
                info.ScaleX = null;
                info.ScaleY = null;
                info.ScaleZ = null;
            }

            info.Attributes = ReadAttributes(br, tr);
            info.DynamicProperties = ReadDynamicProperties(br);
        }

        private static Dictionary<string, string> ReadAttributes(BlockReference br, Transaction tr)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (br.AttributeCollection == null)
                {
                    return attributes;
                }

                foreach (ObjectId attId in br.AttributeCollection)
                {
                    if (attId.IsNull || attId.IsErased)
                    {
                        continue;
                    }

                    try
                    {
                        var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att == null)
                        {
                            continue;
                        }

                        var tag = att.Tag ?? string.Empty;
                        if (!attributes.ContainsKey(tag))
                        {
                            attributes[tag] = att.TextString ?? string.Empty;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip bad attribute; continue others.
                    }
                }
            }
            catch (Exception)
            {
                // Return whatever we collected.
            }

            return attributes.Count > 0 ? attributes : null;
        }

        private static Dictionary<string, object> ReadDynamicProperties(BlockReference br)
        {
            try
            {
                if (!br.IsDynamicBlock)
                {
                    return null;
                }

                var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var collection = br.DynamicBlockReferencePropertyCollection;
                if (collection == null)
                {
                    return null;
                }

                foreach (DynamicBlockReferenceProperty prop in collection)
                {
                    try
                    {
                        var name = prop.PropertyName ?? string.Empty;
                        if (string.IsNullOrEmpty(name) || props.ContainsKey(name))
                        {
                            continue;
                        }

                        props[name] = prop.Value;
                    }
                    catch (Exception)
                    {
                        // Skip one dynamic property.
                    }
                }

                return props.Count > 0 ? props : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Classify(Entity entity)
        {
            if (entity is BlockReference)
            {
                return "BlockReference";
            }

            if (entity is DBText)
            {
                return "DBText";
            }

            if (entity is MText)
            {
                return "MText";
            }

            if (entity is Dimension)
            {
                // Preserve concrete dim subtype (AlignedDimension, RotatedDimension, ...).
                return entity.GetType().Name;
            }

            if (entity is Leader)
            {
                return "Leader";
            }

            if (entity is MLeader)
            {
                return "MLeader";
            }

            if (entity is Line)
            {
                return "Line";
            }

            if (entity is Circle)
            {
                return "Circle";
            }

            if (entity is Arc)
            {
                return "Arc";
            }

            if (entity is Ellipse)
            {
                return "Ellipse";
            }

            if (entity is Polyline)
            {
                return "Polyline";
            }

            if (entity is Spline)
            {
                return "Spline";
            }

            if (entity is Hatch)
            {
                return "Hatch";
            }

            return entity.GetType().Name;
        }

        private static string SafeHandle(Entity entity)
        {
            try
            {
                return entity.Handle.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool SafeVisible(Entity entity)
        {
            try
            {
                return entity.Visible;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static string SafeBlockName(BlockReference br)
        {
            try
            {
                return br.Name ?? "(unnamed)";
            }
            catch (Exception)
            {
                return "(dynamic?)";
            }
        }

        private static string SafeString(Func<string> getter)
        {
            try
            {
                return getter() ?? string.Empty;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}
