using System;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Runtime;
using EnjiCadInspector.Engine;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;
using EnjiCadInspector.UI;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadInspector.Commands.CreateTankShellCommand))]

namespace EnjiCadInspector.Commands
{
    /// <summary>
    /// Lệnh TAOTHANTANK: nhận chiều dài / đường kính / bề dày,
    /// rồi chỉ vẽ thân trụ + đường tâm + kích thước (đứng + bằng).
    /// </summary>
    public class CreateTankShellCommand
    {
        [CommandMethod("TAOTHANTANK")]
        public void CreateTankShell()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== TAOTHANTANK ==========");

            try
            {
                // Bước 1: mở form với params đã cache của document (hoặc mặc định).
                TankBodyParams parameters;
                using (var form = new TankShellParamForm(TankBodyParamsCache.GetOrDefaults(doc)))
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

                // Bước 2: khóa document, vẽ thân trong transaction, rồi commit.
                TankShellLayout layout;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    layout = TankViewDrawer.DrawShellOnly(doc.Database, tr, parameters);
                    tr.Commit();
                }

                // Giữ kích thước + neo đầu/cuối theo view để lệnh khác trên cùng bản vẽ dùng lại.
                TankBodyParamsCache.Set(doc, parameters);
                TankShellGeometryCache.Set(doc, layout);

                // Bước 3: zoom extents (bỏ qua nếu host không hỗ trợ).
                try
                {
                    doc.SendStringToExecute("_.ZOOM _E ", true, false, false);
                }
                catch (System.Exception zoomEx)
                {
                    ed.WriteMessage("\nZOOM E skipped: {0}", zoomEx.Message);
                }

                ed.WriteMessage("\nStatus:   OK");
                ed.WriteMessage("\nThân:     L={0:0.###}  D={1:0.###}  t={2:0.###}",
                    parameters.ShellLength,
                    parameters.Diameter,
                    parameters.ShellThickness);
                ed.WriteMessage("\nViews:    Hình chiếu đứng + hình chiếu bằng (+ kích thước)");
                WriteCachedAnchors(ed, layout);
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

        /// <summary>
        /// In tóm tắt neo đầu/cuối đã cache theo từng hình chiếu (tiện kiểm tra / lệnh sau).
        /// </summary>
        private static void WriteCachedAnchors(Editor ed, TankShellLayout layout)
        {
            if (ed == null || layout == null)
            {
                return;
            }

            WriteViewAnchors(ed, "Đứng", layout.Elevation);
            WriteViewAnchors(ed, "Bằng", layout.Plan);
        }

        private static void WriteViewAnchors(Editor ed, string label, TankShellViewGeometry view)
        {
            if (view == null)
            {
                return;
            }

            ed.WriteMessage(
                "\nCache {0}: L=({1:0.###},{2:0.###})  R=({3:0.###},{4:0.###})",
                label,
                view.LeftCenter.X,
                view.LeftCenter.Y,
                view.RightCenter.X,
                view.RightCenter.Y);
        }
    }
}
