using System;
using Gssoft.Gscad.Colors;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using EnjiCadInspector.Helpers;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.Engine
{
    /// <summary>
    /// Hole 1 — concentric circles on front elevation (立面図).
    /// </summary>
    public static class Hole1Drawer
    {
        /// <summary>
        /// Draws Hole 1 on elevation. Center X = shell end − offset from right head;
        /// center Y = top of outer shell (originY + outerRadius).
        /// </summary>
        public static void DrawElevation(
            BlockTableRecord ms,
            Transaction tr,
            Hole1Params p,
            double shellLength,
            double originY,
            double outerShellRadius,
            Color color = null)
        {
            if (ms == null || tr == null || p == null)
            {
                throw new ArgumentNullException();
            }

            var err = p.Validate();
            if (err != null)
            {
                throw new InvalidOperationException(err);
            }

            if (shellLength <= p.OffsetFromRightHead)
            {
                return;
            }

            DrawingStyle.Ensure(ms.Database, tr);

            var center = new Point3d(
                shellLength - p.OffsetFromRightHead,
                originY + outerShellRadius,
                0);

            var outerR = p.OuterRingDiameter * 0.5;
            CadDraw.AppendCircle(ms, tr, center, outerR, LineRole.Visible, color);
            CadDraw.AppendCircle(ms, tr, center, outerR - p.RingOffset, LineRole.Visible, color);
            CadDraw.AppendCircle(
                ms, tr, center, p.HiddenRingDiameter * 0.5, LineRole.Hidden, color);
            CadDraw.AppendCircle(
                ms, tr, center, p.InnerRingDiameter57 * 0.5, LineRole.Visible, color);
            CadDraw.AppendCircle(
                ms, tr, center, p.InnerRingDiameter105 * 0.5, LineRole.Visible, color);
        }
    }
}
