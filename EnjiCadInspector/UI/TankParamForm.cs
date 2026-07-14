using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EnjiCadInspector.Models;

namespace EnjiCadInspector.UI
{
    /// <summary>
    /// Modal input dialog for TAOMOI_TANK — section Thân bồn.
    /// </summary>
    public sealed class TankParamForm : Form
    {
        private readonly TextBox _txtShellLength;
        private readonly TextBox _txtRadius;
        private readonly Label _lblError;

        public TankBodyParams Result { get; private set; }

        public TankParamForm(TankBodyParams defaults)
        {
            if (defaults == null)
            {
                defaults = TankBodyParams.CreateDefaults();
            }

            Text = "TAOMOI_TANK — Tham số bồn";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 230);
            Font = new Font("Segoe UI", 9F);
            ShowInTaskbar = false;

            var group = new GroupBox
            {
                Text = "Thân bồn",
                Location = new Point(16, 12),
                Size = new Size(388, 120)
            };

            var lblLength = new Label
            {
                Text = "Chiều dài thân (mm):",
                Location = new Point(16, 32),
                AutoSize = true
            };

            _txtShellLength = new TextBox
            {
                Location = new Point(180, 28),
                Width = 180,
                Text = FormatNumber(defaults.ShellLength)
            };

            var lblRadius = new Label
            {
                Text = "Bán kính (mm):",
                Location = new Point(16, 68),
                AutoSize = true
            };

            _txtRadius = new TextBox
            {
                Location = new Point(180, 64),
                Width = 180,
                Text = FormatNumber(defaults.Radius)
            };

            group.Controls.Add(lblLength);
            group.Controls.Add(_txtShellLength);
            group.Controls.Add(lblRadius);
            group.Controls.Add(_txtRadius);

            _lblError = new Label
            {
                ForeColor = Color.Firebrick,
                Location = new Point(16, 140),
                Size = new Size(388, 36),
                Text = string.Empty
            };

            var btnOk = new Button
            {
                Text = "Vẽ",
                DialogResult = DialogResult.None,
                Location = new Point(220, 186),
                Width = 88,
                Height = 28
            };
            btnOk.Click += HandleOkClick;

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location = new Point(316, 186),
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

        private void HandleOkClick(object sender, EventArgs e)
        {
            _lblError.Text = string.Empty;

            double shellLength;
            double radius;
            if (!TryParsePositive(_txtShellLength.Text, out shellLength))
            {
                _lblError.Text = "Chiều dài thân không hợp lệ.";
                _txtShellLength.Focus();
                return;
            }

            if (!TryParsePositive(_txtRadius.Text, out radius))
            {
                _lblError.Text = "Bán kính không hợp lệ.";
                _txtRadius.Focus();
                return;
            }

            var p = new TankBodyParams
            {
                ShellLength = shellLength,
                Radius = radius
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
