using System;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;

namespace EnjiCadInspector.Helpers
{
    /// <summary>
    /// Drawing role for lines — maps to a shared layer + linetype.
    /// Prefer role/layer over per-entity linetype when many parts share styles.
    /// </summary>
    public enum LineRole
    {
        Visible,
        Hidden,
        Center,
        Dim,
        Text
    }

    /// <summary>
    /// Shared CAD layers / linetypes. Call <see cref="Ensure"/> once per draw transaction.
    /// </summary>
    public static class DrawingStyle
    {
        public const string LayerVisible = "TANK_VISIBLE";
        public const string LayerHidden = "TANK_HIDDEN";
        public const string LayerCenter = "TANK_CENTER";
        public const string LayerDim = "TANK_DIM";
        public const string LayerText = "TANK_TEXT";

        public const string LinetypeContinuous = "Continuous";
        public const string LinetypeHidden = "ENJI_HIDDEN";

        /// <summary>Global LTSCALE for the whole drawing.</summary>
        public const double DefaultLtScale = 30.0;

        /// <summary>
        /// Creates style layers and loads HIDDEN linetype if missing.
        /// Safe to call multiple times in the same document.
        /// </summary>
        public static void Ensure(Database db, Transaction tr)
        {
            if (db == null || tr == null)
            {
                throw new ArgumentNullException();
            }

            EnsureHiddenLinetype(db, tr);
            EnsureLinetype(db, tr, LinetypeContinuous);

            EnsureLayer(db, tr, LayerVisible, 4, LinetypeContinuous);
            EnsureLayer(db, tr, LayerHidden, 4, LinetypeHidden);
            EnsureLayer(db, tr, LayerCenter, 1, LinetypeContinuous);
            EnsureLayer(db, tr, LayerDim, 1, LinetypeContinuous);
            EnsureLayer(db, tr, LayerText, 2, LinetypeContinuous);

            try
            {
                db.Ltscale = DefaultLtScale;
            }
            catch
            {
                // Host may lock LTSCALE; layers still usable.
            }
        }

        public static string LayerFor(LineRole role)
        {
            switch (role)
            {
                case LineRole.Hidden:
                    return LayerHidden;
                case LineRole.Center:
                    return LayerCenter;
                case LineRole.Dim:
                    return LayerDim;
                case LineRole.Text:
                    return LayerText;
                default:
                    return LayerVisible;
            }
        }

        /// <summary>
        /// Ensures a named linetype exists (load from common .lin files if needed).
        /// Returns the resolved name, or Continuous if load failed.
        /// </summary>
        public static string EnsureLinetype(Database db, Transaction tr, string preferredName)
        {
            if (string.IsNullOrEmpty(preferredName))
            {
                return LinetypeContinuous;
            }

            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(preferredName))
            {
                return preferredName;
            }

            // Continuous is always present under Continuous / CONTINUOUS.
            if (string.Equals(preferredName, LinetypeContinuous, StringComparison.OrdinalIgnoreCase))
            {
                if (lt.Has("Continuous"))
                {
                    return "Continuous";
                }

                if (lt.Has("CONTINUOUS"))
                {
                    return "CONTINUOUS";
                }

                return preferredName;
            }

            var candidates = preferredName.Equals(LinetypeHidden, StringComparison.OrdinalIgnoreCase)
                ? new[] { "HIDDEN", "DASHED", "HIDDEN2", "DASHED2", "HIDDENX2" }
                : new[] { preferredName };

            foreach (var file in new[] { "acadiso.lin", "acad.lin", "gcad.lin", "gs.lin", "iso.lin" })
            {
                foreach (var name in candidates)
                {
                    try
                    {
                        db.LoadLineTypeFile(name, file);
                        return name;
                    }
                    catch
                    {
                        // Try next name / file.
                    }
                }
            }

            // Fall back to Continuous so callers never get null.
            if (lt.Has("Continuous"))
            {
                return "Continuous";
            }

            if (lt.Has("CONTINUOUS"))
            {
                return "CONTINUOUS";
            }

            return LinetypeContinuous;
        }

        /// <summary>
        /// Creates a deterministic short-dash linetype in drawing units.
        /// This avoids dependency on host-specific .lin files and remains visible
        /// on short hidden segments such as the 50 mm embedded lifting-eye sides.
        /// Pattern: 10 mm dash + 5 mm gap.
        /// </summary>
        private static string EnsureHiddenLinetype(Database db, Transaction tr)
        {
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(LinetypeHidden))
            {
                // Refresh pattern in case an older stub definition was Continuous-looking.
                try
                {
                    var existing = (LinetypeTableRecord)tr.GetObject(lt[LinetypeHidden], OpenMode.ForWrite);
                    existing.AsciiDescription = "Enji hidden __ __ __";
                    existing.PatternLength = 6.0;
                    existing.NumDashes = 2;
                    existing.SetDashLengthAt(0, 4.0);
                    existing.SetDashLengthAt(1, -2.0);
                }
                catch
                {
                    // Keep existing definition if host rejects rewrite.
                }

                return LinetypeHidden;
            }

            lt.UpgradeOpen();
            // Pattern: 4 mm dash + 2 mm gap (readable on short 50 mm stubs).
            var record = new LinetypeTableRecord
            {
                Name = LinetypeHidden,
                AsciiDescription = "Enji hidden __ __ __",
                PatternLength = 6.0,
                NumDashes = 2
            };
            record.SetDashLengthAt(0, 4.0);
            record.SetDashLengthAt(1, -2.0);

            lt.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
            return LinetypeHidden;
        }

        public static void EnsureLayer(
            Database db,
            Transaction tr,
            string name,
            short colorIndex,
            string linetypeName)
        {
            var resolvedLt = string.Equals(
                linetypeName,
                LinetypeHidden,
                StringComparison.OrdinalIgnoreCase)
                ? EnsureHiddenLinetype(db, tr)
                : EnsureLinetype(db, tr, linetypeName);
            var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (layerTable.Has(name))
            {
                // Refresh linetype on existing layer when Continuous was left from a failed load.
                var existing = (LayerTableRecord)tr.GetObject(layerTable[name], OpenMode.ForWrite);
                try
                {
                    if (ltTable.Has(resolvedLt))
                    {
                        existing.LinetypeObjectId = ltTable[resolvedLt];
                    }
                }
                catch
                {
                    // Keep previous linetype.
                }

                return;
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };

            if (ltTable.Has(resolvedLt))
            {
                layer.LinetypeObjectId = ltTable[resolvedLt];
            }

            layerTable.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }

    /// <summary>
    /// Unified entity append — pick <see cref="LineRole"/> instead of raw linetype strings.
    /// </summary>
    public static class CadDraw
    {
        public static void AppendLine(
            BlockTableRecord ms,
            Transaction tr,
            Point3d a,
            Point3d b,
            LineRole role,
            Color color = null)
        {
            if (ms == null || tr == null)
            {
                throw new ArgumentNullException();
            }

            DrawingStyle.Ensure(ms.Database, tr);

            var line = new Line(a, b);
            line.SetDatabaseDefaults();
            line.Layer = DrawingStyle.LayerFor(role);

            if (color != null)
            {
                line.Color = color;
            }
            else
            {
                line.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
            }

            if (role == LineRole.Hidden)
            {
                // Explicit + ByLayer: survives if layer linetype was Continuous from a prior run.
                line.Linetype = DrawingStyle.LinetypeHidden;
                // Cancel global LTSCALE so dash pattern is true drawing-mm length.
                var globalLt = ms.Database.Ltscale;
                line.LinetypeScale = globalLt > 1e-9 ? (1.0 / globalLt) : 1.0;
            }
            else
            {
                line.Linetype = "ByLayer";
            }

            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        public static void AppendArc(
            BlockTableRecord ms,
            Transaction tr,
            Point3d center,
            double radius,
            double startAng,
            double endAng,
            LineRole role,
            Color color = null)
        {
            if (ms == null || tr == null)
            {
                throw new ArgumentNullException();
            }

            DrawingStyle.Ensure(ms.Database, tr);

            startAng = NormalizeAngle(startAng);
            endAng = NormalizeAngle(endAng);
            if (Math.Abs(endAng - startAng) < 1e-12)
            {
                return;
            }

            var arc = new Arc(center, radius, startAng, endAng);
            arc.SetDatabaseDefaults();
            arc.Layer = DrawingStyle.LayerFor(role);
            arc.Color = color ?? Color.FromColorIndex(ColorMethod.ByLayer, 256);
            if (role == LineRole.Hidden)
            {
                arc.Linetype = DrawingStyle.LinetypeHidden;
                var globalLt = ms.Database.Ltscale;
                arc.LinetypeScale = globalLt > 1e-9 ? (1.0 / globalLt) : 1.0;
            }
            else
            {
                arc.Linetype = "ByLayer";
            }

            ms.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        public static void AppendCircle(
            BlockTableRecord ms,
            Transaction tr,
            Point3d center,
            double radius,
            LineRole role,
            Color color = null)
        {
            if (ms == null || tr == null)
            {
                throw new ArgumentNullException();
            }

            if (radius <= 0.0)
            {
                return;
            }

            DrawingStyle.Ensure(ms.Database, tr);

            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.SetDatabaseDefaults();
            circle.Layer = DrawingStyle.LayerFor(role);
            circle.Color = color ?? Color.FromColorIndex(ColorMethod.ByLayer, 256);

            if (role == LineRole.Hidden)
            {
                circle.Linetype = DrawingStyle.LinetypeHidden;
                var globalLt = ms.Database.Ltscale;
                circle.LinetypeScale = globalLt > 1e-9 ? (1.0 / globalLt) : 1.0;
            }
            else
            {
                circle.Linetype = "ByLayer";
            }

            ms.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
        }

        private static double NormalizeAngle(double ang)
        {
            while (ang < 0.0)
            {
                ang += 2.0 * Math.PI;
            }

            while (ang >= 2.0 * Math.PI)
            {
                ang -= 2.0 * Math.PI;
            }

            return ang;
        }
    }
}
