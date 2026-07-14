using System;
using System.Collections.Generic;
using Gssoft.Gscad.DatabaseServices;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Exporters
{
    /// <summary>
    /// Exports named block definitions (non-layout, non-anonymous).
    /// </summary>
    public static class BlockExporter
    {
        /// <summary>
        /// Returns block name + entity count for each named block definition.
        /// </summary>
        public static List<BlockInfo> Export(Database db, Transaction tr)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            var result = new List<BlockInfo>();
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId id in blockTable)
            {
                try
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (btr.IsLayout || btr.IsAnonymous)
                    {
                        continue;
                    }

                    result.Add(new BlockInfo
                    {
                        BlockName = btr.Name,
                        EntityCount = CountEntities(btr)
                    });
                }
                catch (Exception)
                {
                    // Skip invalid block records.
                }
            }

            result.Sort((a, b) => string.Compare(a.BlockName, b.BlockName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static int CountEntities(BlockTableRecord btr)
        {
            var count = 0;
            foreach (ObjectId id in btr)
            {
                if (id.IsNull || id.IsErased)
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }
}
