using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.UI
{
    /// <summary>
    /// Form nhập tham số TAOTHANTANK: chiều dài thân, đường kính, bề dày.
    /// </summary>
    public sealed class TankShellParamForm : Form
    {
        private readonly TextBox _txtShellLength;
        private readonly TextBox _txtDiameter;
        private readonly TextBox _txtThickness;
        private readonly Label _lblError;

        /// <summary>Tham số hợp lệ sau khi bấm Vẽ; null nếu hủy.</summary>
        public TankBodyParams Result { get; private set; }

        public TankShellParamForm(TankBodyParams defaults)
        {
            if (defaults == null)
            {
                defaults = TankBodyParams.CreateDefaults();
            }

            Text = "TAOTHANTANK — Thân bồn";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            // Form rộng hơn để label tiếng Việt không đè lên ô nhập.
            ClientSize = new Size(460, 270);
            Font = new Font("Segoe UI", 9F);
            ShowInTaskbar = false;

            // Cột label cố định; ô nhập bắt đầu sau label để tránh chồng chữ.
            const int labelX = 16;
            const int labelWidth = 170;
            const int inputX = 200;
            const int inputWidth = 220;

            var group = new GroupBox
            {
                Text = "Thân bồn",
                Location = new Point(16, 12),
                Size = new Size(428, 160)
            };

            var lblLength = new Label
            {
                Text = "Chiều dài thân (mm):",
                Location = new Point(labelX, 32),
                Size = new Size(labelWidth, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtShellLength = new TextBox
            {
                Location = new Point(inputX, 28),
                Width = inputWidth,
                Text = FormatNumber(defaults.ShellLength)
            };

            var lblDiameter = new Label
            {
                Text = "Đường kính (mm):",
                Location = new Point(labelX, 68),
                Size = new Size(labelWidth, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtDiameter = new TextBox
            {
                Location = new Point(inputX, 64),
                Width = inputWidth,
                Text = FormatNumber(defaults.Diameter)
            };

            var lblThickness = new Label
            {
                Text = "Bề dày (mm):",
                Location = new Point(labelX, 104),
                Size = new Size(labelWidth, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtThickness = new TextBox
            {
                Location = new Point(inputX, 100),
                Width = inputWidth,
                Text = FormatNumber(defaults.ShellThickness)
            };

            group.Controls.Add(lblLength);
            group.Controls.Add(_txtShellLength);
            group.Controls.Add(lblDiameter);
            group.Controls.Add(_txtDiameter);
            group.Controls.Add(lblThickness);
            group.Controls.Add(_txtThickness);

            _lblError = new Label
            {
                ForeColor = Color.Firebrick,
                Location = new Point(16, 180),
                Size = new Size(428, 36),
                Text = string.Empty
            };

            var btnOk = new Button
            {
                Text = "Vẽ",
                DialogResult = DialogResult.None,
                Location = new Point(260, 226),
                Width = 88,
                Height = 28
            };
            btnOk.Click += HandleOkClick;

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location = new Point(356, 226),
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
        /// Xử lý nút Vẽ: parse 3 ô nhập → kiểm tra → gán Result rồi đóng form.
        /// </summary>
        private void HandleOkClick(object sender, EventArgs e)
        {
            _lblError.Text = string.Empty;

            double shellLength;
            double diameter;
            double thickness;

            // Parse từng trường; lỗi thì focus lại ô tương ứng.
            if (!TryParsePositive(_txtShellLength.Text, out shellLength))
            {
                _lblError.Text = "Chiều dài thân không hợp lệ.";
                _txtShellLength.Focus();
                return;
            }

            if (!TryParsePositive(_txtDiameter.Text, out diameter))
            {
                _lblError.Text = "Đường kính không hợp lệ.";
                _txtDiameter.Focus();
                return;
            }

            if (!TryParsePositive(_txtThickness.Text, out thickness))
            {
                _lblError.Text = "Bề dày không hợp lệ.";
                _txtThickness.Focus();
                return;
            }

            // Form nhập đường kính; model nội bộ dùng bán kính.
            var radius = diameter * 0.5;
            if (thickness >= radius)
            {
                _lblError.Text = "Bề dày phải nhỏ hơn bán kính (đường kính / 2).";
                _txtThickness.Focus();
                return;
            }

            var p = new TankBodyParams
            {
                ShellLength = shellLength,
                Radius = radius,
                ShellThickness = thickness
            };
            p.NormalizeDerived();

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

            if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
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
