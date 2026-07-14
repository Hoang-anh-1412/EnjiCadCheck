using System;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Engine
{
    /// <summary>
    /// Draws elevation (hình chiếu đứng) and section A-A from body parameters.
    /// </summary>
    public static class TankViewDrawer
    {
        private const string LayerOutline = "TANK_OUTLINE";
        private const string LayerSection = "TANK_SECTION";
        private const string LayerCenter = "TANK_CENTER";
        private const string LayerDim = "TANK_DIM";
        private const string LayerText = "TANK_TEXT";

        private const int EllipseSegments = 24;

        /// <summary>
        /// Draws both views into ModelSpace. Origin is left body junction on elevation centerline.
        /// </summary>
        public static void Draw(Database db, Transaction tr, TankBodyParams p)
        {
            if (db == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            p.NormalizeDerived();
            EnsureLayers(db, tr);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var L = p.ShellLength;
            var R = p.Radius;
            var H = p.HeadDepth;
            var t = p.ShellThickness;

            // Elevation: centerline at Y = 0, body from X = 0 .. L, heads outside.
            DrawElevation(ms, tr, L, R, H, originY: 0.0);

            // Section A-A placed below elevation.
            var sectionOriginY = -(2.0 * R + Math.Max(400.0, R * 0.8));
            DrawSectionAa(ms, tr, L, R, H, t, originY: sectionOriginY);
        }

        private static void DrawElevation(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double H,
            double originY)
        {
            AppendClosedTankOutline(ms, tr, L, R, H, originY, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 4));

            // Centerline (body + heads)
            AppendLine(ms, tr,
                new Point3d(-H - 50, originY, 0),
                new Point3d(L + H + 50, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            // Body length dim
            AppendAlignedDim(ms, tr,
                new Point3d(0, originY - R, 0),
                new Point3d(L, originY - R, 0),
                new Point3d(L * 0.5, originY - R - Math.Max(120.0, R * 0.25), 0),
                LayerDim);

            // Left / right head depth dims
            AppendAlignedDim(ms, tr,
                new Point3d(-H, originY - R, 0),
                new Point3d(0, originY - R, 0),
                new Point3d(-H * 0.5, originY - R - Math.Max(120.0, R * 0.25), 0),
                LayerDim);
            AppendAlignedDim(ms, tr,
                new Point3d(L, originY - R, 0),
                new Point3d(L + H, originY - R, 0),
                new Point3d(L + H * 0.5, originY - R - Math.Max(120.0, R * 0.25), 0),
                LayerDim);

            // Diameter dim (vertical)
            AppendAlignedDim(ms, tr,
                new Point3d(-H - Math.Max(80.0, H * 0.2), originY - R, 0),
                new Point3d(-H - Math.Max(80.0, H * 0.2), originY + R, 0),
                new Point3d(-H - Math.Max(160.0, H * 0.45), originY, 0),
                LayerDim);

            AppendText(ms, tr,
                "Hình chiếu đứng (立面図)",
                new Point3d(L * 0.5, originY - R - Math.Max(220.0, R * 0.45), 0),
                Math.Max(40.0, R * 0.08),
                LayerText);
        }

        private static void DrawSectionAa(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double H,
            double thickness,
            double originY)
        {
            var rInner = R - thickness;
            var hInner = Math.Max(H - thickness, H * (rInner / R));

            // Outer shell
            AppendClosedTankOutline(ms, tr, L, R, H, originY, LayerSection, Color.FromColorIndex(ColorMethod.ByAci, 4));

            // Inner shell (cut face)
            AppendClosedTankOutline(ms, tr, L, rInner, hInner, originY, LayerSection, Color.FromColorIndex(ColorMethod.ByAci, 3));

            // Centerline
            AppendLine(ms, tr,
                new Point3d(-H - 50, originY, 0),
                new Point3d(L + H + 50, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            // Thickness callouts (top / bottom at mid body)
            var midX = L * 0.5;
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY + rInner, 0),
                new Point3d(midX, originY + R, 0),
                new Point3d(midX + Math.Max(80.0, R * 0.15), originY + R - thickness * 0.5, 0),
                LayerDim);
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY - R, 0),
                new Point3d(midX, originY - rInner, 0),
                new Point3d(midX + Math.Max(80.0, R * 0.15), originY - R + thickness * 0.5, 0),
                LayerDim);

            // Inner diameter dim
            AppendAlignedDim(ms, tr,
                new Point3d(-H - Math.Max(80.0, H * 0.2), originY - rInner, 0),
                new Point3d(-H - Math.Max(80.0, H * 0.2), originY + rInner, 0),
                new Point3d(-H - Math.Max(200.0, H * 0.55), originY, 0),
                LayerDim);

            // Overall body + heads length
            AppendAlignedDim(ms, tr,
                new Point3d(-H, originY - R, 0),
                new Point3d(L + H, originY - R, 0),
                new Point3d(L * 0.5, originY - R - Math.Max(140.0, R * 0.3), 0),
                LayerDim);

            AppendText(ms, tr,
                "A-A 断面図",
                new Point3d(L * 0.5, originY - R - Math.Max(240.0, R * 0.5), 0),
                Math.Max(40.0, R * 0.08),
                LayerText);

            AppendText(ms, tr,
                string.Format("タンク内径 {0:0}(ID)", rInner * 2.0),
                new Point3d(-H - Math.Max(280.0, H * 0.7), originY + rInner * 0.35, 0),
                Math.Max(28.0, R * 0.055),
                LayerText);
        }

        private static void AppendClosedTankOutline(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double H,
            double originY,
            string layer,
            Color color)
        {
            var pl = new Polyline();
            pl.SetDatabaseDefaults();
            pl.Layer = layer;
            pl.Color = color;
            pl.Closed = true;

            var idx = 0;

            // Start at left body junction top (0, +R), go clockwise: top body → right head → bottom body → left head.
            pl.AddVertexAt(idx++, new Point2d(0, originY + R), 0, 0, 0);
            pl.AddVertexAt(idx++, new Point2d(L, originY + R), 0, 0, 0);

            // Right elliptical head (parametric θ = π/2 → -π/2 through tip)
            for (var i = 1; i <= EllipseSegments; i++)
            {
                var theta = Math.PI / 2.0 - Math.PI * i / EllipseSegments;
                var x = L + H * Math.Cos(theta);
                var y = originY + R * Math.Sin(theta);
                pl.AddVertexAt(idx++, new Point2d(x, y), 0, 0, 0);
            }

            pl.AddVertexAt(idx++, new Point2d(0, originY - R), 0, 0, 0);

            // Left elliptical head (θ = -π/2 → π/2 through tip at x = -H)
            for (var i = 1; i <= EllipseSegments; i++)
            {
                var theta = -Math.PI / 2.0 + Math.PI * i / EllipseSegments;
                var x = -H * Math.Cos(theta);
                var y = originY + R * Math.Sin(theta);
                pl.AddVertexAt(idx++, new Point2d(x, y), 0, 0, 0);
            }

            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }

        private static void AppendLine(
            BlockTableRecord ms,
            Transaction tr,
            Point3d a,
            Point3d b,
            string layer,
            Color color)
        {
            var line = new Line(a, b);
            line.SetDatabaseDefaults();
            line.Layer = layer;
            line.Color = color;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private static void AppendAlignedDim(
            BlockTableRecord ms,
            Transaction tr,
            Point3d p1,
            Point3d p2,
            Point3d dimLine,
            string layer)
        {
            var dim = new AlignedDimension(p1, p2, dimLine, null, ObjectId.Null);
            dim.SetDatabaseDefaults();
            dim.Layer = layer;
            dim.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
            ms.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            try
            {
                dim.RecomputeDimensionBlock(true);
            }
            catch
            {
                // Host may skip recompute; geometry still present.
            }
        }

        private static void AppendText(
            BlockTableRecord ms,
            Transaction tr,
            string content,
            Point3d position,
            double height,
            string layer)
        {
            var text = new DBText
            {
                Position = position,
                Height = height,
                TextString = content
            };
            text.SetDatabaseDefaults();
            text.Layer = layer;
            text.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
            text.HorizontalMode = TextHorizontalMode.TextCenter;
            text.AlignmentPoint = position;
            text.AdjustAlignment(ms.Database);
            ms.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
        }

        private static void EnsureLayers(Database db, Transaction tr)
        {
            EnsureLayer(db, tr, LayerOutline, 4);
            EnsureLayer(db, tr, LayerSection, 4);
            EnsureLayer(db, tr, LayerCenter, 1);
            EnsureLayer(db, tr, LayerDim, 1);
            EnsureLayer(db, tr, LayerText, 2);
        }

        private static void EnsureLayer(Database db, Transaction tr, string name, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name))
            {
                return;
            }

            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }
}
