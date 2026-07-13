using System;
using System.Reflection;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadCheck.Commands))]

namespace EnjiCadCheck
{
    public class Commands
    {
        /// <summary>
        /// NETLOAD this DLL, then type CHECKENJI.
        /// Verifies managed API + prints host info.
        /// If build fails on namespace, switch using to GrxCAD.* (older enjiCAD).
        /// </summary>
        [CommandMethod("CHECKENJI")]
        public void CheckEnji()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                MessageBox.Show("No active document.", "CHECKENJI");
                return;
            }

            var ed = doc.Editor;
            var asm = Assembly.GetExecutingAssembly();

            ed.WriteMessage("\n========== CHECKENJI ==========");
            ed.WriteMessage("\nPlugin:     {0}", asm.Location);
            ed.WriteMessage("\n.NET:       {0}", Environment.Version);
            ed.WriteMessage("\nDocument:   {0}", doc.Name);
            ed.WriteMessage("\nDatabase:   {0}", doc.Database.Filename);

            TryReportHostAssembly(ed, "GcMgd");
            TryReportHostAssembly(ed, "GcDbMgd");
            TryReportHostAssembly(ed, "GcCoreMgd");

            ed.WriteMessage("\nStatus:     OK - enjiCAD .NET API is working");
            ed.WriteMessage("\n================================\n");
        }

        private static void TryReportHostAssembly(Editor ed, string name)
        {
            try
            {
                var loaded = Array.Find(
                    AppDomain.CurrentDomain.GetAssemblies(),
                    a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

                if (loaded == null)
                {
                    ed.WriteMessage("\n{0}:   (not loaded in AppDomain yet)", name);
                    return;
                }

                var loc = string.IsNullOrEmpty(loaded.Location) ? "(dynamic)" : loaded.Location;
                ed.WriteMessage("\n{0}:   {1}", name, loc);
                ed.WriteMessage("\n           v{0}", loaded.GetName().Version);
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n{0}:   ERROR {1}", name, ex.Message);
            }
        }
    }
}
