using System;
using System.Collections.Generic;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Engine
{
    /// <summary>
    /// Draws elevation and section A-A from body parameters.
    /// Base shell = inner (param radius); outer = Curve.GetOffsetCurves(+thickness).
    /// </summary>
    public static class TankViewDrawer
    {
        private const string LayerOutline = "TANK_OUTLINE";
        private const string LayerSection = "TANK_SECTION";
        private const string LayerCenter = "TANK_CENTER";
        private const string LayerDim = "TANK_DIM";
        private const string LayerText = "TANK_TEXT";
        private const string LayerSupport = "TANK_SUPPORT";

        /// <summary>Equilateral support-leg side length (mm).</summary>
        private const double SupportLegSide = 164.0;

        /// <summary>Offset of outer legs from shell mid-length (mm).</summary>
        private const double SupportLegSpanFromMid = 1600.0;

        /// <summary>Shell plate length along axis (mm).</summary>
        private const double ShellPlateLength = 1524.0;

        /// <summary>Inset from each head–shell junction before first/last plate edge (mm).</summary>
        private const double ShellPlateEndOffset = 20.0;

        /// <summary>Lifting eye offset from horizontal centerline on elevation (mm).</summary>
        private const double LiftingOffsetFromCenterline = 300.0;

        /// <summary>Lifting eye offset from plate seam (mép tấm) along shell axis (mm).</summary>
        private const double LiftingOffsetFromPlateEdge = 300.0;

        /// <summary>
        /// Draws both views. Param radius = inner; outer from offset by shell thickness.
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
            var R = p.Radius; // inner
            var H = p.HeadDepth;
            var t = p.ShellThickness;
            var rk = p.KnuckleRadius;
            var rc = p.CrownRadius;
            var Ro = R + t;
            var Ho = H + t;

            DrawElevation(ms, tr, L, R, H, Ho, Ro, rk, rc, t, originY: 0.0);

            var sectionOriginY = -(2.0 * Ro + Math.Max(400.0, Ro * 0.8));
            DrawSectionAa(ms, tr, L, R, H, Ho, Ro, t, rk, rc, originY: sectionOriginY);
        }

        /// <summary>
        /// TAOTHANTANK: chỉ vẽ thân trụ — hình chiếu đứng + hình chiếu bằng,
        /// mỗi view có đường tâm và kích thước. Không vẽ đầu bồn, mép tấm, chân, lỗ, A-A.
        /// Trả về layout neo đầu/cuối theo từng hình chiếu để cache Document.
        /// </summary>
        public static TankShellLayout DrawShellOnly(Database db, Transaction tr, TankBodyParams p)
        {
            if (db == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            // Chuẩn hóa tham số suy ra (crown/knuckle/head) và tạo layer nếu chưa có.
            p.NormalizeDerived();
            EnsureLayers(db, tr);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var L = p.ShellLength;   // Chiều dài thân
            var R = p.Radius;        // Bán kính trong
            var t = p.ShellThickness;
            var Ro = R + t;          // Bán kính ngoài = trong + bề dày

            // Hình chiếu đứng đặt tại Y = 0.
            const double elevationOriginY = 0.0;
            DrawShellOnlyElevation(ms, tr, L, R, Ro, t, originY: elevationOriginY);

            // Hình chiếu bằng đặt phía dưới, chừa khoảng cho dim + tiêu đề.
            var planOriginY = -(2.0 * Ro + Math.Max(500.0, Ro * 1.2));
            DrawShellOnlyPlan(ms, tr, L, R, Ro, t, originY: planOriginY);

            // Snapshot neo khớp đúng originY vừa vẽ — lệnh sau đọc LeftCenter/RightCenter theo view.
            return TankShellLayout.FromShellDraw(
                L, R, Ro, t, elevationOriginY, planOriginY);
        }

        /// <summary>
        /// Vẽ hình chiếu đứng (立面図): nét trong/ngoài thân, đường tâm trục, dim L / D / t.
        /// </summary>
        private static void DrawShellOnlyElevation(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double Ro,
            double thickness,
            double originY)
        {
            // Nét trong (đường sinh trên + dưới theo bán kính trong).
            var inner = BuildShellOnlyCurves(L, R, originY);
            AppendCurves(ms, tr, inner, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 3));

            // Nét ngoài = offset ra ngoài một khoảng bề dày.
            var outer = OffsetCurvesOutward(inner, thickness, originY);
            AppendCurves(ms, tr, outer, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 4));

            // Đường tâm theo trục thân (kéo dài nhẹ hai đầu).
            AppendLine(ms, tr,
                new Point3d(-1000.0, originY, 0),
                new Point3d(L + 1000.0, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            var dimTextH = Math.Max(40.0, Ro * 0.075);
            var dimGap = Math.Max(120.0, Ro * 0.25);

            // Dim chiều dài thân — phía dưới nét ngoài.
            AppendAlignedDim(ms, tr,
                new Point3d(0, originY - Ro, 0),
                new Point3d(L, originY - Ro, 0),
                new Point3d(L * 0.5, originY - Ro - dimGap, 0),
                LayerDim,
                dimTextH);


            // Dim bề dày tại giữa thân (mặt trong → mặt ngoài, phía trên).
            var midX = L * 0.5;
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY + R, 0),
                new Point3d(midX, originY + Ro, 0),
                new Point3d(midX + Math.Max(80.0, Ro * 0.15), originY + R + thickness * 0.5, 0),
                LayerDim,
                dimTextH);

            // Nhãn view.
            AppendText(ms, tr,
                "Hình chiếu đứng (立面図)",
                new Point3d(L * 0.5, originY - Ro - Math.Max(220.0, Ro * 0.45), 0),
                Math.Max(40.0, Ro * 0.08),
                LayerText);
        }

        /// <summary>
        /// Vẽ hình chiếu bằng (平面図): nhìn từ trên (L × đường kính),
        /// đường tâm ngang/dọc, dim L / D / t.
        /// </summary>
        private static void DrawShellOnlyPlan(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double Ro,
            double thickness,
            double originY)
        {
            // Cùng cách dựng nét trong/ngoài như hình chiếu đứng.
            var inner = BuildShellOnlyCurves(L, R, originY);
            AppendCurves(ms, tr, inner, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 3));

            var outer = OffsetCurvesOutward(inner, thickness, originY);
            AppendCurves(ms, tr, outer, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 4));

            // Đường tâm dọc theo chiều dài + đường tâm ngang theo đường kính.
            AppendLine(ms, tr,
                new Point3d(-1000.0, originY, 0),
                new Point3d(L + 1000.0, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));
            AppendLine(ms, tr,
                new Point3d(L * 0.5, originY - Ro - 50.0, 0),
                new Point3d(L * 0.5, originY + Ro + 50.0, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            var dimTextH = Math.Max(40.0, Ro * 0.075);
            var dimGap = Math.Max(120.0, Ro * 0.25);

            // Dim chiều dài thân — phía dưới.
            AppendAlignedDim(ms, tr,
                new Point3d(0, originY - Ro, 0),
                new Point3d(L, originY - Ro, 0),
                new Point3d(L * 0.5, originY - Ro - dimGap, 0),
                LayerDim,
                dimTextH);


            // Dim bề dày tại giữa thân.
            var midX = L * 0.5;
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY + R, 0),
                new Point3d(midX, originY + Ro, 0),
                new Point3d(midX + Math.Max(80.0, Ro * 0.15), originY + R + thickness * 0.5, 0),
                LayerDim,
                dimTextH);

            AppendText(ms, tr,
                "Hình chiếu bằng (平面図)",
                new Point3d(L * 0.5, originY - Ro - Math.Max(220.0, Ro * 0.45), 0),
                Math.Max(40.0, Ro * 0.08),
                LayerText);
        }

        /// <summary>
        /// Tạo 2 đường sinh thân (trên + dưới). Không vẽ nét đứng ở hai đầu x=0 / x=L.
        /// Dùng chung cho hình chiếu đứng và hình chiếu bằng.
        /// </summary>
        private static List<Curve> BuildShellOnlyCurves(double L, double R, double originY)
        {
            var curves = new List<Curve>
            {
                // Đường trên: từ đầu trái → đầu phải
                new Line(new Point3d(0, originY + R, 0), new Point3d(L, originY + R, 0)),
                // Đường dưới: từ đầu phải → đầu trái
                new Line(new Point3d(L, originY - R, 0), new Point3d(0, originY - R, 0))
            };
            return curves;
        }

        private static void DrawElevation(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double H,
            double Ho,
            double Ro,
            double rk,
            double rc,
            double thickness,
            double originY)
        {
            // Inner shell from params; outer = offset +t (same as section A-A).
            var inner = BuildTankOutlineCurves(L, R, rk, rc, originY);
            AppendCurves(ms, tr, inner, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 3));

            var outer = OffsetCurvesOutward(inner, thickness, originY);
            AppendCurves(ms, tr, outer, LayerOutline, Color.FromColorIndex(ColorMethod.ByAci, 4));

            DrawShellPlateSeams(ms, tr, L, Ro, originY, LayerOutline,
                Color.FromColorIndex(ColorMethod.ByAci, 3));

            var dimTextH = Math.Max(40.0, Ro * 0.075);
            DrawLiftingEyesElevation(ms, tr, L, originY, dimTextH);
            DrawHole1Elevation(ms, tr, L, originY, Ro);

            AppendLine(ms, tr,
                new Point3d(-Ho - 50, originY, 0),
                new Point3d(L + Ho + 50, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            DrawShellPlateSeamDims(ms, tr, L, Ro, originY, dimTextH);

            AppendAlignedDim(ms, tr,
                new Point3d(0, originY - Ro, 0),
                new Point3d(L, originY - Ro, 0),
                new Point3d(L * 0.5, originY - Ro - Math.Max(120.0, Ro * 0.25), 0),
                LayerDim,
                dimTextH);

            AppendAlignedDim(ms, tr,
                new Point3d(-Ho, originY - Ro, 0),
                new Point3d(0, originY - Ro, 0),
                new Point3d(-Ho * 0.5, originY - Ro - Math.Max(120.0, Ro * 0.25), 0),
                LayerDim,
                dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(L, originY - Ro, 0),
                new Point3d(L + Ho, originY - Ro, 0),
                new Point3d(L + Ho * 0.5, originY - Ro - Math.Max(120.0, Ro * 0.25), 0),
                LayerDim,
                dimTextH);

            AppendAlignedDim(ms, tr,
                new Point3d(-Ho - Math.Max(80.0, Ho * 0.2), originY - Ro, 0),
                new Point3d(-Ho - Math.Max(80.0, Ho * 0.2), originY + Ro, 0),
                new Point3d(-Ho - Math.Max(160.0, Ho * 0.45), originY, 0),
                LayerDim,
                dimTextH);

            AppendText(ms, tr,
                "Hình chiếu đứng (立面図)",
                new Point3d(L * 0.5, originY - Ro - Math.Max(220.0, Ro * 0.45), 0),
                Math.Max(40.0, Ro * 0.08),
                LayerText);
        }

        private static void DrawSectionAa(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double H,
            double Ho,
            double Ro,
            double thickness,
            double rk,
            double rc,
            double originY)
        {
            // Inner shell from params, then offset +t → outer (no second geometry formula).
            var inner = BuildTankOutlineCurves(L, R, rk, rc, originY);
            AppendCurves(ms, tr, inner, LayerSection, Color.FromColorIndex(ColorMethod.ByAci, 3));

            var outer = OffsetCurvesOutward(inner, thickness, originY);
            AppendCurves(ms, tr, outer, LayerSection, Color.FromColorIndex(ColorMethod.ByAci, 4));

            DrawShellPlateSeams(ms, tr, L, Ro, originY, LayerSection,
                Color.FromColorIndex(ColorMethod.ByAci, 3));

            DrawLiftingEyesSection(ms, tr, L, R, Ro, originY, Math.Max(40.0, Ro * 0.075));

            DrawSupportLegs(ms, tr, L, Ro, originY);

            AppendLine(ms, tr,
                new Point3d(-Ho - 50, originY, 0),
                new Point3d(L + Ho + 50, originY, 0),
                LayerCenter,
                Color.FromColorIndex(ColorMethod.ByAci, 1));

            var midX = L * 0.5;
            var dimTextH = Math.Max(40.0, Ro * 0.075);
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY + R, 0),
                new Point3d(midX, originY + Ro, 0),
                new Point3d(midX + Math.Max(80.0, Ro * 0.15), originY + R + thickness * 0.5, 0),
                LayerDim,
                dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(midX, originY - Ro, 0),
                new Point3d(midX, originY - R, 0),
                new Point3d(midX + Math.Max(80.0, Ro * 0.15), originY - R - thickness * 0.5, 0),
                LayerDim,
                dimTextH);

            AppendAlignedDim(ms, tr,
                new Point3d(-Ho - Math.Max(80.0, Ho * 0.2), originY - R, 0),
                new Point3d(-Ho - Math.Max(80.0, Ho * 0.2), originY + R, 0),
                new Point3d(-Ho - Math.Max(200.0, Ho * 0.55), originY, 0),
                LayerDim,
                dimTextH);

            AppendAlignedDim(ms, tr,
                new Point3d(-Ho, originY - Ro, 0),
                new Point3d(L + Ho, originY - Ro, 0),
                new Point3d(L * 0.5, originY - Ro - Math.Max(140.0, Ro * 0.3), 0),
                LayerDim,
                dimTextH);

            AppendText(ms, tr,
                "A-A 断面図",
                new Point3d(L * 0.5, originY - Ro - Math.Max(240.0, Ro * 0.5), 0),
                Math.Max(40.0, Ro * 0.08),
                LayerText);

            AppendText(ms, tr,
                string.Format("タンク内径 {0:0}(ID)", R * 2.0),
                new Point3d(-Ho - Math.Max(280.0, Ho * 0.7), originY + R * 0.35, 0),
                Math.Max(28.0, Ro * 0.055),
                LayerText);
        }

        /// <summary>
        /// Elevation: 4× lifting eyes at ±300 from centerline;
        /// near edge of each lug is 300 from the end plate seam (not the center).
        /// </summary>
        private static void DrawLiftingEyesElevation(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double originY,
            double dimTextH)
        {
            var eye = LiftingEyeParams.CreateDefaults();
            var w = eye.BaseWidth;
            var h = eye.ElevationHeight;
            var halfW = w * 0.5;
            var color = Color.FromColorIndex(ColorMethod.ByAci, 4);

            var leftSeam = ShellPlateEndOffset;
            var rightSeam = L - ShellPlateEndOffset;
            if (rightSeam - leftSeam < (LiftingOffsetFromPlateEdge + halfW) * 2.0)
            {
                return;
            }

            // Near edge of lug = seam ± 300 → center is further inward by half width.
            var leftNear = leftSeam + LiftingOffsetFromPlateEdge;
            var rightNear = rightSeam - LiftingOffsetFromPlateEdge;
            var xs = new[]
            {
                leftNear + halfW,
                rightNear - halfW
            };
            var ys = new[]
            {
                originY + LiftingOffsetFromCenterline,
                originY - LiftingOffsetFromCenterline
            };

            foreach (var cx in xs)
            {
                foreach (var cy in ys)
                {
                    LiftingEyeDrawer.DrawElevationSymbol(
                        ms, tr, new Point3d(cx, cy, 0), w, h, color);
                }
            }

            // Dims 300 from centerline (match reference: left of left-hand pair).
            var dimX = leftNear - Math.Max(40.0, w * 0.3);
            AppendAlignedDim(ms, tr,
                new Point3d(dimX, originY, 0),
                new Point3d(dimX, ys[0], 0),
                new Point3d(dimX - Math.Max(50.0, dimTextH), (originY + ys[0]) * 0.5, 0),
                LayerDim,
                dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(dimX, originY, 0),
                new Point3d(dimX, ys[1], 0),
                new Point3d(dimX - Math.Max(50.0, dimTextH), (originY + ys[1]) * 0.5, 0),
                LayerDim,
                dimTextH);

            // Dims 300 from plate seam to near edge of lug (top row).
            var plateDimY = ys[0] + h * 0.5 + Math.Max(40.0, dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(leftSeam, ys[0], 0),
                new Point3d(leftNear, ys[0], 0),
                new Point3d((leftSeam + leftNear) * 0.5, plateDimY, 0),
                LayerDim,
                dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(rightNear, ys[0], 0),
                new Point3d(rightSeam, ys[0], 0),
                new Point3d((rightNear + rightSeam) * 0.5, plateDimY, 0),
                LayerDim,
                dimTextH);
        }

        /// <summary>
        /// Hole 1 on elevation — center 350 mm from right shell–head junction.
        /// </summary>
        private static void DrawHole1Elevation(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double originY,
            double Ro)
        {
            var hole = Hole1Params.CreateDefaults();
            var color = Color.FromColorIndex(ColorMethod.ByAci, 4);
            Hole1Drawer.DrawElevation(ms, tr, hole, L, originY, Ro, color);
        }

        /// <summary>
        /// Section A-A: lifting eye profiles on the outer shell face (not perched as a
        /// flat edge pad only); wall penetration drawn dashed. Near edge 300 from seams.
        /// </summary>
        private static void DrawLiftingEyesSection(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double R,
            double Ro,
            double originY,
            double dimTextH)
        {
            var eye = LiftingEyeParams.CreateDefaults();
            var halfW = eye.BaseWidth * 0.5;
            var color = Color.FromColorIndex(ColorMethod.ByAci, 4);

            var leftSeam = ShellPlateEndOffset;
            var rightSeam = L - ShellPlateEndOffset;
            if (rightSeam - leftSeam < (LiftingOffsetFromPlateEdge + halfW) * 2.0)
            {
                return;
            }

            var leftNear = leftSeam + LiftingOffsetFromPlateEdge;
            var rightNear = rightSeam - LiftingOffsetFromPlateEdge;
            var outerY = originY + Ro;

            // Left: outer-left edge at leftNear. Right: outer-right edge at rightNear (mirrored).
            LiftingEyeDrawer.DrawProfileThroughShell(
                ms, tr, eye, new Point3d(leftNear, outerY, 0), eye.SectionHiddenDepth, color);
            LiftingEyeDrawer.DrawProfileThroughShellRight(
                ms, tr, eye, rightNear, outerY, eye.SectionHiddenDepth, color);

            // Dims 300: plate seam → near edge of lug (on outer face).
            var visibleRight = eye.RightHeight - eye.SectionHiddenDepth;
            var dimY = outerY + visibleRight + Math.Max(60.0, dimTextH * 1.5);
            AppendAlignedDim(ms, tr,
                new Point3d(leftSeam, outerY, 0),
                new Point3d(leftNear, outerY, 0),
                new Point3d((leftSeam + leftNear) * 0.5, dimY, 0),
                LayerDim,
                dimTextH);
            AppendAlignedDim(ms, tr,
                new Point3d(rightNear, outerY, 0),
                new Point3d(rightSeam, outerY, 0),
                new Point3d((rightNear + rightSeam) * 0.5, dimY, 0),
                LayerDim,
                dimTextH);
        }

        /// <summary>
        /// Vertical shell plate seams: inset ShellPlateEndOffset from each junction,
        /// then every ShellPlateLength along the cylindrical body.
        /// </summary>
        private static void DrawShellPlateSeams(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double Ro,
            double originY,
            string layer,
            Color color)
        {
            foreach (var x in EnumerateShellPlateSeamXs(L))
            {
                AppendLine(ms, tr,
                    new Point3d(x, originY - Ro, 0),
                    new Point3d(x, originY + Ro, 0),
                    layer,
                    color);
            }
        }

        /// <summary>
        /// Chain dims above elevation: end offsets + plate spans matching seam layout.
        /// </summary>
        private static void DrawShellPlateSeamDims(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double Ro,
            double originY,
            double dimTextH)
        {
            var xs = new List<double> { 0.0 };
            xs.AddRange(EnumerateShellPlateSeamXs(L));
            if (xs[xs.Count - 1] < L - 1e-9)
            {
                xs.Add(L);
            }

            var dimY = originY + Ro + Math.Max(100.0, Ro * 0.2);
            for (var i = 0; i < xs.Count - 1; i++)
            {
                var x0 = xs[i];
                var x1 = xs[i + 1];
                if (x1 - x0 <= 1e-9)
                {
                    continue;
                }

                AppendAlignedDim(ms, tr,
                    new Point3d(x0, originY + Ro, 0),
                    new Point3d(x1, originY + Ro, 0),
                    new Point3d((x0 + x1) * 0.5, dimY, 0),
                    LayerDim,
                    dimTextH);
            }
        }

        private static IEnumerable<double> EnumerateShellPlateSeamXs(double L)
        {
            if (L <= ShellPlateEndOffset * 2.0)
            {
                yield break;
            }

            var rightLimit = L - ShellPlateEndOffset;
            yield return ShellPlateEndOffset;

            var x = ShellPlateEndOffset + ShellPlateLength;
            while (x < rightLimit - 1e-6)
            {
                yield return x;
                x += ShellPlateLength;
            }

            if (rightLimit - ShellPlateEndOffset > 1e-6)
            {
                yield return rightLimit;
            }
        }

        /// <summary>
        /// Three equilateral supports under A-A outer shell bottom: mid and ±1600.
        /// Apex touches Y=originY-Ro; outline Lines + ANSI31 hatch (ACI red).
        /// </summary>
        private static void DrawSupportLegs(
            BlockTableRecord ms,
            Transaction tr,
            double L,
            double Ro,
            double originY)
        {
            var midX = L * 0.5;
            var apexY = originY - Ro;
            var centers = new[]
            {
                midX - SupportLegSpanFromMid,
                midX,
                midX + SupportLegSpanFromMid
            };

            foreach (var cx in centers)
            {
                AppendEquilateralSupportLeg(ms, tr, cx, apexY, SupportLegSide);
            }
        }

        private static void AppendEquilateralSupportLeg(
            BlockTableRecord ms,
            Transaction tr,
            double centerX,
            double apexY,
            double side)
        {
            var h = side * Math.Sqrt(3.0) / 2.0;
            var half = side * 0.5;
            var apex = new Point3d(centerX, apexY, 0);
            var bottomLeft = new Point3d(centerX - half, apexY - h, 0);
            var bottomRight = new Point3d(centerX + half, apexY - h, 0);
            var red = Color.FromColorIndex(ColorMethod.ByAci, 1);

            // Closed polyline = outline + reliable hatch boundary (point-loop often clips mid-fill).
            var boundary = new Polyline();
            boundary.AddVertexAt(0, new Point2d(apex.X, apex.Y), 0, 0, 0);
            boundary.AddVertexAt(1, new Point2d(bottomRight.X, bottomRight.Y), 0, 0, 0);
            boundary.AddVertexAt(2, new Point2d(bottomLeft.X, bottomLeft.Y), 0, 0, 0);
            boundary.Closed = true;
            boundary.SetDatabaseDefaults();
            boundary.Layer = LayerSupport;
            boundary.Color = red;
            ms.AppendEntity(boundary);
            tr.AddNewlyCreatedDBObject(boundary, true);

            try
            {
                var hatch = new Hatch();
                hatch.SetDatabaseDefaults();
                hatch.Layer = LayerSupport;
                hatch.Color = red;
                hatch.Normal = Vector3d.ZAxis;
                hatch.Elevation = 0.0;
                ms.AppendEntity(hatch);
                tr.AddNewlyCreatedDBObject(hatch, true);

                // Scale before+after SetHatchPattern so host keeps it (avoids MAXHATCH truncate).
                const double patternScale = 5.0;
                hatch.PatternScale = patternScale;
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                hatch.PatternScale = patternScale;
                hatch.Associative = true;

                var ids = new ObjectIdCollection();
                ids.Add(boundary.ObjectId);
                hatch.AppendLoop(HatchLoopTypes.External, ids);
                hatch.EvaluateHatch(true);
            }
            catch
            {
                // Host may lack ANSI31; polyline outline remains.
            }
        }

        /// <summary>
        /// CCW around cavity so GetOffsetCurves(+dist) goes outward into the wall / outer face.
        /// Top LTR → right head → bottom RTL → left head.
        /// </summary>
        private static List<Curve> BuildTankOutlineCurves(
            double L,
            double R,
            double rk,
            double rc,
            double originY)
        {
            var curves = new List<Curve>();

            curves.Add(new Line(new Point3d(0, originY + R, 0), new Point3d(L, originY + R, 0)));
            AddTorisphericalHead(curves, junctionX: L, R, rk, rc, originY, isLeft: false);
            curves.Add(new Line(new Point3d(L, originY - R, 0), new Point3d(0, originY - R, 0)));
            AddTorisphericalHead(curves, junctionX: 0.0, R, rk, rc, originY, isLeft: true);

            return curves;
        }

        private static void AddTorisphericalHead(
            List<Curve> curves,
            double junctionX,
            double R,
            double rk,
            double rc,
            double originY,
            bool isLeft)
        {
            var a = rc - rk;
            var b = R - rk;
            var xcLocal = Math.Sqrt(a * a - b * b);
            var sign = isLeft ? 1.0 : -1.0;

            var ckTop = new Point3d(junctionX, originY + b, 0);
            var ckBot = new Point3d(junctionX, originY - b, 0);
            var cc = new Point3d(junctionX + sign * xcLocal, originY, 0);

            var joinTop = PointOnRay(ckTop, ckTop.X - cc.X, ckTop.Y - cc.Y, rk / a);
            var joinBot = PointOnRay(ckBot, ckBot.X - cc.X, ckBot.Y - cc.Y, rk / a);

            var angJuncTop = Math.PI / 2.0;
            var angJoinTop = Math.Atan2(joinTop.Y - ckTop.Y, joinTop.X - ckTop.X);
            var angCrownTop = Math.Atan2(joinTop.Y - cc.Y, joinTop.X - cc.X);
            var angCrownBot = Math.Atan2(joinBot.Y - cc.Y, joinBot.X - cc.X);
            var angJuncBot = -Math.PI / 2.0;
            var angJoinBot = Math.Atan2(joinBot.Y - ckBot.Y, joinBot.X - ckBot.X);

            if (isLeft)
            {
                // Left head: bottom knuckle → crown → top knuckle (minor arcs only).
                curves.Add(CreateMinorArc(ckBot, rk, angJoinBot, angJuncBot));
                curves.Add(CreateMinorArc(cc, rc, angCrownBot, angCrownTop));
                curves.Add(CreateMinorArc(ckTop, rk, angJoinTop, angJuncTop));
            }
            else
            {
                // Right head: top knuckle → crown → bottom knuckle (minor arcs only).
                curves.Add(CreateMinorArc(ckTop, rk, angJoinTop, angJuncTop));
                curves.Add(CreateMinorArc(cc, rc, angCrownTop, angCrownBot));
                curves.Add(CreateMinorArc(ckBot, rk, angJuncBot, angJoinBot));
            }
        }

        /// <summary>
        /// Arc entity always sweeps CCW; pick endpoint order so sweep is the minor arc (&lt;= pi).
        /// Torispherical head knuckles/crown must use the short dish/fillet, not the long way around.
        /// </summary>
        private static Arc CreateMinorArc(Point3d center, double radius, double angA, double angB)
        {
            var a = NormalizeStart(angA);
            var b = NormalizeStart(angB);
            var ccw = b - a;
            if (ccw < 0.0)
            {
                ccw += 2.0 * Math.PI;
            }

            double start;
            double end;
            if (ccw <= Math.PI)
            {
                start = a;
                end = a + ccw;
            }
            else
            {
                // Other direction is the minor arc.
                start = b;
                var ccwOther = a - b;
                if (ccwOther < 0.0)
                {
                    ccwOther += 2.0 * Math.PI;
                }

                end = b + ccwOther;
            }

            return new Arc(center, radius, start, end);
        }

        private static List<Curve> OffsetCurvesOutward(
            IList<Curve> sources,
            double distance,
            double originY)
        {
            var result = new List<Curve>();
            if (sources == null || distance == 0.0)
            {
                return result;
            }

            foreach (var src in sources)
            {
                if (src == null)
                {
                    continue;
                }

                var chosen = ChooseOutwardOffset(src, distance, originY);
                if (chosen != null)
                {
                    result.Add(chosen);
                }
            }

            return result;
        }

        /// <summary>
        /// Tries both offset signs; keeps the side farther from the view centerline (outward).
        /// </summary>
        private static Curve ChooseOutwardOffset(Curve src, double distance, double originY)
        {
            Curve best = null;
            var bestScore = double.NegativeInfinity;

            foreach (var sign in new[] { 1.0, -1.0 })
            {
                DBObjectCollection col = null;
                try
                {
                    col = src.GetOffsetCurves(sign * distance);
                }
                catch
                {
                    continue;
                }

                if (col == null || col.Count == 0)
                {
                    if (col != null)
                    {
                        col.Dispose();
                    }

                    continue;
                }

                for (var i = 0; i < col.Count; i++)
                {
                    var curve = col[i] as Curve;
                    if (curve == null)
                    {
                        continue;
                    }

                    var score = OutwardScore(curve, originY);
                    if (score > bestScore)
                    {
                        if (best != null)
                        {
                            best.Dispose();
                        }

                        // Clone before collection Dispose() releases members.
                        best = (Curve)curve.Clone();
                        bestScore = score;
                    }
                }

                col.Dispose();
            }

            return best;
        }

        private static double OutwardScore(Curve curve, double originY)
        {
            try
            {
                var mid = curve.GetPointAtParameter(
                    0.5 * (curve.StartParam + curve.EndParam));
                return Math.Abs(mid.Y - originY);
            }
            catch
            {
                return 0.0;
            }
        }

        private static void AppendCurves(
            BlockTableRecord ms,
            Transaction tr,
            IList<Curve> curves,
            string layer,
            Color color)
        {
            if (curves == null)
            {
                return;
            }

            foreach (var curve in curves)
            {
                if (curve == null)
                {
                    continue;
                }

                curve.SetDatabaseDefaults();
                curve.Layer = layer;
                curve.Color = color;
                ms.AppendEntity(curve);
                tr.AddNewlyCreatedDBObject(curve, true);
            }
        }

        private static Point3d PointOnRay(Point3d origin, double dx, double dy, double scale)
        {
            return new Point3d(origin.X + dx * scale, origin.Y + dy * scale, 0);
        }

        private static double NormalizeStart(double ang)
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
            string layer,
            double textHeight)
        {
            var dim = new AlignedDimension(p1, p2, dimLine, null, ObjectId.Null);
            dim.SetDatabaseDefaults();
            dim.Layer = layer;
            dim.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
            if (textHeight > 0.0)
            {
                dim.Dimtxt = textHeight;
                dim.Dimasz = textHeight * 0.4;
            }

            // Align text with the measurement direction (not forced horizontal).
            dim.Dimtih = false;
            dim.Dimtoh = false;

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
            DrawingStyle.Ensure(db, tr);
            EnsureLayer(db, tr, LayerOutline, 4);
            EnsureLayer(db, tr, LayerSection, 4);
            EnsureLayer(db, tr, LayerCenter, 1);
            EnsureLayer(db, tr, LayerDim, 1);
            EnsureLayer(db, tr, LayerText, 2);
            EnsureLayer(db, tr, LayerSupport, 1);
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
