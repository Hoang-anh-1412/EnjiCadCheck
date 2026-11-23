using Gssoft.Gscad.Geometry;

namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Phân biệt hình chiếu khi đọc neo đầu/cuối thân từ cache.
    /// </summary>
    public enum TankShellViewKind
    {
        /// <summary>Hình chiếu đứng (立面図).</summary>
        Elevation = 0,

        /// <summary>Hình chiếu bằng (平面図).</summary>
        Plan = 1
    }

    /// <summary>
    /// Neo hình học một hình chiếu thân trụ: gốc view, tâm hai đầu, góc mặt trong/ngoài.
    /// Tọa độ ModelSpace lúc TAOTHANTANK vẽ (mm).
    /// </summary>
    public sealed class TankShellViewGeometry
    {
        public TankShellViewKind ViewKind { get; set; }

        /// <summary>Y của đường tâm view (originY lúc vẽ).</summary>
        public double OriginY { get; set; }

        /// <summary>Gốc view = đầu trái trên đường tâm (0, OriginY).</summary>
        public Point3d Origin { get; set; }

        /// <summary>Đầu trái thân trên đường tâm.</summary>
        public Point3d LeftCenter { get; set; }

        /// <summary>Đầu phải thân trên đường tâm.</summary>
        public Point3d RightCenter { get; set; }

        /// <summary>Giữa thân trên đường tâm.</summary>
        public Point3d MidCenter { get; set; }

        public Point3d LeftInnerTop { get; set; }
        public Point3d LeftInnerBottom { get; set; }
        public Point3d RightInnerTop { get; set; }
        public Point3d RightInnerBottom { get; set; }

        public Point3d LeftOuterTop { get; set; }
        public Point3d LeftOuterBottom { get; set; }
        public Point3d RightOuterTop { get; set; }
        public Point3d RightOuterBottom { get; set; }

        /// <summary>Sao chép sâu để lệnh khác sửa không đụng cache.</summary>
        public TankShellViewGeometry Clone()
        {
            return new TankShellViewGeometry
            {
                ViewKind = ViewKind,
                OriginY = OriginY,
                Origin = Origin,
                LeftCenter = LeftCenter,
                RightCenter = RightCenter,
                MidCenter = MidCenter,
                LeftInnerTop = LeftInnerTop,
                LeftInnerBottom = LeftInnerBottom,
                RightInnerTop = RightInnerTop,
                RightInnerBottom = RightInnerBottom,
                LeftOuterTop = LeftOuterTop,
                LeftOuterBottom = LeftOuterBottom,
                RightOuterTop = RightOuterTop,
                RightOuterBottom = RightOuterBottom
            };
        }

        /// <summary>
        /// Dựng neo từ L / R / Ro và originY — khớp công thức DrawShellOnly.
        /// </summary>
        public static TankShellViewGeometry Build(
            TankShellViewKind viewKind,
            double shellLength,
            double radius,
            double outerRadius,
            double originY)
        {
            var L = shellLength;
            var R = radius;
            var Ro = outerRadius;

            return new TankShellViewGeometry
            {
                ViewKind = viewKind,
                OriginY = originY,
                Origin = new Point3d(0.0, originY, 0.0),
                LeftCenter = new Point3d(0.0, originY, 0.0),
                RightCenter = new Point3d(L, originY, 0.0),
                MidCenter = new Point3d(L * 0.5, originY, 0.0),
                LeftInnerTop = new Point3d(0.0, originY + R, 0.0),
                LeftInnerBottom = new Point3d(0.0, originY - R, 0.0),
                RightInnerTop = new Point3d(L, originY + R, 0.0),
                RightInnerBottom = new Point3d(L, originY - R, 0.0),
                LeftOuterTop = new Point3d(0.0, originY + Ro, 0.0),
                LeftOuterBottom = new Point3d(0.0, originY - Ro, 0.0),
                RightOuterTop = new Point3d(L, originY + Ro, 0.0),
                RightOuterBottom = new Point3d(L, originY - Ro, 0.0)
            };
        }
    }

    /// <summary>
    /// Snapshot layout thân trụ sau TAOTHANTANK: kích thước + neo từng hình chiếu.
    /// </summary>
    public sealed class TankShellLayout
    {
        public double ShellLength { get; set; }
        public double Radius { get; set; }
        public double OuterRadius { get; set; }
        public double ShellThickness { get; set; }

        public TankShellViewGeometry Elevation { get; set; }
        public TankShellViewGeometry Plan { get; set; }

        /// <summary>
        /// Tạo layout khớp vị trí view trong DrawShellOnly (đứng Y=0, bằng phía dưới).
        /// </summary>
        public static TankShellLayout FromShellDraw(
            double shellLength,
            double radius,
            double outerRadius,
            double thickness,
            double elevationOriginY,
            double planOriginY)
        {
            return new TankShellLayout
            {
                ShellLength = shellLength,
                Radius = radius,
                OuterRadius = outerRadius,
                ShellThickness = thickness,
                Elevation = TankShellViewGeometry.Build(
                    TankShellViewKind.Elevation,
                    shellLength,
                    radius,
                    outerRadius,
                    elevationOriginY),
                Plan = TankShellViewGeometry.Build(
                    TankShellViewKind.Plan,
                    shellLength,
                    radius,
                    outerRadius,
                    planOriginY)
            };
        }

        public TankShellLayout Clone()
        {
            return new TankShellLayout
            {
                ShellLength = ShellLength,
                Radius = Radius,
                OuterRadius = OuterRadius,
                ShellThickness = ShellThickness,
                Elevation = Elevation != null ? Elevation.Clone() : null,
                Plan = Plan != null ? Plan.Clone() : null
            };
        }

        /// <summary>Lấy geometry theo loại hình chiếu; null nếu chưa có.</summary>
        public TankShellViewGeometry GetView(TankShellViewKind viewKind)
        {
            if (viewKind == TankShellViewKind.Plan)
            {
                return Plan;
            }

            return Elevation;
        }
    }
}
