namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Hole 1 — concentric rings on tank front elevation.
    /// </summary>
    public sealed class Hole1Params
    {
        /// <summary>Horizontal distance from right shell–head junction to hole center (mm).</summary>
        public double OffsetFromRightHead { get; set; }

        /// <summary>Outer visible ring diameter (mm).</summary>
        public double OuterRingDiameter { get; set; }

        /// <summary>Inward offset for the second solid ring (mm).</summary>
        public double RingOffset { get; set; }

        /// <summary>Hidden (dashed) ring diameter (mm).</summary>
        public double HiddenRingDiameter { get; set; }

        /// <summary>Inner solid ring diameter (mm).</summary>
        public double InnerRingDiameter57 { get; set; }

        /// <summary>Inner solid ring diameter (mm).</summary>
        public double InnerRingDiameter105 { get; set; }

        public static Hole1Params CreateDefaults()
        {
            return new Hole1Params
            {
                OffsetFromRightHead = 350.0,
                OuterRingDiameter = 235.0,
                RingOffset = 3.0,
                HiddenRingDiameter = 150.0,
                InnerRingDiameter57 = 57.0,
                InnerRingDiameter105 = 105.0
            };
        }

        public string Validate()
        {
            if (OffsetFromRightHead <= 0.0)
            {
                return "Khoảng cách từ đầu phải phải lớn hơn 0.";
            }

            if (OuterRingDiameter <= 0.0)
            {
                return "Đường kính vòng ngoài phải lớn hơn 0.";
            }

            if (RingOffset <= 0.0 || RingOffset * 2.0 >= OuterRingDiameter)
            {
                return "Offset vòng ngoài không hợp lệ.";
            }

            if (HiddenRingDiameter <= 0.0)
            {
                return "Đường kính vòng nét đứt phải lớn hơn 0.";
            }

            if (InnerRingDiameter57 <= 0.0 || InnerRingDiameter105 <= 0.0)
            {
                return "Đường kính vòng trong phải lớn hơn 0.";
            }

            return null;
        }
    }
}
