using System;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Engine
{
    /// <summary>
    /// Vẽ lỗ treo đúng theo 6 bước dựng tay (không dim).
    /// </summary>
    public static class LiftingEyeDrawer
    {
        private const string LayerOutline = "LIFT_EYE_OUTLINE";

        public static void Draw(Database db, Transaction tr, LiftingEyeParams p, Point3d origin)
        {
            if (db == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            var err = p.Validate();
            if (err != null)
            {
                throw new InvalidOperationException(err);
            }

            EnsureLayer(db, tr, LayerOutline, 4);
            DrawingStyle.Ensure(db, tr);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var color = Color.FromColorIndex(ColorMethod.ByAci, 4);

            DrawProfile(ms, tr, p, origin, color);

            // ==================================================================
            // Hình chiếu đứng: hình chữ nhật dài 160, rộng 19.
            // Đặt bên dưới mặt cắt A-A để 2 hình không chồng lên nhau.
            // ==================================================================
            var elevationGap = Math.Max(30.0, p.ElevationHeight * 2.0);
            var elevationOrigin = new Point3d(
                origin.X,
                origin.Y - elevationGap - p.ElevationHeight,
                0);
            DrawElevationRectangle(ms, tr, elevationOrigin, p.BaseWidth, p.ElevationHeight, color);
        }

        /// <summary>
        /// Biên dạng bản mã (6 bước dựng tay), gốc = góc trái đáy.
        /// </summary>
        public static void DrawProfile(
            BlockTableRecord ms,
            Transaction tr,
            LiftingEyeParams p,
            Point3d origin,
            Color color)
        {
            if (ms == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            EnsureLayer(ms.Database, tr, LayerOutline, 4);

            var ox = origin.X;
            var oy = origin.Y;

            // ==================================================================
            // Bước 1: Vẽ H trái = 60 (đoạn thẳng đứng từ góc trái đáy lên).
            // ==================================================================
            var pBl = new Point3d(ox, oy, 0);
            var pLeftTop = new Point3d(ox, oy + p.LeftHeight, 0);
            AppendLine(ms, tr, pBl, pLeftTop, color);

            // ==================================================================
            // Bước 2: Vẽ đáy = 160 (nằm ngang từ góc trái sang phải).
            // ==================================================================
            var pBr = new Point3d(ox + p.BaseWidth, oy, 0);
            AppendLine(ms, tr, pBl, pBr, color);

            // ==================================================================
            // Bước 3: Vẽ H phải = 100 (đứng từ góc phải đáy lên).
            // (Thực tế chỉ giữ đoạn đến tiếp điểm fillet — xem bước 5.)
            // ==================================================================
            // Điểm đỉnh tham chiếu cạnh phải (trước khi trim fillet):
            var pRightTopRef = new Point3d(ox + p.BaseWidth, oy + p.RightHeight, 0);

            // ==================================================================
            // Bước 4: Vẽ cạnh chéo trái dài 100, nghiêng 47° (từ đỉnh H trái).
            // (Thực tế chỉ giữ đoạn đến tiếp điểm fillet — xem bước 5.)
            // ==================================================================
            var angDiag = p.DiagonalAngleDeg * Math.PI / 180.0;
            var pDiagEndRef = new Point3d(
                pLeftTop.X + p.DiagonalLength * Math.Cos(angDiag),
                pLeftTop.Y + p.DiagonalLength * Math.Sin(angDiag),
                0);

            // ==================================================================
            // Bước 5: Fillet R = 60 giữa cạnh chéo trái và H phải.
            // - Tâm fillet cách cạnh phải đúng R về phía trong: Cx = BaseWidth - R
            // - Cy sao cho khoảng cách tới đường chéo = R (phía trong thân)
            // - Trim cạnh chéo / cạnh phải về tiếp điểm, rồi vẽ cung fillet
            // ==================================================================
            Point3d tanDiag;
            Point3d tanRight;
            Point3d cFillet;
            ComputeFillet(
                ox, oy, p,
                out cFillet, out tanDiag, out tanRight);

            // Cạnh phải đã trim: từ đáy → tiếp điểm fillet trên H phải
            AppendLine(ms, tr, pBr, tanRight, color);

            // Cạnh chéo đã trim: từ đỉnh H trái → tiếp điểm fillet trên chéo
            AppendLine(ms, tr, pLeftTop, tanDiag, color);

            // Cung fillet: từ tiếp điểm cạnh phải (góc 0°) CCW tới tiếp điểm cạnh chéo
            var aRight = 0.0;
            var aDiag = Math.Atan2(tanDiag.Y - cFillet.Y, tanDiag.X - cFillet.X);
            AppendArcCcw(ms, tr, cFillet, p.FilletRadius, aRight, aDiag, color);

            // (pRightTopRef / pDiagEndRef chỉ là mốc bước 3–4; không vẽ phần thừa sau trim)
            _ = pRightTopRef;
            _ = pDiagEndRef;

            // ==================================================================
            // Bước 6: Dựng lỗ treo
            // 6.1 Từ trung điểm đáy → vuông góc lên cao 68.5086 = tâm tròn 1
            // 6.2 Từ tâm 1 → nghiêng 45°, cách 30 = tâm tròn 2
            // 6.3 Vẽ 2 đường tròn R=30, nối 2 tiếp tuyến ngoài, bỏ phần cung thừa
            //     (= vẽ capsule: 2 nửa vòng + 2 tiếp tuyến)
            // ==================================================================
            var midBase = new Point3d(ox + p.BaseWidth * 0.5, oy, 0);
            var c1 = new Point3d(midBase.X, midBase.Y + p.HoleCenterHeight, 0);

            var angHole = p.HoleAngleDeg * Math.PI / 180.0;
            var c2 = new Point3d(
                c1.X + p.HoleCenterSpan * Math.Cos(angHole),
                c1.Y + p.HoleCenterSpan * Math.Sin(angHole),
                0);

            DrawSlotCapsule(ms, tr, c1, c2, p.HoleRadius, color);
        }

        /// <summary>
        /// Section A-A — left lug. <paramref name="outerLeftX"/> = outer-face X of left edge.
        /// </summary>
        public static void DrawProfileThroughShell(
            BlockTableRecord ms,
            Transaction tr,
            LiftingEyeParams p,
            Point3d origin,
            double hiddenDepth,
            Color color)
        {
            DrawProfileThroughShellInternal(
                ms, tr, p, origin.Y, hiddenDepth, color,
                (lx, ly, baseY, w) => new Point3d(origin.X + lx, baseY + ly, 0));
        }

        /// <summary>
        /// Section A-A — right lug, mirrored. <paramref name="outerRightX"/> = outer-face X of right edge.
        /// </summary>
        public static void DrawProfileThroughShellRight(
            BlockTableRecord ms,
            Transaction tr,
            LiftingEyeParams p,
            double outerRightX,
            double outerY,
            double hiddenDepth,
            Color color)
        {
            DrawProfileThroughShellInternal(
                ms, tr, p, outerY, hiddenDepth, color,
                (lx, ly, baseY, w) => new Point3d(outerRightX - lx, baseY + ly, 0));
        }

        private static void DrawProfileThroughShellInternal(
            BlockTableRecord ms,
            Transaction tr,
            LiftingEyeParams p,
            double outerY,
            double hiddenDepth,
            Color color,
            Func<double, double, double, double, Point3d> toWorld)
        {
            if (ms == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            if (hiddenDepth <= 0.0)
            {
                throw new InvalidOperationException("SectionHiddenDepth required for shell embed.");
            }

            if (p.LeftHeight <= hiddenDepth || p.RightHeight <= hiddenDepth)
            {
                throw new InvalidOperationException(
                    "H trái / H phải phải lớn hơn chiều cao phần khuất mặt cắt.");
            }

            EnsureLayer(ms.Database, tr, LayerOutline, 4);
            DrawingStyle.Ensure(ms.Database, tr);

            var w = p.BaseWidth;
            var baseY = outerY - hiddenDepth;

            Point3d W(double lx, double ly)
            {
                return toWorld(lx, ly, baseY, w);
            }

            var pBl = W(0.0, 0.0);
            var pBr = W(w, 0.0);
            var pLeftTop = W(0.0, p.LeftHeight);
            var pLeftAtShell = W(0.0, hiddenDepth);
            var pRightAtShell = W(w, hiddenDepth);

            Point3d tanDiag;
            Point3d tanRight;
            Point3d cFillet;
            ComputeFillet(0.0, 0.0, p, out cFillet, out tanDiag, out tanRight);

            var cFilletW = W(cFillet.X, cFillet.Y);
            var tanDiagW = W(tanDiag.X, tanDiag.Y);
            var tanRightW = W(tanRight.X, tanRight.Y);

            // Embedded portion → TANK_HIDDEN (ByLayer HIDDEN).
            CadDraw.AppendLine(ms, tr, pBl, pLeftAtShell, LineRole.Hidden, color);
            CadDraw.AppendLine(ms, tr, pBl, pBr, LineRole.Hidden, color);
            CadDraw.AppendLine(ms, tr, pBr, pRightAtShell, LineRole.Hidden, color);

            // Visible above outer shell.
            AppendLine(ms, tr, pLeftAtShell, pLeftTop, color);
            AppendLine(ms, tr, pRightAtShell, tanRightW, color);
            AppendLine(ms, tr, pLeftTop, tanDiagW, color);

            AppendArcCcwThrough(ms, tr, cFilletW, p.FilletRadius, tanRightW, tanDiagW, color);

            var angHole = p.HoleAngleDeg * Math.PI / 180.0;
            var c1W = W(w * 0.5, p.HoleCenterHeight);
            var c2W = W(
                w * 0.5 + p.HoleCenterSpan * Math.Cos(angHole),
                p.HoleCenterHeight + p.HoleCenterSpan * Math.Sin(angHole));
            DrawSlotCapsule(ms, tr, c1W, c2W, p.HoleRadius, color);
        }

        /// <summary>CCW short arc on the fillet from <paramref name="from"/> to <paramref name="to"/>.</summary>
        private static void AppendArcCcwThrough(
            BlockTableRecord ms,
            Transaction tr,
            Point3d center,
            double radius,
            Point3d from,
            Point3d to,
            Color color)
        {
            var aStart = Math.Atan2(from.Y - center.Y, from.X - center.X);
            var aEnd = Math.Atan2(to.Y - center.Y, to.X - center.X);

            var sweep = aEnd - aStart;
            while (sweep <= 1e-10)
            {
                sweep += 2.0 * Math.PI;
            }

            while (sweep > 2.0 * Math.PI - 1e-10)
            {
                sweep -= 2.0 * Math.PI;
            }

            if (sweep > Math.PI)
            {
                var tmp = aStart;
                aStart = aEnd;
                aEnd = tmp;
            }

            AppendArcCcw(ms, tr, center, radius, aStart, aEnd, color);
        }

        /// <summary>
        /// Tính tâm fillet và 2 tiếp điểm (cạnh chéo + cạnh phải).
        /// </summary>
        private static void ComputeFillet(
            double ox,
            double oy,
            LiftingEyeParams p,
            out Point3d cFillet,
            out Point3d tanDiag,
            out Point3d tanRight)
        {
            var r = p.FilletRadius;

            // Tâm cách cạnh phải (x = BaseWidth) đúng R về trái
            var cx = ox + p.BaseWidth - r;
            var cxLocal = p.BaseWidth - r;

            // Đường chéo local: y = LeftHeight + tan(α)*x
            var m = Math.Tan(p.DiagonalAngleDeg * Math.PI / 180.0);
            var denom = Math.Sqrt(m * m + 1.0);

            // Chọn nhánh tâm nằm dưới đường chéo (phía trong chi tiết)
            var cyLocal = p.LeftHeight + m * cxLocal - r * denom;
            var cy = oy + cyLocal;
            cFillet = new Point3d(cx, cy, 0);

            // Tiếp điểm trên cạnh phải
            tanRight = new Point3d(ox + p.BaseWidth, cy, 0);

            // Tiếp điểm trên cạnh chéo (vuông góc từ tâm xuống đường)
            var nx = m / denom;
            var ny = -1.0 / denom;
            var signed = (m * cxLocal - cyLocal + p.LeftHeight) / denom;
            var sign = signed >= 0.0 ? 1.0 : -1.0;
            tanDiag = new Point3d(
                cFillet.X - sign * r * nx,
                cFillet.Y - sign * r * ny,
                0);
        }

        /// <summary>
        /// Vẽ lỗ capsule: 2 nửa vòng R + 2 tiếp tuyến ngoài (đã trim phần thừa).
        /// </summary>
        private static void DrawSlotCapsule(
            BlockTableRecord ms,
            Transaction tr,
            Point3d c1,
            Point3d c2,
            double radius,
            Color color)
        {
            var dx = c2.X - c1.X;
            var dy = c2.Y - c1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9)
            {
                AppendArcCcw(ms, tr, c1, radius, 0.0, Math.PI * 2.0 - 1e-6, color);
                return;
            }

            var theta = Math.Atan2(dy, dx);
            var aPerp1 = theta + Math.PI * 0.5;
            var aPerp2 = theta - Math.PI * 0.5;

            var o1 = new Point3d(c1.X + radius * Math.Cos(aPerp1), c1.Y + radius * Math.Sin(aPerp1), 0);
            var o2 = new Point3d(c2.X + radius * Math.Cos(aPerp1), c2.Y + radius * Math.Sin(aPerp1), 0);
            var o3 = new Point3d(c1.X + radius * Math.Cos(aPerp2), c1.Y + radius * Math.Sin(aPerp2), 0);
            var o4 = new Point3d(c2.X + radius * Math.Cos(aPerp2), c2.Y + radius * Math.Sin(aPerp2), 0);

            AppendLine(ms, tr, o1, o2, color);
            AppendLine(ms, tr, o3, o4, color);

            // Nửa vòng phía ngoài mỗi tâm
            AppendArcCcw(ms, tr, c1, radius, aPerp1, aPerp2, color);
            AppendArcCcw(ms, tr, c2, radius, aPerp2, aPerp1, color);
        }

        /// <summary>
        /// Elevation symbol (rectangle) centered on <paramref name="center"/>.
        /// </summary>
        public static void DrawElevationSymbol(
            BlockTableRecord ms,
            Transaction tr,
            Point3d center,
            double width,
            double height,
            Color color)
        {
            if (ms == null || tr == null)
            {
                throw new ArgumentNullException();
            }

            EnsureLayer(ms.Database, tr, LayerOutline, 4);

            var origin = new Point3d(center.X - width * 0.5, center.Y - height * 0.5, 0);
            DrawElevationRectangle(ms, tr, origin, width, height, color);
        }

        /// <summary>
        /// Vẽ hình chiếu đứng dạng chữ nhật dài BaseWidth, rộng ElevationHeight.
        /// </summary>
        private static void DrawElevationRectangle(
            BlockTableRecord ms,
            Transaction tr,
            Point3d origin,
            double width,
            double height,
            Color color)
        {
            var p1 = origin;
            var p2 = new Point3d(origin.X + width, origin.Y, 0);
            var p3 = new Point3d(origin.X + width, origin.Y + height, 0);
            var p4 = new Point3d(origin.X, origin.Y + height, 0);

            AppendLine(ms, tr, p1, p2, color);
            AppendLine(ms, tr, p2, p3, color);
            AppendLine(ms, tr, p3, p4, color);
            AppendLine(ms, tr, p4, p1, color);
        }

        private static void AppendArcCcw(
            BlockTableRecord ms,
            Transaction tr,
            Point3d center,
            double radius,
            double startAng,
            double endAng,
            Color color)
        {
            startAng = NormalizeAngle(startAng);
            endAng = NormalizeAngle(endAng);
            if (Math.Abs(endAng - startAng) < 1e-12)
            {
                return;
            }

            var arc = new Arc(center, radius, startAng, endAng);
            arc.SetDatabaseDefaults();
            arc.Layer = LayerOutline;
            arc.Color = color;
            ms.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
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

        private static void AppendLine(
            BlockTableRecord ms,
            Transaction tr,
            Point3d a,
            Point3d b,
            Color color)
        {
            var line = new Line(a, b);
            line.SetDatabaseDefaults();
            line.Layer = LayerOutline;
            line.Color = color;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
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
