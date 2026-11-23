using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Engine
{
    /// <summary>
    /// Insert template DWG chỏm (PART_CHOM) vào ModelSpace với scale theo đường kính bồn.
    /// </summary>
    public static class PartChomInserter
    {
        /// <summary>
        /// Tên file template DWG — nằm cùng thư mục với DLL (đường dẫn tương đối).
        /// </summary>
        public const string TemplateFileName = "PART_CHOM.dwg";

        /// <summary>Tiền tố tên block định nghĩa sau khi import template.</summary>
        public const string BlockName = "PART_CHOM";

        /// <summary>
        /// Ghép đường dẫn tuyệt đối tới file template:
        /// thư mục chứa DLL + tên file tương đối <see cref="TemplateFileName"/>.
        /// </summary>
        public static string ResolveTemplatePath()
        {
            // Lấy thư mục chứa assembly đang chạy (EnjiCadCheck.dll).
            var asm = Assembly.GetExecutingAssembly();
            var dllPath = asm.Location;
            var dllDir = Path.GetDirectoryName(dllPath);
            if (string.IsNullOrEmpty(dllDir))
            {
                throw new InvalidOperationException(
                    "Không xác định được thư mục DLL để tìm template PART_CHOM.dwg.");
            }

            // Đường dẫn tương đối: cùng thư mục với DLL.
            return Path.Combine(dllDir, TemplateFileName);
        }

        /// <summary>
        /// Scale insert = đường kính bồn (mm) / 1000.
        /// Template được vẽ ở hệ quy chiếu đường kính = 1000 mm.
        /// </summary>
        public static double ComputeScaleFromDiameter(double diameterMm)
        {
            if (diameterMm <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    "diameterMm",
                    "Đường kính phải lớn hơn 0 để tính scale.");
            }

            return diameterMm / 1000.0;
        }

        /// <summary>
        /// Đọc template DWG → import block → scale → thêm SF → offset dày ra ngoài
        /// (chỏm + SF) → insert BlockReference tại đầu SF (scale 1 vì đã scale sẵn).
        /// </summary>
        /// <param name="db">Database bản vẽ đang mở.</param>
        /// <param name="tr">Transaction đang mở (ForWrite ModelSpace).</param>
        /// <param name="sfTipPoint">Điểm neo đầu SF (khớp đầu thân); block đặt lệch theo rotation.</param>
        /// <param name="scale">Hệ số scale đều (Diameter/1000), áp vào geometry trước khi sửa.</param>
        /// <param name="parameters">Chiều dày chỏm + chiều cao SF dùng khi sửa geometry.</param>
        /// <param name="rotationRadians">Góc xoay quanh Z; 0 = đầu trái, π = đầu phải (đảo chiều).</param>
        /// <returns>ObjectId của BlockReference vừa tạo.</returns>
        public static ObjectId InsertTemplate(
            Database db,
            Transaction tr,
            Point3d sfTipPoint,
            double scale,
            PartChomParams parameters,
            double rotationRadians = 0.0)
        {
            var blockDefId = PrepareBlockDefinition(db, tr, scale, parameters);
            var ms = GetModelSpaceForWrite(db, tr);
            return AppendBlockReferenceAtSfTip(
                ms, tr, blockDefId, sfTipPoint, rotationRadians, parameters.SfHeight);
        }

        /// <summary>
        /// Insert 4 chỏm theo layout TAOTHANTANK: trái/phải × hình chiếu đứng/bằng.
        /// Đầu trái rotation = 0; đầu phải rotation = π (SF vẫn khớp mép thân).
        /// Dùng chung một định nghĩa block (cùng scale / Thickness / SF).
        /// </summary>
        public static IList<ObjectId> InsertAtShellLayout(
            Database db,
            Transaction tr,
            TankShellLayout layout,
            double scale,
            PartChomParams parameters)
        {
            if (layout == null)
            {
                throw new ArgumentNullException("layout");
            }

            if (layout.Elevation == null || layout.Plan == null)
            {
                throw new InvalidOperationException(
                    "Layout thiếu hình chiếu đứng hoặc bằng — chạy lại TAOTHANTANK.");
            }

            var blockDefId = PrepareBlockDefinition(db, tr, scale, parameters);
            var ms = GetModelSpaceForWrite(db, tr);
            var ids = new List<ObjectId>(4);

            // Mỗi view: trái LeftInnerBottom (0°); phải RightInnerTop (180°).
            AppendViewEnds(ms, tr, blockDefId, layout.Elevation, parameters.SfHeight, ids);
            AppendViewEnds(ms, tr, blockDefId, layout.Plan, parameters.SfHeight, ids);

            return ids;
        }

        /// <summary>
        /// Import + scale + EditBlockDefinition — dùng chung cho insert 1 điểm hoặc cả layout.
        /// </summary>
        private static ObjectId PrepareBlockDefinition(
            Database db,
            Transaction tr,
            double scale,
            PartChomParams parameters)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            if (tr == null)
            {
                throw new ArgumentNullException("tr");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            if (scale <= 0.0)
            {
                throw new ArgumentOutOfRangeException("scale", "Scale phải lớn hơn 0.");
            }

            var templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy template DWG chỏm. Hãy đặt file cạnh DLL.",
                    templatePath);
            }

            // Định nghĩa mới mỗi lần gọi — tránh Thickness/SF lần trước làm bẩn lần sau.
            var blockDefId = CreateBlockDefinitionFromTemplate(db, tr, templatePath);
            ScaleBlockDefinition(tr, blockDefId, scale);
            EditBlockDefinition(tr, blockDefId, parameters);
            return blockDefId;
        }

        private static BlockTableRecord GetModelSpaceForWrite(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(
                bt[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);
        }

        /// <summary>
        /// Insert trái (0°) + phải (π) cho một hình chiếu.
        /// Trái: LeftInnerBottom. Phải (xoay 180°): RightInnerTop.
        /// </summary>
        private static void AppendViewEnds(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId blockDefId,
            TankShellViewGeometry view,
            double sfHeight,
            IList<ObjectId> ids)
        {
            ids.Add(AppendBlockReferenceAtSfTip(
                ms, tr, blockDefId, view.LeftInnerBottom, 0.0, sfHeight));
            ids.Add(AppendBlockReferenceAtSfTip(
                ms, tr, blockDefId, view.RightInnerTop, Math.PI, sfHeight));
        }

        /// <summary>
        /// Đặt BlockReference sao cho đầu SF (local +X = SfHeight) khớp <paramref name="sfTipWorld"/>.
        /// rotation = 0 → chỏm bên trái thân; π → chỏm bên phải thân.
        /// </summary>
        private static ObjectId AppendBlockReferenceAtSfTip(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId blockDefId,
            Point3d sfTipWorld,
            double rotationRadians,
            double sfHeight)
        {
            // Trong block: đầu SF phải = origin + (SfHeight, 0). Xoay rồi trừ để khớp neo world.
            var localSfTip = new Vector3d(sfHeight, 0.0, 0.0);
            var worldOffset = localSfTip.RotateBy(rotationRadians, Vector3d.ZAxis);
            var placedPoint = sfTipWorld - worldOffset;

            var br = new BlockReference(placedPoint, blockDefId);
            br.Rotation = rotationRadians;
            br.ScaleFactors = new Scale3d(1.0, 1.0, 1.0);

            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            return br.ObjectId;
        }

        /// <summary>
        /// Nhân mọi entity trong định nghĩa block với <paramref name="scale"/> quanh (0,0,0).
        /// </summary>
        private static void ScaleBlockDefinition(
            Transaction tr,
            ObjectId blockDefId,
            double scale)
        {
            var btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForWrite);
            // Scale đều quanh gốc block — khớp hệ template D=1000.
            var xform = Matrix3d.Scaling(scale, Point3d.Origin);

            foreach (ObjectId entId in btr)
            {
                var ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                if (ent == null)
                {
                    continue;
                }

                ent.TransformBy(xform);
            }
        }

        /// <summary>
        /// Import template thành một BlockTableRecord mới (tên duy nhất mỗi lần gọi).
        /// </summary>
        private static ObjectId CreateBlockDefinitionFromTemplate(
            Database destDb,
            Transaction tr,
            string templatePath)
        {
            // Tên duy nhất: tránh Has(BlockName) trả về bản đã sửa từ lần trước.
            var uniqueName = string.Format("{0}_{1}", BlockName, DateTime.UtcNow.Ticks);

            // Database tạm chỉ để đọc DWG nguồn (không gắn document).
            using (var sourceDb = new Database(false, true))
            {
                // Mở template chỉ đọc, cho phép share để file không bị khóa cứng.
                sourceDb.ReadDwgFile(
                    templatePath,
                    FileOpenMode.OpenForReadAndAllShare,
                    true,
                    string.Empty);

                // Import toàn bộ nội dung ModelSpace nguồn thành 1 block.
                // preserveSourceDatabase = true: giữ sourceDb sống đến hết using.
                var blockId = destDb.Insert(uniqueName, sourceDb, true);

                // Insert có thể không gắn transaction đang mở — đảm bảo object có trong tr.
                tr.GetObject(blockId, OpenMode.ForRead);
                return blockId;
            }
        }

        /// <summary>
        /// Đọc Arc trong định nghĩa block SAU khi đã scale, thêm Line SF,
        /// rồi offset ra ngoài đúng Thickness (cả chỏm + SF).
        /// </summary>
        private static void EditBlockDefinition(
            Transaction tr,
            ObjectId blockDefId,
            PartChomParams parameters)
        {
            // Mở ForWrite để append Line SF / curve offset vào định nghĩa block.
            var btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForWrite);

            // Flatten BlockReference lồng → Arc chỏm nằm trực tiếp trong block ngoài.
            ExplodeNestedBlockReferences(tr, btr);

            // Gom endpoint Arc ở không gian block ngoài (sau scale Diameter/1000).
            var anchors = new List<Point3d>();
            CollectArcEndpoints(tr, btr, Matrix3d.Identity, anchors);

            // Thêm 2 Line SF (mặt trong) neo từ đầu Arc.
            ApplyLineEdits(tr, btr, anchors, parameters);

            // Bước 6: gom Line/Arc thành chuỗi → Polyline → offset 1 lần (tránh hở khớp).
            // Giữ bản gốc (trong); append bản offset (ngoài).
            AppendOutwardThicknessOffsets(tr, btr, parameters.Thickness);
        }

        /// <summary>
        /// Explode mọi BlockReference cấp 1 trong định nghĩa để geometry chỏm
        /// thành entity trực tiếp (phục vụ neo SF + offset dày).
        /// </summary>
        private static void ExplodeNestedBlockReferences(
            Transaction tr,
            BlockTableRecord btr)
        {
            var brIds = new List<ObjectId>();
            foreach (ObjectId entId in btr)
            {
                if (tr.GetObject(entId, OpenMode.ForRead) is BlockReference)
                {
                    brIds.Add(entId);
                }
            }

            foreach (var brId in brIds)
            {
                var br = (BlockReference)tr.GetObject(brId, OpenMode.ForWrite);
                var pieces = new DBObjectCollection();
                br.Explode(pieces);

                foreach (DBObject obj in pieces)
                {
                    var ent = obj as Entity;
                    if (ent == null)
                    {
                        obj.Dispose();
                        continue;
                    }

                    btr.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                }

                br.Erase();
            }
        }

        /// <summary>
        /// Join Line/Arc into open chains → one Polyline each → GetOffsetCurves once
        /// per chain (outward from Y=0). Keeps originals; appends outer copies.
        /// </summary>
        private static void AppendOutwardThicknessOffsets(
            Transaction tr,
            BlockTableRecord btr,
            double thickness)
        {
            if (thickness <= 0.0)
            {
                return;
            }

            var segments = CollectLineArcSegments(tr, btr);
            if (segments.Count == 0)
            {
                return;
            }

            // Endpoint join tolerance after Diameter/1000 scale (real mm).
            var joinTol = Math.Max(0.5, thickness * 0.05);
            var chains = BuildConnectedChains(segments, joinTol);

            // Block template centerline is Y = 0.
            const double originY = 0.0;

            foreach (var chain in chains)
            {
                if (chain == null || chain.Count == 0)
                {
                    continue;
                }

                Polyline pline = null;
                try
                {
                    pline = BuildPolylineFromChain(chain);
                    var outer = ChooseOutwardOffset(pline, thickness, originY);
                    if (outer == null)
                    {
                        continue;
                    }

                    outer.SetDatabaseDefaults();
                    btr.AppendEntity(outer);
                    tr.AddNewlyCreatedDBObject(outer, true);
                }
                finally
                {
                    // Temporary join polyline is not DB-resident — dispose after offset.
                    if (pline != null && pline.Database == null)
                    {
                        pline.Dispose();
                    }
                }
            }
        }

        /// <summary>Collect Line/Arc in the block definition as directed segments.</summary>
        private static List<ChainSegment> CollectLineArcSegments(
            Transaction tr,
            BlockTableRecord btr)
        {
            var list = new List<ChainSegment>();
            foreach (ObjectId entId in btr)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead);

                var line = ent as Line;
                if (line != null)
                {
                    list.Add(new ChainSegment(line.StartPoint, line.EndPoint, 0.0));
                    continue;
                }

                var arc = ent as Arc;
                if (arc != null)
                {
                    // Arc is CCW Start→End; positive bulge = CCW on lightweight Polyline.
                    var bulge = Math.Tan(arc.TotalAngle / 4.0);
                    list.Add(new ChainSegment(arc.StartPoint, arc.EndPoint, bulge));
                }
            }

            return list;
        }

        /// <summary>
        /// Greedy endpoint chaining: grow open paths by attaching unused segments
        /// (forward or reversed) until no neighbor within <paramref name="joinTol"/>.
        /// </summary>
        private static List<List<ChainSegment>> BuildConnectedChains(
            List<ChainSegment> segments,
            double joinTol)
        {
            var chains = new List<List<ChainSegment>>();
            var unused = new List<ChainSegment>(segments);
            var tolSq = joinTol * joinTol;

            while (unused.Count > 0)
            {
                var chain = new List<ChainSegment>();
                chain.Add(unused[0]);
                unused.RemoveAt(0);

                var grew = true;
                while (grew)
                {
                    grew = false;
                    var head = chain[0].Start;
                    var tail = chain[chain.Count - 1].End;

                    for (var i = 0; i < unused.Count; i++)
                    {
                        var seg = unused[i];
                        if (DistanceSquared(tail, seg.Start) <= tolSq)
                        {
                            chain.Add(seg);
                            unused.RemoveAt(i);
                            grew = true;
                            break;
                        }

                        if (DistanceSquared(tail, seg.End) <= tolSq)
                        {
                            chain.Add(seg.Reversed());
                            unused.RemoveAt(i);
                            grew = true;
                            break;
                        }

                        if (DistanceSquared(head, seg.End) <= tolSq)
                        {
                            chain.Insert(0, seg);
                            unused.RemoveAt(i);
                            grew = true;
                            break;
                        }

                        if (DistanceSquared(head, seg.Start) <= tolSq)
                        {
                            chain.Insert(0, seg.Reversed());
                            unused.RemoveAt(i);
                            grew = true;
                            break;
                        }
                    }
                }

                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>
        /// Build open lightweight Polyline: vertex i holds bulge for segment i→i+1.
        /// </summary>
        private static Polyline BuildPolylineFromChain(IList<ChainSegment> chain)
        {
            var pline = new Polyline();
            for (var i = 0; i < chain.Count; i++)
            {
                var seg = chain[i];
                pline.AddVertexAt(
                    i,
                    new Point2d(seg.Start.X, seg.Start.Y),
                    seg.Bulge,
                    0.0,
                    0.0);
            }

            var last = chain[chain.Count - 1];
            pline.AddVertexAt(
                chain.Count,
                new Point2d(last.End.X, last.End.Y),
                0.0,
                0.0,
                0.0);
            pline.Closed = false;
            return pline;
        }

        private static double DistanceSquared(Point3d a, Point3d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// One directed segment for chaining (Line bulge=0, Arc bulge=tan(Δ/4)).
        /// </summary>
        private sealed class ChainSegment
        {
            public ChainSegment(Point3d start, Point3d end, double bulge)
            {
                Start = start;
                End = end;
                Bulge = bulge;
            }

            public Point3d Start { get; private set; }

            public Point3d End { get; private set; }

            public double Bulge { get; private set; }

            /// <summary>Reverse direction; negate bulge so arc side stays the same.</summary>
            public ChainSegment Reversed()
            {
                return new ChainSegment(End, Start, -Bulge);
            }
        }

        /// <summary>
        /// Thử offset ±distance; chọn phía xa đường tâm hơn (ra ngoài).
        /// </summary>
        private static Curve ChooseOutwardOffset(
            Curve src,
            double distance,
            double originY)
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
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    if (best != null)
                    {
                        best.Dispose();
                    }

                    // Clone trước khi Dispose collection (tránh mất object).
                    best = (Curve)curve.Clone();
                    bestScore = score;
                }

                col.Dispose();
            }

            return best;
        }

        /// <summary>Điểm giữa curve càng xa originY → càng “ra ngoài”.</summary>
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

        /// <summary>
        /// Duyệt định nghĩa block; đưa Start/End của Arc về không gian PART_CHOM ngoài.
        /// BlockReference lồng: nhân BlockTransform để endpoint vẫn là mm thật.
        /// </summary>
        private static void CollectArcEndpoints(
            Transaction tr,
            BlockTableRecord btr,
            Matrix3d toOuter,
            IList<Point3d> anchors)
        {
            foreach (ObjectId entId in btr)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead);

                var arc = ent as Arc;
                if (arc != null)
                {
                    // Đưa 2 đầu Arc local về không gian định nghĩa block ngoài cùng.
                    anchors.Add(arc.StartPoint.TransformBy(toOuter));
                    anchors.Add(arc.EndPoint.TransformBy(toOuter));
                    continue;
                }

                var br = ent as BlockReference;
                if (br == null)
                {
                    continue;
                }

                // PART_CHOM.dwg thường để Arc chỏm trong một block insert lồng.
                var nested = (BlockTableRecord)tr.GetObject(
                    br.BlockTableRecord,
                    OpenMode.ForRead);
                // p' = toOuter * BlockTransform * p_local (thứ tự nhân ma trận AutoCAD).
                var nestedToOuter = toOuter * br.BlockTransform;
                CollectArcEndpoints(tr, nested, nestedToOuter, anchors);
            }
        }

        /// <summary>
        /// Quy tắc thêm Line trong không gian block ĐÃ scale (mm thật trên bản vẽ).
        /// SF: từ mỗi đầu Arc vẽ Line dọc +X dài SfHeight.
        /// </summary>
        private static void ApplyLineEdits(
            Transaction tr,
            BlockTableRecord btr,
            IList<Point3d> arcEndpoints,
            PartChomParams parameters)
        {
            if (parameters.Thickness <= 0.0 || parameters.SfHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    "parameters",
                    "Thickness và SfHeight phải lớn hơn 0.");
            }

            if (arcEndpoints == null || arcEndpoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "Template PART_CHOM không có Arc để neo 2 đầu SF.");
            }

            // SfHeight đã là mm thật (tọa độ đã scale theo Diameter/1000).
            var sfHeight = parameters.SfHeight;

            // Neo 2 đầu SF từ endpoint Arc (không gian block ngoài), vẽ stub theo +X.
            AppendSfLineAlongX(tr, btr, arcEndpoints[0], sfHeight);
            AppendSfLineAlongX(tr, btr, arcEndpoints[3], sfHeight);
        }

        /// <summary>
        /// Thêm Line SF nằm ngang: từ endpoint tới endpoint+(SfHeight, 0, 0).
        /// </summary>
        private static void AppendSfLineAlongX(
            Transaction tr,
            BlockTableRecord btr,
            Point3d endpoint,
            double sfHeight)
        {
            var other = new Point3d(endpoint.X + sfHeight, endpoint.Y, endpoint.Z);
            AppendLineToBlock(tr, btr, endpoint, other);
        }

        /// <summary>
        /// Tạo Line mới, gắn vào BlockTableRecord định nghĩa, đăng ký với transaction.
        /// </summary>
        private static void AppendLineToBlock(
            Transaction tr,
            BlockTableRecord btr,
            Point3d start,
            Point3d end)
        {
            var line = new Line(start, end);
            line.SetDatabaseDefaults();
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }
    }
}
