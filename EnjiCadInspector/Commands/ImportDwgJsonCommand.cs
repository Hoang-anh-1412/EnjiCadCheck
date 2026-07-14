using System;
using System.IO;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
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
    /// Session flag is required for DocumentManager.Add / Open.
    /// </summary>
    public class ImportDwgJsonCommand
    {
        /// <summary>
        /// Entry point: pick JSON → new document → import layers + ModelSpace entities.
        /// </summary>
        [CommandMethod("IMPORT_DWG_JSON", CommandFlags.Session)]
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
                string savedDwgPath;
                var stats = BuildNewDrawingFile(payload, jsonPath, ed, out savedDwgPath);

                ed.WriteMessage("\nOpening:  {0}", savedDwgPath);
                var newDoc = Application.DocumentManager.Open(savedDwgPath, false);
                if (newDoc == null)
                {
                    throw new InvalidOperationException("DocumentManager.Open returned null for " + savedDwgPath);
                }

                Application.DocumentManager.MdiActiveDocument = newDoc;
                var newEd = newDoc.Editor;

                try
                {
                    newDoc.SendStringToExecute("_.ZOOM _E ", true, false, false);
                }
                catch (System.Exception zoomEx)
                {
                    newEd.WriteMessage("\nZOOM E skipped: {0}", zoomEx.Message);
                }

                newEd.WriteMessage("\n========== IMPORT_DWG_JSON ==========");
                newEd.WriteMessage("\nStatus:   OK");
                newEd.WriteMessage("\nSource:   {0}", jsonPath);
                newEd.WriteMessage("\nDWG:     {0}", savedDwgPath);
                newEd.WriteMessage("\nLayers+:  {0}", stats.LayersCreated);
                newEd.WriteMessage("\nCreated:  {0}", stats.Created);
                newEd.WriteMessage("\nSkipped:  {0}", stats.Skipped);
                newEd.WriteMessage("\nErrors:   {0}", stats.Errors);
                newEd.WriteMessage("\n=====================================\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nStatus:   FAIL - {0}", ex.Message);
                if (ex.InnerException != null)
                {
                    ed.WriteMessage("\nInner:    {0}", ex.InnerException.Message);
                }

                ed.WriteMessage("\n=====================================\n");
            }
        }

        /// <summary>
        /// Builds entities into a side Database, SaveAs to a new DWG beside the JSON.
        /// Avoids DocumentManager.Add("") which returns eNotApplicable on CADian.
        /// </summary>
        private static ImportStats BuildNewDrawingFile(
            ExportResult payload,
            string jsonPath,
            Editor logEd,
            out string savedDwgPath)
        {
            savedDwgPath = ResolveOutputDwgPath(jsonPath);
            var warnCount = 0;
            Action<string> logWarn = message =>
            {
                warnCount++;
                if (warnCount <= 40)
                {
                    logEd.WriteMessage("\n  WARN: {0}", message);
                }
            };

            using (var db = new Database(true, true))
            {
                ImportStats stats;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    logEd.WriteMessage("\nEnsuring layers...");
                    var layersCreated = LayerImporter.EnsureLayers(
                        db,
                        tr,
                        payload.Layers,
                        message => logWarn("layer: " + message));

                    logEd.WriteMessage("\nImporting entities...");
                    stats = EntityImporter.Import(
                        db,
                        tr,
                        payload.Entities,
                        (handle, message) => logWarn("[" + handle + "] " + message));

                    stats.LayersCreated = layersCreated;
                    tr.Commit();
                }

                logEd.WriteMessage("\nSaving:   {0}", savedDwgPath);
                SaveDatabase(db, savedDwgPath);

                if (warnCount > 40)
                {
                    logEd.WriteMessage("\n  ... {0} total warnings (truncated)", warnCount);
                }

                return stats;
            }
        }

        private static void SaveDatabase(Database db, string path)
        {
            // Try common AutoCAD / GstarCAD SaveAs overloads.
            try
            {
                db.SaveAs(path, DwgVersion.Current);
                return;
            }
            catch (System.Exception)
            {
                // Fall through.
            }

            try
            {
                db.SaveAs(path, true, DwgVersion.Current, null);
                return;
            }
            catch (System.Exception ex)
            {
                throw new InvalidOperationException("SaveAs failed for " + path + ": " + ex.Message, ex);
            }
        }

        private static string ResolveOutputDwgPath(string jsonPath)
        {
            string directory;
            string baseName;
            try
            {
                directory = Path.GetDirectoryName(jsonPath);
                baseName = Path.GetFileNameWithoutExtension(jsonPath);
            }
            catch (System.Exception)
            {
                directory = null;
                baseName = null;
            }

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "drawing";
            }

            var candidate = Path.Combine(directory, baseName + "-imported.dwg");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            return Path.Combine(
                directory,
                baseName + "-imported-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dwg");
        }

        private static string PromptJsonPath(Editor ed)
        {
            var opts = new PromptOpenFileOptions("Select drawing JSON to import")
            {
                Filter = "JSON (*.json)|*.json",
                PreferCommandLine = false
            };

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
    }
}
