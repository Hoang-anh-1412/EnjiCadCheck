using System;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.Runtime;
using EnjiCadInspector.Engine;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;
using EnjiCadInspector.UI;
using Application = Gssoft.Gscad.ApplicationServices.Application;

[assembly: CommandClass(typeof(EnjiCadInspector.Commands.PartChomCommand))]

namespace EnjiCadInspector.Commands
{
    /// <summary>
    /// Lệnh PART_CHOM: nhập dày chỏm + chiều cao SF,
    /// rồi tự insert 4 chỏm (trái/phải × đứng/bằng) theo neo TAOTHANTANK.
    /// </summary>
    public class PartChomCommand
    {
        [CommandMethod("PART_CHOM")]
        public void PartChom()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n========== PART_CHOM ==========");

            try
            {
                // ----- Bước A: nhập chiều dày chỏm + chiều cao SF -----
                PartChomParams parameters;
                using (var form = new PartChomParamForm(PartChomParams.CreateDefaults()))
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

                // ----- Bước B: neo hình học từ TAOTHANTANK (bắt buộc để tự đặt vị trí) -----
                TankShellLayout layout;
                if (!TankShellGeometryCache.TryGet(doc, out layout) || layout == null)
                {
                    ed.WriteMessage(
                        "\nStatus:   FAIL - Chưa có neo thân trong cache Document.");
                    ed.WriteMessage(
                        "\nGợi ý:   Chạy TAOTHANTANK trước để lưu đầu/cuối theo hình chiếu.");
                    ed.WriteMessage("\n================================\n");
                    return;
                }

                // Đường kính: ưu tiên TankBodyParamsCache; không có thì suy từ layout.
                double diameter;
                TankBodyParams body;
                if (TankBodyParamsCache.TryGet(doc, out body) && body != null)
                {
                    diameter = body.Diameter;
                }
                else
                {
                    diameter = layout.Radius * 2.0;
                }

                // Scale = Đường kính (mm) / 1000 — khớp hệ quy chiếu template.
                var scale = PartChomInserter.ComputeScaleFromDiameter(diameter);

                ed.WriteMessage(
                    "\nCache D:  {0:0.###} mm  →  scale = {1:0.###}",
                    diameter,
                    scale);
                ed.WriteMessage(
                    "\nTemplate: {0}",
                    PartChomInserter.ResolveTemplatePath());
                ed.WriteMessage(
                    "\nAuto:     4 chỏm (Đứng L/R + Bằng L/R) theo neo cache");

                // ----- Bước C: khóa document, insert theo layout, commit -----
                int insertedCount;
                using (doc.LockDocument())
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var ids = PartChomInserter.InsertAtShellLayout(
                        doc.Database,
                        tr,
                        layout,
                        scale,
                        parameters);
                    insertedCount = ids != null ? ids.Count : 0;
                    tr.Commit();
                }

                ed.WriteMessage("\nStatus:   OK");
                ed.WriteMessage(
                    "\nChỏm:    t={0:0.###}  SF={1:0.###}  scale={2:0.###}  count={3}",
                    parameters.Thickness,
                    parameters.SfHeight,
                    scale,
                    insertedCount);
                WriteAnchorSummary(ed, layout);
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

        /// <summary>In neo Left/Right đã dùng để insert (đứng + bằng).</summary>
        private static void WriteAnchorSummary(
            Gssoft.Gscad.EditorInput.Editor ed,
            TankShellLayout layout)
        {
            if (ed == null || layout == null)
            {
                return;
            }

            WriteViewSummary(ed, "Đứng", layout.Elevation);
            WriteViewSummary(ed, "Bằng", layout.Plan);
        }

        private static void WriteViewSummary(
            Gssoft.Gscad.EditorInput.Editor ed,
            string label,
            TankShellViewGeometry view)
        {
            if (view == null)
            {
                return;
            }

            ed.WriteMessage(
                "\n{0}:     L=({1:0.###},{2:0.###})  R=({3:0.###},{4:0.###})",
                label,
                view.LeftInnerBottom.X,
                view.LeftInnerBottom.Y,
                view.RightInnerTop.X,
                view.RightInnerTop.Y);
        }
    }
}
