using System;
using System.Collections.Generic;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;

namespace EnjiCadCheck
{
    /// <summary>
    /// Smoke-test read/write for common entity kinds (G0.1 checklist).
    /// Write tests are reversible: mutate then restore original values.
    /// </summary>
    public static class EntityApiProbe
    {
        public static void Run(Database db, Editor ed)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var samples = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
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

                    var kind = Classify(ent);
                    if (!counts.ContainsKey(kind))
                    {
                        counts[kind] = 0;
                    }

                    counts[kind]++;

                    if (samples.Count < 40)
                    {
                        samples.Add(id);
                    }
                }

                ed.WriteMessage("\n--- READ: ModelSpace counts ---");
                if (counts.Count == 0)
                {
                    ed.WriteMessage("\n(empty ModelSpace)");
                }
                else
                {
                    foreach (var kv in counts)
                    {
                        ed.WriteMessage("\n  {0}: {1}", kv.Key, kv.Value);
                    }
                }

                ReportSamples(tr, ed, samples);

                ed.WriteMessage("\n--- WRITE: reversible probes ---");
                ProbeWriteBlock(tr, ed, samples);
                ProbeWriteText(tr, ed, samples);
                ProbeWriteDim(tr, ed, samples);
                ProbeWriteTable(tr, ed, samples);

                tr.Commit();
            }
        }

        private static string Classify(Entity ent)
        {
            if (ent is BlockReference)
            {
                return "BlockReference";
            }

            if (ent is DBText)
            {
                return "DBText";
            }

            if (ent is MText)
            {
                return "MText";
            }

            if (ent is Dimension)
            {
                return "Dimension";
            }

            if (ent is Table)
            {
                return "Table";
            }

            if (ent is Line)
            {
                return "Line";
            }

            if (ent is Circle)
            {
                return "Circle";
            }

            if (ent is Arc)
            {
                return "Arc";
            }

            if (ent is Polyline)
            {
                return "Polyline";
            }

            return ent.GetType().Name;
        }

        private static void ReportSamples(Transaction tr, Editor ed, List<ObjectId> samples)
        {
            ed.WriteMessage("\n--- READ: first samples ---");
            var shown = 0;

            foreach (var id in samples)
            {
                if (shown >= 12)
                {
                    break;
                }

                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    continue;
                }

                if (ent is BlockReference br)
                {
                    ed.WriteMessage(
                        "\n  Block '{0}' at ({1:F2},{2:F2}) handle={3}",
                        SafeBlockName(br),
                        br.Position.X,
                        br.Position.Y,
                        br.Handle);
                    shown++;
                    continue;
                }

                if (ent is DBText dbText)
                {
                    ed.WriteMessage(
                        "\n  DBText '{0}' handle={1}",
                        Truncate(dbText.TextString, 40),
                        dbText.Handle);
                    shown++;
                    continue;
                }

                if (ent is MText mText)
                {
                    ed.WriteMessage(
                        "\n  MText '{0}' handle={1}",
                        Truncate(mText.Contents, 40),
                        mText.Handle);
                    shown++;
                    continue;
                }

                if (ent is Dimension dim)
                {
                    ed.WriteMessage(
                        "\n  Dim type={0} measurement={1:F3} handle={2}",
                        dim.GetType().Name,
                        dim.Measurement,
                        dim.Handle);
                    shown++;
                    continue;
                }

                if (ent is Table table)
                {
                    ed.WriteMessage(
                        "\n  Table rows={0} cols={1} handle={2}",
                        table.Rows.Count,
                        table.Columns.Count,
                        table.Handle);
                    shown++;
                }
            }

            if (shown == 0)
            {
                ed.WriteMessage("\n  (no Block/Text/Dim/Table samples)");
            }
        }

        private static void ProbeWriteBlock(Transaction tr, Editor ed, List<ObjectId> samples)
        {
            var br = FindFirst<BlockReference>(tr, samples);
            if (br == null)
            {
                ed.WriteMessage("\n  Block: SKIP (none found)");
                return;
            }

            try
            {
                br.UpgradeOpen();
                var original = br.Position;
                var delta = new Vector3d(0.001, 0, 0);
                br.Position = original + delta;
                br.Position = original;
                ed.WriteMessage("\n  Block: OK write/restore '{0}'", SafeBlockName(br));
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n  Block: FAIL - {0}", ex.Message);
            }
        }

        private static void ProbeWriteText(Transaction tr, Editor ed, List<ObjectId> samples)
        {
            var dbText = FindFirst<DBText>(tr, samples);
            if (dbText != null)
            {
                try
                {
                    dbText.UpgradeOpen();
                    var original = dbText.TextString;
                    dbText.TextString = original + " ";
                    dbText.TextString = original;
                    ed.WriteMessage("\n  DBText: OK write/restore handle={0}", dbText.Handle);
                }
                catch (Exception ex)
                {
                    ed.WriteMessage("\n  DBText: FAIL - {0}", ex.Message);
                }

                return;
            }

            var mText = FindFirst<MText>(tr, samples);
            if (mText == null)
            {
                ed.WriteMessage("\n  Text: SKIP (none found)");
                return;
            }

            try
            {
                mText.UpgradeOpen();
                var original = mText.Contents;
                mText.Contents = original + " ";
                mText.Contents = original;
                ed.WriteMessage("\n  MText: OK write/restore handle={0}", mText.Handle);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n  MText: FAIL - {0}", ex.Message);
            }
        }

        private static void ProbeWriteDim(Transaction tr, Editor ed, List<ObjectId> samples)
        {
            var dim = FindFirst<Dimension>(tr, samples);
            if (dim == null)
            {
                ed.WriteMessage("\n  Dim: SKIP (none found)");
                return;
            }

            try
            {
                dim.UpgradeOpen();
                var original = dim.DimensionText;
                // Touch DimensionText then restore (empty = use measured value).
                dim.DimensionText = original;
                dim.RecomputeDimensionBlock(true);
                ed.WriteMessage(
                    "\n  Dim: OK touch/recompute type={0} handle={1}",
                    dim.GetType().Name,
                    dim.Handle);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n  Dim: FAIL - {0}", ex.Message);
            }
        }

        private static void ProbeWriteTable(Transaction tr, Editor ed, List<ObjectId> samples)
        {
            var table = FindFirst<Table>(tr, samples);
            if (table == null)
            {
                ed.WriteMessage("\n  Table: SKIP (none found)");
                return;
            }

            try
            {
                if (table.Rows.Count < 1 || table.Columns.Count < 1)
                {
                    ed.WriteMessage("\n  Table: SKIP (empty)");
                    return;
                }

                table.UpgradeOpen();
                var original = table.Cells[0, 0].TextString;
                table.Cells[0, 0].TextString = original;
                ed.WriteMessage("\n  Table: OK write cell[0,0] handle={0}", table.Handle);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n  Table: FAIL - {0}", ex.Message);
            }
        }

        private static T FindFirst<T>(Transaction tr, List<ObjectId> samples) where T : Entity
        {
            foreach (var id in samples)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as T;
                if (ent != null)
                {
                    return ent;
                }
            }

            return null;
        }

        private static string SafeBlockName(BlockReference br)
        {
            try
            {
                return br.Name ?? "(unnamed)";
            }
            catch
            {
                return "(dynamic?)";
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var oneLine = value.Replace("\r", " ").Replace("\n", " ");
            if (oneLine.Length <= max)
            {
                return oneLine;
            }

            return oneLine.Substring(0, max) + "...";
        }
    }
}
