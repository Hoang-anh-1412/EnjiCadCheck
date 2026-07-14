using System;
using System.IO;
using Gssoft.Gscad.DatabaseServices;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Exporters
{
    /// <summary>
    /// Exports top-level drawing metadata (name, version, units, extents).
    /// </summary>
    public static class DrawingExporter
    {
        /// <summary>
        /// Builds <see cref="DrawingInfo"/> for the given database.
        /// </summary>
        public static DrawingInfo Export(Database db, Transaction tr)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            var info = new DrawingInfo
            {
                DrawingName = ResolveDrawingName(db.Filename),
                DwgVersion = SafeDwgVersion(db),
                InsertionUnits = SafeInsertionUnits(db),
                GeometricExtents = ComputeGeometricExtents(db, tr)
            };

            return info;
        }

        private static string ResolveDrawingName(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return "(unsaved)";
            }

            try
            {
                return Path.GetFileName(filename);
            }
            catch (Exception)
            {
                return filename;
            }
        }

        private static string SafeDwgVersion(Database db)
        {
            try
            {
                return db.LastSavedAsVersion.ToString();
            }
            catch (Exception ex)
            {
                return "Unknown (" + ex.Message + ")";
            }
        }

        private static string SafeInsertionUnits(Database db)
        {
            try
            {
                return db.Insunits.ToString();
            }
            catch (Exception ex)
            {
                return "Unknown (" + ex.Message + ")";
            }
        }

        /// <summary>
        /// Prefers Database Extmin/Extmax; falls back to scanning model-space entity extents.
        /// </summary>
        private static BoundsInfo ComputeGeometricExtents(Database db, Transaction tr)
        {
            try
            {
                var min = db.Extmin;
                var max = db.Extmax;
                if (!double.IsInfinity(min.X) && !double.IsInfinity(max.X))
                {
                    return new BoundsInfo
                    {
                        Min = GeometryHelper.ToPoint(min),
                        Max = GeometryHelper.ToPoint(max)
                    };
                }
            }
            catch (Exception)
            {
                // Fall through to entity scan.
            }

            BoundsInfo merged = null;
            try
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    if (id.IsNull || id.IsErased)
                    {
                        continue;
                    }

                    Entity entity;
                    try
                    {
                        entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (entity == null)
                    {
                        continue;
                    }

                    merged = GeometryHelper.MergeBounds(merged, GeometryHelper.TryGetBounds(entity));
                }
            }
            catch (Exception)
            {
                return merged;
            }

            return merged;
        }
    }
}
