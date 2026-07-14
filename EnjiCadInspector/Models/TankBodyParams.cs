namespace EnjiCadInspector.Models
{
    /// <summary>
    /// MVP body parameters for TAOMOI_TANK (Thân bồn).
    /// </summary>
    public sealed class TankBodyParams
    {
        /// <summary>
        /// Straight cylindrical shell length (mm). Corresponds to mid body dim (e.g. 3600).
        /// </summary>
        public double ShellLength { get; set; }

        /// <summary>
        /// Body radius (mm) used for elevation / section silhouette.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Head / end-cap depth along axis (mm). Default from FAF08S06 proportion 400/750.
        /// </summary>
        public double HeadDepth { get; set; }

        /// <summary>
        /// Shell thickness (mm) for section A-A inner profile. Default 6.
        /// </summary>
        public double ShellThickness { get; set; }

        public static TankBodyParams CreateDefaults()
        {
            return new TankBodyParams
            {
                ShellLength = 3600.0,
                Radius = 750.0,
                HeadDepth = 400.0,
                ShellThickness = 6.0
            };
        }

        /// <summary>
        /// Applies derived defaults when optional fields are not on the form yet.
        /// </summary>
        public void NormalizeDerived()
        {
            if (HeadDepth <= 0.0 && Radius > 0.0)
            {
                HeadDepth = Radius * (400.0 / 750.0);
            }

            if (ShellThickness <= 0.0)
            {
                ShellThickness = 6.0;
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
