using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;

namespace EnjiCadCheck
{
    /// <summary>
    /// G0.2 inventory dump: layers, blocks, dims, text, tables + encoding suspects.
    /// Writes Markdown beside the DWG and mirrors lines to the Editor (F2 copy).
    /// </summary>
    public static class DrawingInventory
    {
        private const int MaxTextLines = 400;
        private const int MaxDimLines = 200;
        private const int MaxBlockRefLines = 200;

        public static string Run(Database db, Editor ed)
        {
            var lines = new List<string>();
            Action<string> add = s => lines.Add(s ?? "");

            add("# Drawing inventory (G0.2)");
            add("");
            add("- Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            add("- DWG: `" + (db.Filename ?? "(unsaved)") + "`");
            add("");

            using (var tr = db.TransactionManager.StartTransaction())
            {
                DumpLayers(tr, db, add);
                DumpBlockDefinitions(tr, db, add);
                DumpModelSpace(tr, db, add);
                tr.Commit();
            }

            add("");
            add("---");
            add("End of inventory. Paste this log (or the .md file) back to the assistant.");

            var outPath = ResolveOutputPath(db.Filename);
            File.WriteAllText(outPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            ed.WriteMessage("\n========== TANKINV ==========");
            ed.WriteMessage("\nWrote: {0}", outPath);
            ed.WriteMessage("\n--- inventory log (also in .md) ---");
            foreach (var line in lines)
            {
                ed.WriteMessage("\n{0}", line);
            }
            ed.WriteMessage("\n========== TANKINV DONE ==========\n");

            return outPath;
        }

        private static string ResolveOutputPath(string dwgPath)
        {
            if (!string.IsNullOrWhiteSpace(dwgPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(dwgPath);
                    var name = Path.GetFileNameWithoutExtension(dwgPath);
                    if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
                    {
                        return Path.Combine(dir, name + "-inventory.md");
                    }
                }
                catch
                {
                    // fall through
                }
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "tank-inventory.md");
        }

        private static void DumpLayers(Transaction tr, Database db, Action<string> add)
        {
            add("## Layers");
            add("");
            add("| Name | On | Frozen | Locked | Color |");
            add("|------|----|--------|--------|-------|");

            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var names = new List<string>();
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                names.Add(layer.Name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                var layer = (LayerTableRecord)tr.GetObject(lt[name], OpenMode.ForRead);
                add(string.Format(
                    "| `{0}` | {1} | {2} | {3} | {4} |",
                    EscapeCell(layer.Name),
                    layer.IsOff ? "off" : "on",
                    layer.IsFrozen ? "yes" : "no",
                    layer.IsLocked ? "yes" : "no",
                    layer.Color));
            }

            add("");
            add("- Layer count: " + names.Count);
            add("");
        }

        private static void DumpBlockDefinitions(Transaction tr, Database db, Action<string> add)
        {
            add("## Block definitions");
            add("");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var defs = new List<string>();

            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout || btr.IsAnonymous)
                {
                    continue;
                }

                defs.Add(btr.Name);
            }

            defs.Sort(StringComparer.OrdinalIgnoreCase);
            if (defs.Count == 0)
            {
                add("_None (no named block definitions)._");
            }
            else
            {
                foreach (var name in defs)
                {
                    add("- `" + EscapeCell(name) + "`");
                }
            }

            add("");
            add("- Named block definition count: " + defs.Count);
            add("");
        }

        private static void DumpModelSpace(Transaction tr, Database db, Action<string> add)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var blockRefs = new List<string>();
            var dims = new List<string>();
            var texts = new List<string>();
            var tables = new List<string>();
            var suspects = new List<string>();

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

                if (ent is BlockReference br)
                {
                    if (blockRefs.Count < MaxBlockRefLines)
                    {
                        blockRefs.Add(string.Format(
                            "- `{0}` pos=({1:F2},{2:F2}) layer=`{3}` handle={4}",
                            EscapeCell(SafeBlockName(br)),
                            br.Position.X,
                            br.Position.Y,
                            EscapeCell(br.Layer),
                            br.Handle));
                    }

                    CollectAttributes(br, tr, texts, suspects);
                    continue;
                }

                if (ent is DBText dbText)
                {
                    CollectText("DBText", dbText.TextString, dbText.Layer, dbText.Handle.ToString(), texts, suspects);
                    continue;
                }

                if (ent is MText mText)
                {
                    CollectText("MText", mText.Contents, mText.Layer, mText.Handle.ToString(), texts, suspects);
                    continue;
                }

                if (ent is Dimension dim)
                {
                    if (dims.Count < MaxDimLines)
                    {
                        dims.Add(string.Format(
                            "- {0} meas={1:F3} text=`{2}` layer=`{3}` handle={4}",
                            dim.GetType().Name,
                            dim.Measurement,
                            EscapeCell(Truncate(dim.DimensionText, 60)),
                            EscapeCell(dim.Layer),
                            dim.Handle));
                    }

                    if (LooksSuspicious(dim.DimensionText))
                    {
                        suspects.Add(string.Format(
                            "- Dim handle={0} text=`{1}`",
                            dim.Handle,
                            EscapeCell(Truncate(dim.DimensionText, 80))));
                    }

                    continue;
                }

                if (ent is Table table)
                {
                    tables.Add(string.Format(
                        "- rows={0} cols={1} layer=`{2}` handle={3}",
                        table.Rows.Count,
                        table.Columns.Count,
                        EscapeCell(table.Layer),
                        table.Handle));

                    DumpTableCells(table, texts, suspects);
                }
            }

            add("## ModelSpace entity counts");
            add("");
            foreach (var kv in counts)
            {
                add("- " + kv.Key + ": " + kv.Value);
            }

            add("");
            add("## Block references (ModelSpace)");
            add("");
            if (blockRefs.Count == 0)
            {
                add("_None._");
            }
            else
            {
                foreach (var line in blockRefs)
                {
                    add(line);
                }

                if (counts.ContainsKey("BlockReference") && counts["BlockReference"] > blockRefs.Count)
                {
                    add(string.Format("_… truncated, total BlockReference={0}_", counts["BlockReference"]));
                }
            }

            add("");
            add("## Dimensions (ModelSpace)");
            add("");
            if (dims.Count == 0)
            {
                add("_None._");
            }
            else
            {
                foreach (var line in dims)
                {
                    add(line);
                }

                if (counts.ContainsKey("Dimension") && counts["Dimension"] > dims.Count)
                {
                    add(string.Format("_… truncated, total Dimension={0}_", counts["Dimension"]));
                }
            }

            add("");
            add("## Text / attributes (ModelSpace)");
            add("");
            if (texts.Count == 0)
            {
                add("_None._");
            }
            else
            {
                var shown = texts.Take(MaxTextLines).ToList();
                foreach (var line in shown)
                {
                    add(line);
                }

                if (texts.Count > shown.Count)
                {
                    add(string.Format("_… truncated, listed {0}/{1}_", shown.Count, texts.Count));
                }
            }

            add("");
            add("## Tables (ModelSpace)");
            add("");
            if (tables.Count == 0)
            {
                add("_None._");
            }
            else
            {
                foreach (var line in tables)
                {
                    add(line);
                }
            }

            add("");
            add("## Encoding suspects (`????` / replacement / heavy `?`)");
            add("");
            if (suspects.Count == 0)
            {
                add("_None detected by heuristic._");
            }
            else
            {
                foreach (var line in suspects.Distinct().Take(100))
                {
                    add(line);
                }
            }

            add("");
        }

        private static void CollectAttributes(
            BlockReference br,
            Transaction tr,
            List<string> texts,
            List<string> suspects)
        {
            if (br.AttributeCollection == null)
            {
                return;
            }

            foreach (ObjectId attId in br.AttributeCollection)
            {
                if (attId.IsNull || attId.IsErased)
                {
                    continue;
                }

                var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (att == null)
                {
                    continue;
                }

                CollectText(
                    "Attrib:" + (att.Tag ?? ""),
                    att.TextString,
                    att.Layer,
                    att.Handle.ToString(),
                    texts,
                    suspects);
            }
        }

        private static void DumpTableCells(Table table, List<string> texts, List<string> suspects)
        {
            var rows = Math.Min(table.Rows.Count, 30);
            var cols = Math.Min(table.Columns.Count, 20);
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    string cellText;
                    try
                    {
                        cellText = table.Cells[r, c].TextString ?? "";
                    }
                    catch
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(cellText))
                    {
                        continue;
                    }

                    CollectText(
                        string.Format("Table[{0},{1}]", r, c),
                        cellText,
                        table.Layer,
                        table.Handle.ToString(),
                        texts,
                        suspects);
                }
            }
        }

        private static void CollectText(
            string kind,
            string raw,
            string layer,
            string handle,
            List<string> texts,
            List<string> suspects)
        {
            var value = raw ?? "";
            var line = string.Format(
                "- {0} `{1}` layer=`{2}` handle={3}",
                kind,
                EscapeCell(Truncate(OneLine(value), 80)),
                EscapeCell(layer),
                handle);

            texts.Add(line);

            if (LooksSuspicious(value))
            {
                suspects.Add(line + " **SUSPECT**");
            }
        }

        private static bool LooksSuspicious(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (value.IndexOf('\uFFFD') >= 0)
            {
                return true;
            }

            if (value.Contains("????"))
            {
                return true;
            }

            var q = 0;
            foreach (var ch in value)
            {
                if (ch == '?')
                {
                    q++;
                }
            }

            return q >= 3;
        }

        private static string Classify(Entity ent)
        {
            if (ent is BlockReference) return "BlockReference";
            if (ent is DBText) return "DBText";
            if (ent is MText) return "MText";
            if (ent is Dimension) return "Dimension";
            if (ent is Table) return "Table";
            if (ent is Line) return "Line";
            if (ent is Circle) return "Circle";
            if (ent is Arc) return "Arc";
            if (ent is Polyline) return "Polyline";
            return ent.GetType().Name;
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

        private static string OneLine(string value)
        {
            return value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
        }

        private static string EscapeCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return OneLine(value).Replace("`", "'");
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.Length <= max)
            {
                return value;
            }

            return value.Substring(0, max) + "...";
        }
    }
}
