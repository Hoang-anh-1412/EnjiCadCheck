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
            ed.WriteMessage("\n================================\n");
        }


    }
}