using System;
using System.Reflection;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using static check.Class2;
using Application = Gssoft.Gscad.ApplicationServices.Application;


[assembly: CommandClass(typeof(EnjiCadCheck.Commands))]

namespace EnjiCadCheck
{
    public class Commands
    {
        [CommandMethod("CHECKENJI")]
        public void CheckEnji()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            var asm = Assembly.GetExecutingAssembly();

            ed.WriteMessage("\n========== CHECKENJI ==========");
            ed.WriteMessage("\nPlugin:   {0}", asm.Location);
            ed.WriteMessage("\n.NET:     {0}", Environment.Version);
            ed.WriteMessage("\nDocument: {0}", doc.Name);
            ed.WriteMessage("\nDatabase: {0}", doc.Database.Filename);

            ReportAsm(ed, "GcMgd");
            ReportAsm(ed, "GcDbMgd");
            ReportAsm(ed, "GcCoreMgd");

            ed.WriteMessage("\nStatus:   OK - enjiCAD .NET API is working");
            ed.WriteMessage("\nTip:      CHECKENT / TANKINV / XDRAW / XTAG / XTAGS / XSIZE");
            ed.WriteMessage("\n================================\n");
        }

        [CommandMethod("CHECKENT")]
        public void CheckEntities()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== CHECKENT ==========");

            try
            {
                using (doc.LockDocument())
                {
                    EntityApiProbe.Run(doc.Database, ed);
                }

                ed.WriteMessage("\nStatus:   DONE - see OK / SKIP / FAIL above");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nStatus:   FAIL - {0}", ex.Message);
            }

            ed.WriteMessage("\n================================\n");
        }

        [CommandMethod("TANKINV")]
        public void TankInventory()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;

            try
            {
                string path;
                using (doc.LockDocument())
                {
                    path = DrawingInventory.Run(doc.Database, ed);
                }

                ed.WriteMessage("\nStatus:   Inventory written to:\n  {0}", path);
                ed.WriteMessage("\nNext:     F2 copy log, or send the .md file here\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n========== TANKINV ==========");
                ed.WriteMessage("\nStatus:   FAIL - {0}", ex.Message);
                ed.WriteMessage("\n================================\n");
            }
        }
    }
}