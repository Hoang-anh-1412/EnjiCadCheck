using System;
using System.IO;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Importers;
using EnjiCadInspector.Models;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadInspector.Commands.ImportDwgJsonCommand))]

namespace EnjiCadInspector.Commands
{
    /// <summary>
    /// CADian / enjiCAD command: IMPORT_DWG_JSON
    /// Creates a new drawing and recreates entities from drawing.json.
    /// </summary>
    public class ImportDwgJsonCommand
    {
        /// <summary>
        /// Entry point: pick JSON → new document → import layers + ModelSpace entities.
        /// </summary>
        [CommandMethod("IMPORT_DWG_JSON")]
        public void ImportDwgJson()
        {
            var sourceDoc = Application.DocumentManager.MdiActiveDocument;
            if (sourceDoc == null)
            {
                return;
            }

            var ed = sourceDoc.Editor;
            ed.WriteMessage("\n========== IMPORT_DWG_JSON ==========");

            try
            {
                var jsonPath = PromptJsonPath(ed);
                if (string.IsNullOrWhiteSpace(jsonPath))
                {
                    ed.WriteMessage("\nStatus:   CANCELLED");
                    ed.WriteMessage("\n=====================================\n");
                    return;
                }

                ed.WriteMessage("\nReading:  {0}", jsonPath);
                var payload = JsonHelper.Deserialize(jsonPath);

                ed.WriteMessage("\nCreating new drawing...");
                var newDoc = Application.DocumentManager.Add(string.Empty);
                if (newDoc == null)
                {
                    throw new InvalidOperationException("DocumentManager.Add returned null.");
                }

                var newEd = newDoc.Editor;
                ImportStats stats;

                using (newDoc.LockDocument())
                {
                    stats = RunImport(newDoc, payload, newEd);
                }

                try
                {
                    newEd.Command("_.ZOOM", "_E");
                }
                catch (System.Exception)
                {
                    try
                    {
                        newDoc.SendStringToExecute("_.ZOOM _E ", true, false, false);
                    }
                    catch (System.Exception zoomEx)
                    {
                        newEd.WriteMessage("\nZOOM E skipped: {0}", zoomEx.Message);
                    }
                }

                newEd.WriteMessage("\n========== IMPORT_DWG_JSON ==========");
                newEd.WriteMessage("\nStatus:   OK");
                newEd.WriteMessage("\nSource:   {0}", jsonPath);
                newEd.WriteMessage("\nLayers+:  {0}", stats.LayersCreated);
                newEd.WriteMessage("\nCreated:  {0}", stats.Created);
                newEd.WriteMessage("\nSkipped:  {0}", stats.Skipped);
                newEd.WriteMessage("\nErrors:   {0}", stats.Errors);
                newEd.WriteMessage("\nTip:      Re-export with EXPORT_DWG_JSON if Dimensions were skipped (legacy JSON).");
                newEd.WriteMessage("\n=====================================\n");

                // Also echo on the original editor if still valid.
                ed.WriteMessage("\nStatus:   OK — imported into new document");
                ed.WriteMessage("\nCreated:  {0}  Skipped: {1}  Errors: {2}", stats.Created, stats.Skipped, stats.Errors);
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

        private static string PromptJsonPath(Editor ed)
        {
            var opts = new PromptOpenFileOptions("Select drawing JSON to import")
            {
                Filter = "JSON (*.json)|*.json",
                PreferCommandLine = false
            };

            // Default to Desktop / project-friendly start if available.
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktop))
                {
                    opts.InitialDirectory = desktop;
                }
            }
            catch (System.Exception)
            {
                // Ignore initial directory failure.
            }

            var result = ed.GetFileNameForOpen(opts);
            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            return result.StringResult;
        }

        private static ImportStats RunImport(Document doc, ExportResult payload, Editor ed)
        {
            var db = doc.Database;
            var warnCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                ed.WriteMessage("\nEnsuring layers...");
                var layersCreated = LayerImporter.EnsureLayers(
                    db,
                    tr,
                    payload.Layers,
                    message =>
                    {
                        warnCount++;
                        if (warnCount <= 30)
                        {
                            ed.WriteMessage("\n  WARN layer: {0}", message);
                        }
                    });

                ed.WriteMessage("\nImporting entities...");
                var stats = EntityImporter.Import(
                    db,
                    tr,
                    payload.Entities,
                    (handle, message) =>
                    {
                        warnCount++;
                        if (warnCount <= 40)
                        {
                            ed.WriteMessage("\n  WARN [{0}]: {1}", handle, message);
                        }
                    });

                stats.LayersCreated = layersCreated;
                tr.Commit();

                if (warnCount > 40)
                {
                    ed.WriteMessage("\n  ... {0} total warnings (truncated)", warnCount);
                }

                return stats;
            }
        }
    }
}
