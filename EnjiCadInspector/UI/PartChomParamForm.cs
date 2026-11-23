using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.UI
{
    /// <summary>
    /// Form nhập tham số PART_CHOM: chiều dày chỏm + chiều cao SF.
    /// </summary>
    public sealed class PartChomParamForm : Form
    {
        private readonly TextBox _txtThickness;
        private readonly TextBox _txtSfHeight;
        private readonly Label _lblError;

        /// <summary>Tham số hợp lệ sau khi bấm OK; null nếu hủy.</summary>
        public PartChomParams Result { get; private set; }

        public PartChomParamForm(PartChomParams defaults)
        {
            if (defaults == null)
            {
                defaults = PartChomParams.CreateDefaults();
            }

            Text = "PART_CHOM — Chỏm";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 230);
            Font = new Font("Segoe UI", 9F);
            ShowInTaskbar = false;

            // Cột label cố định để chữ tiếng Việt không đè lên ô nhập.
            const int labelX = 16;
            const int labelWidth = 170;
            const int inputX = 200;
            const int inputWidth = 220;

            var group = new GroupBox
            {
                Text = "Tham số chỏm",
                Location = new Point(16, 12),
                Size = new Size(428, 120)
            };

            var lblThickness = new Label
            {
                Text = "Chiều dày chỏm (mm):",
                Location = new Point(labelX, 32),
                Size = new Size(labelWidth, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtThickness = new TextBox
            {
                Location = new Point(inputX, 28),
                Width = inputWidth,
                Text = FormatNumber(defaults.Thickness)
            };

            var lblSfHeight = new Label
            {
                Text = "Chiều cao SF (mm):",
                Location = new Point(labelX, 68),
                Size = new Size(labelWidth, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtSfHeight = new TextBox
            {
                Location = new Point(inputX, 64),
                Width = inputWidth,
                Text = FormatNumber(defaults.SfHeight)
            };

            group.Controls.Add(lblThickness);
            group.Controls.Add(_txtThickness);
            group.Controls.Add(lblSfHeight);
            group.Controls.Add(_txtSfHeight);

            _lblError = new Label
            {
                ForeColor = Color.Firebrick,
                Location = new Point(16, 140),
                Size = new Size(428, 36),
                Text = string.Empty
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.None,
                Location = new Point(260, 186),
                Width = 88,
                Height = 28
            };
            btnOk.Click += HandleOkClick;

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location = new Point(356, 186),
                Width = 88,
                Height = 28
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(group);
            Controls.Add(_lblError);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }

        /// <summary>
        /// Xử lý nút OK: parse 2 ô nhập → kiểm tra → gán Result rồi đóng form.
        /// </summary>
        private void HandleOkClick(object sender, EventArgs e)
        {
            _lblError.Text = string.Empty;

            double thickness;
            double sfHeight;

            if (!TryParsePositive(_txtThickness.Text, out thickness))
            {
                _lblError.Text = "Chiều dày chỏm không hợp lệ.";
                _txtThickness.Focus();
                return;
            }

            if (!TryParsePositive(_txtSfHeight.Text, out sfHeight))
            {
                _lblError.Text = "Chiều cao SF không hợp lệ.";
                _txtSfHeight.Focus();
                return;
            }

            var p = new PartChomParams
            {
                Thickness = thickness,
                SfHeight = sfHeight
            };

            var err = p.Validate();
            if (!string.IsNullOrEmpty(err))
            {
                _lblError.Text = err;
                return;
            }

            Result = p;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Parse số dương; chấp nhận dấu phẩy hoặc chấm thập phân.</summary>
        private static bool TryParsePositive(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!double.TryParse(
                    text.Trim().Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return false;
            }

            return value > 0.0;
        }

        /// <summary>Hiển thị số nguyên gọn nếu không có phần thập phân.</summary>
        private static string FormatNumber(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 1e-9)
            {
                return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
