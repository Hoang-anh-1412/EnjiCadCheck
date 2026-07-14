namespace EnjiCadInspector.Models
{
    /// <summary>
    /// MVP body parameters for TAOMOI_TANK (Thân bồn).
    /// Head = torispherical: knuckle R150 + crown R1500 (FAF08S06).
    /// </summary>
    public sealed class TankBodyParams
    {
        public const double DefaultKnuckleRadius = 150.0;
        public const double DefaultCrownRadius = 1500.0;
        public const double DefaultShellThickness = 6.0;

        /// <summary>
        /// Straight cylindrical shell length (mm).
        /// </summary>
        public double ShellLength { get; set; }

        /// <summary>
        /// Body radius (mm) used for elevation / section silhouette.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Head depth along axis (mm). Derived from knuckle + crown when not set.
        /// </summary>
        public double HeadDepth { get; set; }

        /// <summary>
        /// Knuckle (corner) arc radius (mm).
        /// </summary>
        public double KnuckleRadius { get; set; }

        /// <summary>
        /// Crown (dome) arc radius (mm).
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
                ShellLength = 3600.0,
                Radius = 750.0,
                KnuckleRadius = DefaultKnuckleRadius,
                CrownRadius = DefaultCrownRadius,
                ShellThickness = DefaultShellThickness
            };
            p.NormalizeDerived();
            return p;
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
        /// Applies derived knuckle/crown/depth/thickness when optional fields are unset.
        /// </summary>
        public void NormalizeDerived()
        {
            if (KnuckleRadius <= 0.0)
            {
                KnuckleRadius = DefaultKnuckleRadius;
            }

            if (CrownRadius <= 0.0)
            {
                CrownRadius = DefaultCrownRadius;
            }

            if (ShellThickness <= 0.0)
            {
                ShellThickness = DefaultShellThickness;
            }

            if (Radius > KnuckleRadius && CrownRadius > Radius)
            {
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
