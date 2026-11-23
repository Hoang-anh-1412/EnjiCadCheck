namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Tham số lệnh PART_CHOM: chiều dày chỏm và chiều cao SF.
    /// </summary>
    public sealed class PartChomParams
    {
        /// <summary>Chiều dày chỏm (mm).</summary>
        public double Thickness { get; set; }

        /// <summary>Chiều cao SF (mm).</summary>
        public double SfHeight { get; set; }

        /// <summary>Giá trị mặc định khi mở form lần đầu.</summary>
        public static PartChomParams CreateDefaults()
        {
            return new PartChomParams
            {
                Thickness = 6.0,
                SfHeight = 20.0
            };
        }

        /// <summary>Kiểm tra số dương trước khi insert template.</summary>
        public string Validate()
        {
            if (Thickness <= 0.0)
            {
                return "Chiều dày chỏm phải lớn hơn 0.";
            }

            if (SfHeight <= 0.0)
            {
                return "Chiều cao SF phải lớn hơn 0.";
            }

            return null;
        }
    }
}
