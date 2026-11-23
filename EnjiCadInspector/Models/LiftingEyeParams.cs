namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Param lỗ treo — theo đúng bước dựng tay (không suy ngược phức tạp).
    /// </summary>
    public sealed class LiftingEyeParams
    {
        /// <summary>Bước 1 — chiều cao cạnh trái.</summary>
        public double LeftHeight { get; set; }

        /// <summary>Bước 2 — chiều dài đáy.</summary>
        public double BaseWidth { get; set; }

        /// <summary>Bước 3 — chiều cao cạnh phải.</summary>
        public double RightHeight { get; set; }

        /// <summary>Bước 4 — chiều dài cạnh chéo trái.</summary>
        public double DiagonalLength { get; set; }

        /// <summary>Bước 4 — góc nghiêng cạnh chéo (độ, từ +X).</summary>
        public double DiagonalAngleDeg { get; set; }

        /// <summary>Bước 5 — bán kính fillet giữa cạnh chéo và cạnh phải.</summary>
        public double FilletRadius { get; set; }

        /// <summary>Bước 6 — cao từ trung điểm đáy lên điểm tâm thứ nhất.</summary>
        public double HoleCenterHeight { get; set; }

        /// <summary>Bước 6 — khoảng cách giữa 2 tâm lỗ.</summary>
        public double HoleCenterSpan { get; set; }

        /// <summary>Bước 6 — góc nghiêng từ tâm 1 → tâm 2 (độ).</summary>
        public double HoleAngleDeg { get; set; }

        /// <summary>Bước 6 — bán kính 2 đường tròn lỗ.</summary>
        public double HoleRadius { get; set; }

        /// <summary>Hình chiếu đứng — chiều rộng bản mã.</summary>
        public double ElevationHeight { get; set; }

        /// <summary>Section A-A — chiều cao phần khuất (nét đứt) xuyên vào thân (mm).
        /// H trái / H phải đo từ đáy thật; phần lộ ra trên mặt ngoài = H − giá trị này.</summary>
        public double SectionHiddenDepth { get; set; }

        public static LiftingEyeParams CreateDefaults()
        {
            return new LiftingEyeParams
            {
                LeftHeight = 60.0,
                BaseWidth = 160.0,
                RightHeight = 100.0,
                DiagonalLength = 100.0,
                DiagonalAngleDeg = 47.0,
                FilletRadius = 60.0,
                HoleCenterHeight = 68.5086,
                HoleCenterSpan = 30.0,
                HoleAngleDeg = 45.0,
                HoleRadius = 30.0,
                ElevationHeight = 19.0,
                SectionHiddenDepth = 50.0
            };
        }

        public string Validate()
        {
            if (LeftHeight <= 0.0 || BaseWidth <= 0.0 || RightHeight <= 0.0)
            {
                return "H trái / đáy / H phải phải lớn hơn 0.";
            }

            if (DiagonalLength <= 0.0 || DiagonalAngleDeg <= 0.0 || DiagonalAngleDeg >= 90.0)
            {
                return "Cạnh chéo không hợp lệ.";
            }

            if (FilletRadius <= 0.0)
            {
                return "R fillet phải lớn hơn 0.";
            }

            if (HoleCenterHeight <= 0.0 || HoleCenterSpan <= 0.0 || HoleRadius <= 0.0)
            {
                return "Thông số lỗ không hợp lệ.";
            }

            if (ElevationHeight <= 0.0)
            {
                return "Chiều rộng hình chiếu đứng phải lớn hơn 0.";
            }

            if (SectionHiddenDepth <= 0.0)
            {
                return "Chiều cao phần khuất mặt cắt phải lớn hơn 0.";
            }

            if (LeftHeight <= SectionHiddenDepth || RightHeight <= SectionHiddenDepth)
            {
                return "H trái / H phải phải lớn hơn chiều cao phần khuất mặt cắt.";
            }

            return null;
        }
    }
}
