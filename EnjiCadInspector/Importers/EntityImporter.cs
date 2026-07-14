using System;
using System.Collections.Generic;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Importers
{
    /// <summary>
    /// Recreates ModelSpace entities from exported <see cref="EntityInfo"/> records.
    /// </summary>
    public static class EntityImporter
    {
        /// <summary>
        /// Imports supported entity types into ModelSpace.
        /// </summary>
        public static ImportStats Import(
            Database db,
            Transaction tr,
            IList<EntityInfo> entities,
            Action<string, string> onWarn = null)
        {
            var stats = new ImportStats();
            if (entities == null || entities.Count == 0)
            {
                return stats;
            }

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            foreach (var info in entities)
            {
                if (info == null || string.IsNullOrWhiteSpace(info.Type))
                {
                    stats.Skipped++;
                    continue;
                }

                try
                {
                    Entity entity = CreateEntity(info, db, tr, onWarn);
                    if (entity == null)
                    {
                        stats.Skipped++;
                        onWarn?.Invoke(info.Handle ?? "?", "Skipped type: " + info.Type);
                        continue;
                    }

                    ApplyCommon(entity, info, db, tr, onWarn);
                    ms.AppendEntity(entity);
                    tr.AddNewlyCreatedDBObject(entity, true);

                    try
                    {
                        var dim = entity as Dimension;
                        if (dim != null)
                        {
                            dim.RecomputeDimensionBlock(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        onWarn?.Invoke(info.Handle ?? "?", "RecomputeDimensionBlock: " + ex.Message);
                    }

                    stats.Created++;
                }
                catch (Exception ex)
                {
                    stats.Errors++;
                    onWarn?.Invoke(info.Handle ?? "?", "Create failed (" + info.Type + "): " + ex.Message);
                }
            }

            return stats;
        }

        private static Entity CreateEntity(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            var type = info.Type;

            if (string.Equals(type, "Line", StringComparison.OrdinalIgnoreCase))
            {
                if (!GeometryHelper.HasPoint(info.StartPoint) || !GeometryHelper.HasPoint(info.EndPoint))
                {
                    return null;
                }

                return new Line(
                    GeometryHelper.ToCadPoint(info.StartPoint),
                    GeometryHelper.ToCadPoint(info.EndPoint));
            }

            if (string.Equals(type, "Circle", StringComparison.OrdinalIgnoreCase))
            {
                if (!GeometryHelper.HasPoint(info.Center) || !info.Radius.HasValue)
                {
                    return null;
                }

                return new Circle(GeometryHelper.ToCadPoint(info.Center), Vector3d.ZAxis, info.Radius.Value);
            }

            if (string.Equals(type, "Arc", StringComparison.OrdinalIgnoreCase))
            {
                if (!GeometryHelper.HasPoint(info.Center)
                    || !info.Radius.HasValue
                    || !info.StartAngle.HasValue
                    || !info.EndAngle.HasValue)
                {
                    return null;
                }

                return new Arc(
                    GeometryHelper.ToCadPoint(info.Center),
                    info.Radius.Value,
                    info.StartAngle.Value,
                    info.EndAngle.Value);
            }

            if (string.Equals(type, "DBText", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDbText(info, db, tr, onWarn);
            }

            if (string.Equals(type, "AlignedDimension", StringComparison.OrdinalIgnoreCase))
            {
                return CreateAlignedDimension(info, db, tr, onWarn);
            }

            if (string.Equals(type, "RotatedDimension", StringComparison.OrdinalIgnoreCase))
            {
                return CreateRotatedDimension(info, db, tr, onWarn);
            }

            if (string.Equals(type, "RadialDimension", StringComparison.OrdinalIgnoreCase))
            {
                return CreateRadialDimension(info, db, tr, onWarn);
            }

            if (string.Equals(type, "DiametricDimension", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDiametricDimension(info, db, tr, onWarn);
            }

            if (string.Equals(type, "OrdinateDimension", StringComparison.OrdinalIgnoreCase))
            {
                return CreateOrdinateDimension(info, db, tr, onWarn);
            }

            // Legacy export used Type="Dimension" without geometry — cannot recreate as true DIM.
            if (string.Equals(type, "Dimension", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return null;
        }

        private static DBText CreateDbText(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.Position))
            {
                return null;
            }

            var text = new DBText
            {
                Position = GeometryHelper.ToCadPoint(info.Position),
                Height = info.Height.HasValue && info.Height.Value > 0 ? info.Height.Value : 2.5,
                TextString = info.Text ?? string.Empty,
                Rotation = info.Rotation ?? 0.0
            };

            ApplyTextStyle(text, info.Style, db, tr, onWarn);
            return text;
        }

        private static AlignedDimension CreateAlignedDimension(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.XLine1Point)
                || !GeometryHelper.HasPoint(info.XLine2Point)
                || !GeometryHelper.HasPoint(info.DimLinePoint))
            {
                return null;
            }

            var dim = new AlignedDimension(
                GeometryHelper.ToCadPoint(info.XLine1Point),
                GeometryHelper.ToCadPoint(info.XLine2Point),
                GeometryHelper.ToCadPoint(info.DimLinePoint),
                info.DimensionText ?? string.Empty,
                ResolveDimStyleId(info.DimensionStyle, db, tr, onWarn));

            ApplyDimTextOverrides(dim, info);
            return dim;
        }

        private static RotatedDimension CreateRotatedDimension(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.XLine1Point)
                || !GeometryHelper.HasPoint(info.XLine2Point)
                || !GeometryHelper.HasPoint(info.DimLinePoint))
            {
                return null;
            }

            var rotation = info.Rotation ?? 0.0;
            var dim = new RotatedDimension(
                rotation,
                GeometryHelper.ToCadPoint(info.XLine1Point),
                GeometryHelper.ToCadPoint(info.XLine2Point),
                GeometryHelper.ToCadPoint(info.DimLinePoint),
                info.DimensionText ?? string.Empty,
                ResolveDimStyleId(info.DimensionStyle, db, tr, onWarn));

            ApplyDimTextOverrides(dim, info);
            return dim;
        }

        private static RadialDimension CreateRadialDimension(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.Center) || !GeometryHelper.HasPoint(info.ChordPoint))
            {
                return null;
            }

            var dim = new RadialDimension(
                GeometryHelper.ToCadPoint(info.Center),
                GeometryHelper.ToCadPoint(info.ChordPoint),
                0.0,
                info.DimensionText ?? string.Empty,
                ResolveDimStyleId(info.DimensionStyle, db, tr, onWarn));

            ApplyDimTextOverrides(dim, info);
            return dim;
        }

        private static DiametricDimension CreateDiametricDimension(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.ChordPoint) || !GeometryHelper.HasPoint(info.FarChordPoint))
            {
                return null;
            }

            var dim = new DiametricDimension(
                GeometryHelper.ToCadPoint(info.ChordPoint),
                GeometryHelper.ToCadPoint(info.FarChordPoint),
                0.0,
                info.DimensionText ?? string.Empty,
                ResolveDimStyleId(info.DimensionStyle, db, tr, onWarn));

            ApplyDimTextOverrides(dim, info);
            return dim;
        }

        private static OrdinateDimension CreateOrdinateDimension(
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!GeometryHelper.HasPoint(info.XLine1Point) || !GeometryHelper.HasPoint(info.DimLinePoint))
            {
                return null;
            }

            var defining = GeometryHelper.ToCadPoint(info.XLine1Point);
            var leaderEnd = GeometryHelper.ToCadPoint(info.DimLinePoint);
            var useXAxis = Math.Abs(leaderEnd.X - defining.X) >= Math.Abs(leaderEnd.Y - defining.Y);

            var dim = new OrdinateDimension(
                useXAxis,
                defining,
                leaderEnd,
                info.DimensionText ?? string.Empty,
                ResolveDimStyleId(info.DimensionStyle, db, tr, onWarn));

            if (GeometryHelper.HasPoint(info.Center))
            {
                try
                {
                    dim.Origin = GeometryHelper.ToCadPoint(info.Center);
                }
                catch (Exception)
                {
                    // Origin may be read-only on some hosts.
                }
            }

            ApplyDimTextOverrides(dim, info);
            return dim;
        }

        private static void ApplyDimTextOverrides(Dimension dim, EntityInfo info)
        {
            try
            {
                if (!string.IsNullOrEmpty(info.DimensionText))
                {
                    dim.DimensionText = info.DimensionText;
                }
            }
            catch (Exception)
            {
                // Ignore text override failure.
            }

            if (GeometryHelper.HasPoint(info.TextPosition))
            {
                try
                {
                    dim.TextPosition = GeometryHelper.ToCadPoint(info.TextPosition);
                }
                catch (Exception)
                {
                    // Ignore.
                }
            }
        }

        private static ObjectId ResolveDimStyleId(
            string styleName,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            try
            {
                var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                if (!string.IsNullOrWhiteSpace(styleName) && dst.Has(styleName))
                {
                    return dst[styleName];
                }

                if (dst.Has("Standard"))
                {
                    return dst["Standard"];
                }

                if (dst.Has("STANDARD"))
                {
                    return dst["STANDARD"];
                }

                // First available style.
                foreach (ObjectId id in dst)
                {
                    return id;
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke("dimstyle", ex.Message);
            }

            return ObjectId.Null;
        }

        private static void ApplyTextStyle(
            DBText text,
            string styleName,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            try
            {
                var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (!string.IsNullOrWhiteSpace(styleName) && tst.Has(styleName))
                {
                    text.TextStyleId = tst[styleName];
                    return;
                }

                if (tst.Has("Standard"))
                {
                    text.TextStyleId = tst["Standard"];
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke("textstyle", ex.Message);
            }
        }

        private static void ApplyCommon(
            Entity entity,
            EntityInfo info,
            Database db,
            Transaction tr,
            Action<string, string> onWarn)
        {
            if (!string.IsNullOrWhiteSpace(info.Layer))
            {
                try
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(info.Layer))
                    {
                        entity.Layer = info.Layer;
                    }
                }
                catch (Exception ex)
                {
                    onWarn?.Invoke(info.Handle ?? "?", "Layer: " + ex.Message);
                }
            }

            try
            {
                entity.Visible = info.Visible;
            }
            catch (Exception)
            {
                // Ignore.
            }

            ApplyEntityColor(entity, info.Color, onWarn, info.Handle);
            ApplyEntityLinetype(entity, info.Linetype, db, tr, onWarn, info.Handle);
        }

        private static void ApplyEntityColor(Entity entity, string colorText, Action<string, string> onWarn, string handle)
        {
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return;
            }

            try
            {
                if (colorText.IndexOf("ByLayer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entity.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                    return;
                }

                if (colorText.IndexOf("ByBlock", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entity.Color = Color.FromColorIndex(ColorMethod.ByBlock, 0);
                    return;
                }

                short index;
                if (TryParseAciIndex(colorText, out index))
                {
                    entity.Color = Color.FromColorIndex(ColorMethod.ByAci, index);
                    return;
                }

                var named = MapNamedColor(colorText);
                if (named.HasValue)
                {
                    entity.Color = Color.FromColorIndex(ColorMethod.ByAci, named.Value);
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke(handle ?? "?", "Color: " + ex.Message);
            }
        }

        private static void ApplyEntityLinetype(
            Entity entity,
            string linetypeName,
            Database db,
            Transaction tr,
            Action<string, string> onWarn,
            string handle)
        {
            if (string.IsNullOrWhiteSpace(linetypeName))
            {
                return;
            }

            try
            {
                if (linetypeName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)
                    || linetypeName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    entity.Linetype = linetypeName;
                    return;
                }

                var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(linetypeName))
                {
                    entity.Linetype = linetypeName;
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke(handle ?? "?", "Linetype: " + ex.Message);
            }
        }

        private static bool TryParseAciIndex(string colorText, out short index)
        {
            index = 0;
            var marker = "ColorIndex=";
            var pos = colorText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (pos >= 0)
            {
                var start = pos + marker.Length;
                var end = start;
                while (end < colorText.Length && char.IsDigit(colorText[end]))
                {
                    end++;
                }

                short parsed;
                if (short.TryParse(colorText.Substring(start, end - start), out parsed))
                {
                    index = parsed;
                    return true;
                }
            }

            short direct;
            if (short.TryParse(colorText.Trim(), out direct))
            {
                index = direct;
                return true;
            }

            return false;
        }

        private static short? MapNamedColor(string colorText)
        {
            var name = colorText.Trim();
            if (name.Equals("Red", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (name.Equals("Yellow", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (name.Equals("Green", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (name.Equals("Cyan", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (name.Equals("Blue", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (name.Equals("Magenta", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            if (name.Equals("White", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Black", StringComparison.OrdinalIgnoreCase))
            {
                return 7;
            }

            return null;
        }
    }
}
