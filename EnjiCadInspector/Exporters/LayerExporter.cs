using System;
using System.Collections.Generic;
using Gssoft.Gscad.DatabaseServices;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Exporters
{
    /// <summary>
    /// Exports all layer table records.
    /// </summary>
    public static class LayerExporter
    {
        /// <summary>
        /// Reads every layer and returns a sorted list of <see cref="LayerInfo"/>.
        /// </summary>
        public static List<LayerInfo> Export(Database db, Transaction tr)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            var result = new List<LayerInfo>();
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var names = new List<string>();

            foreach (ObjectId id in layerTable)
            {
                try
                {
                    var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    names.Add(layer.Name);
                }
                catch (Exception)
                {
                    // Skip corrupt layer entries.
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                try
                {
                    var layer = (LayerTableRecord)tr.GetObject(layerTable[name], OpenMode.ForRead);
                    result.Add(new LayerInfo
                    {
                        Name = layer.Name,
                        Color = SafeColor(layer),
                        Linetype = SafeLinetype(layer, tr),
                        IsLocked = layer.IsLocked,
                        IsFrozen = layer.IsFrozen,
                        IsOff = layer.IsOff
                    });
                }
                catch (Exception)
                {
                    // Skip individual layer failures; continue exporting others.
                }
            }

            return result;
        }

        private static string SafeColor(LayerTableRecord layer)
        {
            try
            {
                return layer.Color != null ? layer.Color.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private static string SafeLinetype(LayerTableRecord layer, Transaction tr)
        {
            try
            {
                if (layer.LinetypeObjectId.IsNull)
                {
                    return string.Empty;
                }

                var ltr = tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                return ltr != null ? ltr.Name : string.Empty;
            }
            catch (Exception)
            {
                try
                {
                    // Some hosts expose a string property; keep best-effort fallback.
                    return layer.LinetypeObjectId.ToString();
                }
                catch (Exception ex)
                {
                    return "Error: " + ex.Message;
                }
            }
        }
    }
}
