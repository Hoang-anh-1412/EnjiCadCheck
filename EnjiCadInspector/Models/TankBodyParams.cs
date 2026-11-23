namespace EnjiCadInspector.Models
{
    /// <summary>
    /// MVP body parameters for TAOMOI_TANK (Thân bồn).
    /// Head: crown = tank diameter (2R), knuckle = crown / 10.
    /// </summary>
    public sealed class TankBodyParams
    {
        public const double DefaultShellThickness = 6.0;

        /// <summary>
        /// Straight cylindrical shell length (mm).
        /// </summary>
        public double ShellLength { get; set; }

        /// <summary>
        /// Body radius (mm) — inner shell (内径/2). Outer = offset by ShellThickness.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Head depth along axis (mm). Derived from knuckle + crown.
        /// </summary>
        public double HeadDepth { get; set; }

        /// <summary>
        /// Knuckle (corner) arc radius (mm). Derived: CrownRadius / 10.
        /// </summary>
        public double KnuckleRadius { get; set; }

        /// <summary>
        /// Crown (dome) arc radius (mm). Derived: tank diameter = 2 * Radius.
        /// </summary>
        public double CrownRadius { get; set; }

        /// <summary>
        /// Shell thickness (mm) for section A-A inner profile.
        /// </summary>
        public double ShellThickness { get; set; }

        public static TankBodyParams CreateDefaults()
        {
            var p = new TankBodyParams
            {
                ShellLength = 4440.0,
                Radius = 750.0,
                ShellThickness = DefaultShellThickness
            };
            p.NormalizeDerived();
            return p;
        }

        /// <summary>
        /// Sao chép sâu: form/lệnh khác sửa được mà không đụng cache theo Document.
        /// </summary>
        public TankBodyParams Clone()
        {
            var copy = new TankBodyParams
            {
                ShellLength = ShellLength,
                Radius = Radius,
                HeadDepth = HeadDepth,
                KnuckleRadius = KnuckleRadius,
                CrownRadius = CrownRadius,
                ShellThickness = ShellThickness
            };
            return copy;
        }

        /// <summary>
        /// Dish depth for a torispherical head: H = Rc - sqrt((Rc-Rk)^2 - (R-Rk)^2).
        /// </summary>
        public static double ComputeHeadDepth(double radius, double knuckleRadius, double crownRadius)
        {
            var a = crownRadius - knuckleRadius;
            var b = radius - knuckleRadius;
            if (a <= 0.0 || b <= 0.0 || a <= b)
            {
                return 0.0;
            }

            return crownRadius - System.Math.Sqrt(a * a - b * b);
        }

        /// <summary>
        /// Crown = 2R (theo bán kính bồn / 内径), knuckle = crown / 10; then head depth.
        /// </summary>
        public void NormalizeDerived()
        {
            if (ShellThickness <= 0.0)
            {
                ShellThickness = DefaultShellThickness;
            }

            if (Radius > 0.0)
            {
                CrownRadius = Radius * 2.0;
                KnuckleRadius = CrownRadius / 10.0;
                HeadDepth = ComputeHeadDepth(Radius, KnuckleRadius, CrownRadius);
            }
        }

        public string Validate()
        {
            if (ShellLength <= 0.0)
            {
                return "Chiều dài thân phải lớn hơn 0.";
            }

            if (Radius <= 0.0)
            {
                return "Bán kính phải lớn hơn 0.";
            }

            NormalizeDerived();

            if (Radius <= KnuckleRadius)
            {
                return string.Format(
                    "Bán kính phải lớn hơn knuckle R={0:0}.",
                    KnuckleRadius);
            }

            if (CrownRadius <= Radius)
            {
                return string.Format(
                    "Bán kính phải nhỏ hơn crown R={0:0}.",
                    CrownRadius);
            }

            if (HeadDepth <= 0.0)
            {
                return "Chiều sâu đầu bồn (derived) không hợp lệ.";
            }

            if (ShellThickness <= 0.0 || ShellThickness >= Radius)
            {
                return "Chiều dày thân phải lớn hơn 0 và nhỏ hơn bán kính.";
            }

            if (ShellThickness >= KnuckleRadius)
            {
                return string.Format(
                    "Chiều dày thân phải nhỏ hơn knuckle R={0:0}.",
                    KnuckleRadius);
            }

            return null;
        }

        public double OverallLength
        {
            get { return HeadDepth + ShellLength + HeadDepth; }
        }

        public double Diameter
        {
            get { return Radius * 2.0; }
        }
    }
}
