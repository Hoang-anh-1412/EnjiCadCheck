using System;
using System.Collections.Generic;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;

namespace EnjiCadCheck
{
    /// <summary>
    /// XData helpers for the 2-rectangle sample geometry test.
    /// Roles: TOP, MID, BOT, TL, TR, BL, BR, DIM_W, DIM_H1, DIM_H2
    /// </summary>
    public static class SampleXData
    {
        public const string AppName = "ENJI_SAMPLE";

        public static readonly string[] LineRoles =
        {
            "TOP", "MID", "BOT", "TL", "TR", "BL", "BR"
        };

        public static readonly string[] DimRoles =
        {
            "DIM_W", "DIM_H1", "DIM_H2"
        };

        public static void EnsureRegApp(Database db, Transaction tr)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(AppName))
            {
                return;
            }

            rat.UpgradeOpen();
            var record = new RegAppTableRecord { Name = AppName };
            rat.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        public static void SetRole(Entity ent, string role)
        {
            if (ent == null)
            {
                throw new ArgumentNullException(nameof(ent));
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                throw new ArgumentException("Role is empty.", nameof(role));
            }

            var normalized = role.Trim().ToUpperInvariant();
            using (var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, normalized)))
            {
                ent.XData = rb;
            }
        }

        public static string GetRole(Entity ent)
        {
            if (ent == null)
            {
                return null;
            }

            var rb = ent.GetXDataForApplication(AppName);
            if (rb == null)
            {
                return null;
            }

            using (rb)
            {
                foreach (TypedValue tv in rb)
                {
                    if (tv.TypeCode == (int)DxfCode.ExtendedDataAsciiString
                        || tv.TypeCode == (int)DxfCode.ExtendedDataControlString)
                    {
                        var text = tv.Value as string;
                        if (!string.IsNullOrWhiteSpace(text)
                            && !string.Equals(text, AppName, StringComparison.OrdinalIgnoreCase))
                        {
                            return text.Trim().ToUpperInvariant();
                        }
                    }
                }
            }

            return null;
        }

        public static void ClearRole(Entity ent)
        {
            if (ent == null)
            {
                return;
            }

            // Passing only the app name clears that app's XData.
            using (var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName)))
            {
                ent.XData = rb;
            }
        }

        public static Dictionary<string, List<ObjectId>> CollectRoles(Database db, Transaction tr)
        {
            var map = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (id.IsNull || id.IsErased)
                {
                    continue;
                }

                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    continue;
                }

                var role = GetRole(ent);
                if (string.IsNullOrEmpty(role))
                {
                    continue;
                }

                if (!map.TryGetValue(role, out var list))
                {
                    list = new List<ObjectId>();
                    map[role] = list;
                }

                list.Add(id);
            }

            return map;
        }

        public static Point3d ResolveOrigin(Dictionary<string, List<Line>> lines)
        {
            if (lines.TryGetValue("BOT", out var bots) && bots.Count > 0)
            {
                var bot = bots[0];
                return Leftish(bot.StartPoint, bot.EndPoint);
            }

            if (lines.TryGetValue("BL", out var bls) && bls.Count > 0)
            {
                var bl = bls[0];
                return Lowerish(bl.StartPoint, bl.EndPoint);
            }

            throw new InvalidOperationException("Need BOT or BL tagged to find origin (bottom-left).");
        }

        public static void ApplySize(
            Dictionary<string, List<Line>> lines,
            Point3d origin,
            double width,
            double heightTop,
            double heightBot)
        {
            var x0 = origin.X;
            var y0 = origin.Y;
            var x1 = x0 + width;
            var yMid = y0 + heightBot;
            var yTop = yMid + heightTop;
            var z = origin.Z;

            SetLines(lines, "BOT", new Point3d(x0, y0, z), new Point3d(x1, y0, z));
            SetLines(lines, "MID", new Point3d(x0, yMid, z), new Point3d(x1, yMid, z));
            SetLines(lines, "TOP", new Point3d(x0, yTop, z), new Point3d(x1, yTop, z));
            SetLines(lines, "BL", new Point3d(x0, y0, z), new Point3d(x0, yMid, z));
            SetLines(lines, "BR", new Point3d(x1, y0, z), new Point3d(x1, yMid, z));
            SetLines(lines, "TL", new Point3d(x0, yMid, z), new Point3d(x0, yTop, z));
            SetLines(lines, "TR", new Point3d(x1, yMid, z), new Point3d(x1, yTop, z));
        }

        private static void SetLines(
            Dictionary<string, List<Line>> lines,
            string role,
            Point3d start,
            Point3d end)
        {
            if (!lines.TryGetValue(role, out var list))
            {
                return;
            }

            foreach (var line in list)
            {
                line.UpgradeOpen();
                line.StartPoint = start;
                line.EndPoint = end;
            }
        }

        private static Point3d Leftish(Point3d a, Point3d b)
        {
            return a.X <= b.X ? a : b;
        }

        private static Point3d Lowerish(Point3d a, Point3d b)
        {
            return a.Y <= b.Y ? a : b;
        }
    }
}
