using System;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using EnjiCadInspector.Engine;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;
using EnjiCadInspector.UI;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadInspector.Commands.CreateNewTankCommand))]

namespace EnjiCadInspector.Commands
{
    /// <summary>
    /// Lệnh TAOMOI_TANK: form nhập tham số → hình chiếu đứng + mặt cắt A-A.
    /// </summary>
    public class CreateNewTankCommand
    {
        [CommandMethod("TAOMOI_TANK")]
        public void CreateNewTank()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== TAOMOI_TANK ==========");

            try
            {
                // Prefill từ cache document nếu TAOTHANTANK (hoặc lần chạy trước) đã lưu.
                TankBodyParams parameters;
                using (var form = new TankParamForm(TankBodyParamsCache.GetOrDefaults(doc)))
                {
                    var result = Application.ShowModalDialog(form);
                    if (result != DialogResult.OK || form.Result == null)
                    {
                        ed.WriteMessage("\nStatus:   CANCELLED");
                        ed.WriteMessage("\n================================\n");
                        return;
                    }

                    parameters = form.Result;
                }

                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    TankViewDrawer.Draw(doc.Database, tr, parameters);
                    tr.Commit();
                }

                // Chia sẻ params thân đã OK cho các lệnh khác trên document này.
                TankBodyParamsCache.Set(doc, parameters);

                try
                {
                    doc.SendStringToExecute("_.ZOOM _E ", true, false, false);
                }
                catch (System.Exception zoomEx)
                {
                    ed.WriteMessage("\nZOOM E skipped: {0}", zoomEx.Message);
                }

                ed.WriteMessage("\nStatus:   OK");
                ed.WriteMessage("\nThân:     L={0:0.###}  R={1:0.###}  Head={2:0.###}  t={3:0.###}",
                    parameters.ShellLength,
                    parameters.Radius,
                    parameters.HeadDepth,
                    parameters.ShellThickness);
                ed.WriteMessage("\nViews:    Hình chiếu đứng + A-A 断面図");
                ed.WriteMessage("\n================================\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nStatus:   FAIL - {0}", ex.Message);
                if (ex.InnerException != null)
                {
                    ed.WriteMessage("\nInner:    {0}", ex.InnerException.Message);
                }

                ed.WriteMessage("\n================================\n");
            }
        }

    }
}
