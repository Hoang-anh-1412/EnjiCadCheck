using System;
using System.Collections.Generic;
using System.Globalization;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.Runtime;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadCheck.SampleXDataCommands))]

namespace EnjiCadCheck
{
    /// <summary>
    /// CAD commands to tag and resize the 2-rectangle XData sample.
    /// </summary>
    public class SampleXDataCommands
    {
        [CommandMethod("XTAG")]
        public void TagEntity()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== XTAG ==========");
            ed.WriteMessage("\nFlow: type role → window/select many → Enter.");
            ed.WriteMessage("\nThen type next role, or Enter empty to finish.");
            ed.WriteMessage("\nSample roles: TOP MID BOT TL TR BL BR | DIM_W DIM_H1 DIM_H2");

            var totalTagged = 0;
            try
            {
                using (doc.LockDocument())
                {
                    while (true)
                    {
                        var keyOpts = new PromptStringOptions("\nRole key (Enter empty = done): ")
                        {
                            AllowSpaces = false,
                            UseDefaultValue = true,
                            DefaultValue = ""
                        };
                        var keyRes = ed.GetString(keyOpts);
                        if (keyRes.Status != PromptStatus.OK)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(keyRes.StringResult))
                        {
                            break;
                        }

                        var role = keyRes.StringResult.Trim().ToUpperInvariant();

                        var selOpts = new PromptSelectionOptions
                        {
                            MessageForAdding = string.Format(
                                "\nSelect entities for role [{0}] (window OK): ",
                                role),
                            AllowDuplicates = false
                        };
                        var selRes = ed.GetSelection(selOpts);
                        if (selRes.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\n(no selection for {0}, skipped)", role);
                            continue;
                        }

                        using (var tr = doc.TransactionManager.StartTransaction())
                        {
                            SampleXData.EnsureRegApp(doc.Database, tr);
                            var count = 0;
                            foreach (SelectedObject so in selRes.Value)
                            {
                                if (so == null || so.ObjectId.IsNull)
                                {
                                    continue;
                                }

                                var ent = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Entity;
                                if (ent == null)
                                {
                                    continue;
                                }

                                SampleXData.SetRole(ent, role);
                                count++;
                            }

                            tr.Commit();
                            totalTagged += count;
                            ed.WriteMessage("\nOK: role={0} tagged={1}", role, count);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nFAIL: {0}", ex.Message);
            }

            ed.WriteMessage("\nDone. Total tagged this run: {0}", totalTagged);
            ed.WriteMessage("\n==========================\n");
        }

        [CommandMethod("XTAGS")]
        public void ListTags()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== XTAGS ==========");

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var map = SampleXData.CollectRoles(doc.Database, tr);
                    if (map.Count == 0)
                    {
                        ed.WriteMessage("\n(no ENJI_SAMPLE XData found)");
                    }
                    else
                    {
                        var entityCount = 0;
                        foreach (var kv in map)
                        {
                            foreach (var id in kv.Value)
                            {
                                var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                ed.WriteMessage(
                                    "\n  {0,-8} handle={1}  {2}",
                                    kv.Key,
                                    ent.Handle,
                                    ent.GetType().Name);
                                entityCount++;
                            }
                        }

                        ed.WriteMessage(
                            "\nRoles: {0} | Entities: {1}",
                            map.Count,
                            entityCount);
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nFAIL: {0}", ex.Message);
            }

            ed.WriteMessage("\n===========================\n");
        }

        [CommandMethod("XSIZE")]
        public void ApplySize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== XSIZE ==========");
            ed.WriteMessage("\nAnchor: bottom-left of BOT (or BL). Updates tagged lines + DIM_* text.");

            if (!TryGetPositiveDouble(ed, "\nWidth W: ", out var width)
                || !TryGetPositiveDouble(ed, "\nTop height H1: ", out var h1)
                || !TryGetPositiveDouble(ed, "\nBottom height H2: ", out var h2))
            {
                ed.WriteMessage("\nCancelled.\n");
                return;
            }

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var roleIds = SampleXData.CollectRoles(doc.Database, tr);
                    var lines = new Dictionary<string, List<Line>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var role in SampleXData.LineRoles)
                    {
                        if (!roleIds.TryGetValue(role, out var ids))
                        {
                            continue;
                        }

                        var list = new List<Line>();
                        foreach (var id in ids)
                        {
                            var line = tr.GetObject(id, OpenMode.ForRead) as Line;
                            if (line == null)
                            {
                                ed.WriteMessage(
                                    "\nWARN: role {0} handle not a Line (skipped)",
                                    role);
                                continue;
                            }

                            list.Add(line);
                        }

                        if (list.Count > 0)
                        {
                            lines[role] = list;
                        }
                    }

                    if (!lines.ContainsKey("BOT") && !lines.ContainsKey("BL"))
                    {
                        throw new InvalidOperationException(
                            "Tag BOT or BL first (XTAG). Need an origin.");
                    }

                    var missing = new List<string>();
                    foreach (var role in SampleXData.LineRoles)
                    {
                        if (!lines.ContainsKey(role))
                        {
                            missing.Add(role);
                        }
                    }

                    if (missing.Count > 0)
                    {
                        ed.WriteMessage(
                            "\nWARN: missing line roles: {0}",
                            string.Join(", ", missing));
                    }

                    var origin = SampleXData.ResolveOrigin(lines);
                    SampleXData.ApplySize(lines, origin, width, h1, h2);

                    UpdateDimText(tr, roleIds, "DIM_W", width);
                    UpdateDimText(tr, roleIds, "DIM_H1", h1);
                    UpdateDimText(tr, roleIds, "DIM_H2", h2);

                    tr.Commit();
                    ed.WriteMessage(
                        "\nOK: W={0} H1={1} H2={2} origin=({3:0.###},{4:0.###})",
                        width,
                        h1,
                        h2,
                        origin.X,
                        origin.Y);
                    ed.WriteMessage("\nTip: run RE / REGEN if dims look stale.");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nFAIL: {0}", ex.Message);
            }

            ed.WriteMessage("\n==========================\n");
        }

        [CommandMethod("XDRAW")]
        public void DrawSample()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== XDRAW ==========");
            ed.WriteMessage("\nDraws a tagged 2-rectangle sample at a picked point.");

            var pOpts = new PromptPointOptions("\nBottom-left corner: ");
            var pRes = ed.GetPoint(pOpts);
            if (pRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nCancelled.\n");
                return;
            }

            const double width = 28.0;
            const double h1 = 11.5;
            const double h2 = 13.0;
            var origin = pRes.Value;

            try
            {
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    SampleXData.EnsureRegApp(doc.Database, tr);
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite);

                    var x0 = origin.X;
                    var y0 = origin.Y;
                    var x1 = x0 + width;
                    var yMid = y0 + h2;
                    var yTop = yMid + h1;
                    var z = origin.Z;

                    AddTaggedLine(tr, ms, "BOT", new Point3d(x0, y0, z), new Point3d(x1, y0, z));
                    AddTaggedLine(tr, ms, "MID", new Point3d(x0, yMid, z), new Point3d(x1, yMid, z));
                    AddTaggedLine(tr, ms, "TOP", new Point3d(x0, yTop, z), new Point3d(x1, yTop, z));
                    AddTaggedLine(tr, ms, "BL", new Point3d(x0, y0, z), new Point3d(x0, yMid, z));
                    AddTaggedLine(tr, ms, "BR", new Point3d(x1, y0, z), new Point3d(x1, yMid, z));
                    AddTaggedLine(tr, ms, "TL", new Point3d(x0, yMid, z), new Point3d(x0, yTop, z));
                    AddTaggedLine(tr, ms, "TR", new Point3d(x1, yMid, z), new Point3d(x1, yTop, z));

                    tr.Commit();
                    ed.WriteMessage(
                        "\nOK: sample drawn W={0} H1={1} H2={2} (lines tagged).",
                        width,
                        h1,
                        h2);
                    ed.WriteMessage("\nNext: XTAGS to verify, then XSIZE to resize.");
                    ed.WriteMessage("\nNote: dimensions not created — tag your own dims with XTAG if needed.");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nFAIL: {0}", ex.Message);
            }

            ed.WriteMessage("\n==========================\n");
        }

        private static void AddTaggedLine(
            Transaction tr,
            BlockTableRecord ms,
            string role,
            Point3d start,
            Point3d end)
        {
            var line = new Line(start, end);
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            SampleXData.SetRole(line, role);
        }

        private static void UpdateDimText(
            Transaction tr,
            Dictionary<string, List<ObjectId>> roleIds,
            string role,
            double value)
        {
            if (!roleIds.TryGetValue(role, out var ids))
            {
                return;
            }

            var text = value.ToString("0.####", CultureInfo.InvariantCulture);
            foreach (var id in ids)
            {
                var dim = tr.GetObject(id, OpenMode.ForWrite) as Dimension;
                if (dim == null)
                {
                    continue;
                }

                dim.DimensionText = text;
                try
                {
                    dim.RecomputeDimensionBlock(true);
                }
                catch
                {
                    // Some hosts accept text override without recompute.
                }
            }
        }

        private static bool TryGetPositiveDouble(Editor ed, string prompt, out double value)
        {
            value = 0;
            var opts = new PromptDoubleOptions(prompt)
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false
            };
            var res = ed.GetDouble(opts);
            if (res.Status != PromptStatus.OK)
            {
                return false;
            }

            value = res.Value;
            return true;
        }
    }
}
