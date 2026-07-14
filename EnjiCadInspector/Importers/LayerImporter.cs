using System;
using System.Collections.Generic;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Importers
{
    /// <summary>
    /// Creates missing layers on the target database from exported layer metadata.
    /// </summary>
    public static class LayerImporter
    {
        /// <summary>
        /// Ensures every layer in <paramref name="layers"/> exists (creates when missing).
        /// </summary>
        public static int EnsureLayers(Database db, Transaction tr, IList<LayerInfo> layers, Action<string> onWarn = null)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            if (layers == null || layers.Count == 0)
            {
                return 0;
            }

            var created = 0;
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            foreach (var info in layers)
            {
                if (info == null || string.IsNullOrWhiteSpace(info.Name))
                {
                    continue;
                }

                try
                {
                    if (layerTable.Has(info.Name))
                    {
                        continue;
                    }

                    var record = new LayerTableRecord
                    {
                        Name = info.Name
                    };

                    ApplyColor(record, info.Color, onWarn);
                    ApplyLinetype(record, db, tr, info.Linetype, onWarn);

                    try
                    {
                        record.IsLocked = info.IsLocked;
                    }
                    catch (Exception)
                    {
                        // Ignore flag failures on create.
                    }

                    try
                    {
                        record.IsFrozen = info.IsFrozen;
                    }
                    catch (Exception)
                    {
                        // Layer 0 cannot freeze; ignore.
                    }

                    try
                    {
                        record.IsOff = info.IsOff;
                    }
                    catch (Exception)
                    {
                        // Ignore.
                    }

                    layerTable.Add(record);
                    tr.AddNewlyCreatedDBObject(record, true);
                    created++;
                }
                catch (Exception ex)
                {
                    onWarn?.Invoke("Layer '" + info.Name + "': " + ex.Message);
                }
            }

            return created;
        }

        private static void ApplyColor(LayerTableRecord record, string colorText, Action<string> onWarn)
        {
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return;
            }

            try
            {
                // Exported Color.ToString() is typically ACI name or "ColorMethod=ByAci, ColorIndex=1".
                short index;
                if (TryParseAciIndex(colorText, out index))
                {
                    record.Color = Color.FromColorIndex(ColorMethod.ByAci, index);
                    return;
                }

                var named = MapNamedColor(colorText);
                if (named.HasValue)
                {
                    record.Color = Color.FromColorIndex(ColorMethod.ByAci, named.Value);
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke("Layer color '" + colorText + "': " + ex.Message);
            }
        }

        private static void ApplyLinetype(
            LayerTableRecord record,
            Database db,
            Transaction tr,
            string linetypeName,
            Action<string> onWarn)
        {
            if (string.IsNullOrWhiteSpace(linetypeName))
            {
                return;
            }

            try
            {
                var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(linetypeName))
                {
                    record.LinetypeObjectId = lt[linetypeName];
                    return;
                }

                if (lt.Has("Continuous"))
                {
                    record.LinetypeObjectId = lt["Continuous"];
                }
                else if (lt.Has("CONTINUOUS"))
                {
                    record.LinetypeObjectId = lt["CONTINUOUS"];
                }
            }
            catch (Exception ex)
            {
                onWarn?.Invoke("Layer linetype '" + linetypeName + "': " + ex.Message);
            }
        }

        private static bool TryParseAciIndex(string colorText, out short index)
        {
            index = 0;
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return false;
            }

            // Patterns: "1", "Red", "ColorIndex=1", "BYLAYER"
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
