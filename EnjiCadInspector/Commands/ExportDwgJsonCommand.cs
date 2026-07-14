using System;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using EnjiCadInspector.Exporters;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadInspector.Commands.ExportDwgJsonCommand))]

namespace EnjiCadInspector.Commands
{
    /// <summary>
    /// CADian / enjiCAD command: EXPORT_DWG_JSON
    /// Exports the active drawing to drawing.json beside the DWG file.
    /// </summary>
    public class ExportDwgJsonCommand
    {
        /// <summary>
        /// Entry point registered with the host via [CommandMethod].
        /// </summary>
        [CommandMethod("EXPORT_DWG_JSON")]
        public void ExportDwgJson()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== EXPORT_DWG_JSON ==========");

            try
            {
                string outputPath;
                using (doc.LockDocument())
                {
                    outputPath = RunExport(doc, ed);
                }

                ed.WriteMessage("\nStatus:   OK");
                ed.WriteMessage("\nWrote:    {0}", outputPath);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nStatus:   FAIL - {0}", ex.Message);
                if (ex.InnerException != null)
                {
                    ed.WriteMessage("\nInner:    {0}", ex.InnerException.Message);
                }
            }

            ed.WriteMessage("\n=====================================\n");
        }

        /// <summary>
        /// Builds the export payload and writes drawing.json.
        /// </summary>
        private static string RunExport(Document doc, Editor ed)
        {
            var db = doc.Database;
            var outputPath = JsonHelper.ResolveOutputPath(db.Filename);
            var errorCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                ed.WriteMessage("\nReading drawing metadata...");
                var drawing = DrawingExporter.Export(db, tr);

                ed.WriteMessage("\nReading layers...");
                var layers = LayerExporter.Export(db, tr);

                ed.WriteMessage("\nReading block definitions...");
                var blocks = BlockExporter.Export(db, tr);

                ed.WriteMessage("\nReading entities...");
                var entities = EntityExporter.Export(db, tr, (id, message) =>
                {
                    errorCount++;
                    if (errorCount <= 20)
                    {
                        ed.WriteMessage("\n  WARN [{0}]: {1}", id, message);
                    }
                });

                ed.WriteMessage("\nBuilding summary...");
                var summary = SummaryExporter.Export(entities);

                var result = new ExportResult
                {
                    Drawing = drawing,
                    Summary = summary,
                    Layers = layers,
                    Blocks = blocks,
                    Entities = entities
                };

                ed.WriteMessage("\nWriting JSON...");
                JsonHelper.WriteToFile(result, outputPath);

                tr.Commit();

                ed.WriteMessage("\nEntities: {0}", summary.TotalEntity);
                ed.WriteMessage("\nLayers:   {0}", layers.Count);
                ed.WriteMessage("\nBlocks:   {0}", blocks.Count);
                if (errorCount > 0)
                {
                    ed.WriteMessage("\nWarnings: {0} (export continued)", errorCount);
                }
            }

            return outputPath;
        }
    }
}
