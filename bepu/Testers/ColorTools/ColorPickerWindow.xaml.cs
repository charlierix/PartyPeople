using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Game.Bepu.Testers.ColorTools
{
    public partial class ColorPickerWindow : Window
    {
        #region Declaration Section

        private bool _isInitialized = false;
        private bool _isProgrammaticallyChanging = false;

        private readonly Effect _errorEffect;

        #endregion

        #region Constructor

        public ColorPickerWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C03333"),
                Opacity = .75,
                BlurRadius = 12,
                ShadowDepth = 0,
            };

            _isInitialized = true;

            txtHex_TextChanged(this, null);
        }

        #endregion

        #region Event Listeners

        private void txtHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isProgrammaticallyChanging)     // this needs to return outside the finally
                return;

            try
            {
                ClearEffects();

                Color color = UtilityWPF.ColorFromHex(txtHex.Text);

                ShowColor(color);

                _isProgrammaticallyChanging = true;

                SetARGB(color);
                SetARGBp(color);
                SetAHSV(color);
                SetAHSVp(color);

                txtHexDisplay.Text = UtilityWPF.ColorToHex(color, true, false);
            }
            catch (Exception)
            {
                txtHex.Effect = _errorEffect;
                ShowColor(null);
            }
            finally
            {
                _isProgrammaticallyChanging = false;
            }
        }
        private void txtRGB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isProgrammaticallyChanging)     // this needs to return outside the finally
                return;

            try
            {
                ClearEffects();

                var bytes = new[] { txtRGB_A, txtRGB_R, txtRGB_G, txtRGB_B }.
                    Select(o =>
                    {
                        if (byte.TryParse(o.Text, out byte result))
                        {
                            return (true, result);
                        }
                        else
                        {
                            o.Effect = _errorEffect;
                            return (false, (byte)0);
                        }
                    }).
                    ToArray();

                if (bytes.Any(o => !o.Item1))
                {
                    ShowColor(null);
                    return;
                }

                Color color = Color.FromArgb(bytes[0].Item2, bytes[1].Item2, bytes[2].Item2, bytes[3].Item2);

                ShowColor(color);

                _isProgrammaticallyChanging = true;

                SetHex(color);
                SetARGBp(color);
                SetAHSV(color);
                SetAHSVp(color);
            }
            catch (Exception)
            {
                ShowColor(null);
            }
            finally
            {
                _isProgrammaticallyChanging = false;
            }
        }
        private void txtRGBp_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isProgrammaticallyChanging)     // this needs to return outside the finally
                return;

            try
            {
                ClearEffects();

                var bytes = new[] { txtRGBp_A, txtRGBp_R, txtRGBp_G, txtRGBp_B }.
                    Select(o =>
                    {
                        if (double.TryParse(o.Text, out double result) && result >= 0d && result <= 1d)
                        {
                            return (true, (result * 255d).ToByte_Round());
                        }
                        else
                        {
                            o.Effect = _errorEffect;
                            return (false, (byte)0);
                        }
                    }).
                    ToArray();

                if (bytes.Any(o => !o.Item1))
                {
                    ShowColor(null);
                    return;
                }

                Color color = Color.FromArgb(bytes[0].Item2, bytes[1].Item2, bytes[2].Item2, bytes[3].Item2);

                ShowColor(color);

                _isProgrammaticallyChanging = true;

                SetHex(color);
                SetARGB(color);
                SetAHSV(color);
                SetAHSVp(color);
            }
            catch (Exception)
            {
                ShowColor(null);
            }
            finally
            {
                _isProgrammaticallyChanging = false;
            }
        }
        private void txtHSV_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isProgrammaticallyChanging)     // this needs to return outside the finally
                return;

            try
            {
                ClearEffects();

                var values = new[] { (txtHSV_A, 100), (txtHSV_H, 360), (txtHSV_S, 100), (txtHSV_V, 100) }.
                    Select(o =>
                    {
                        if (double.TryParse(o.Item1.Text, out double result) && result >= 0d && result <= o.Item2)
                        {
                            return (true, result);
                        }
                        else
                        {
                            o.Item1.Effect = _errorEffect;
                            return (false, 0d);
                        }
                    }).
                    ToArray();

                if (values.Any(o => !o.Item1))
                {
                    ShowColor(null);
                    return;
                }

                Color color = new ColorHSV((values[0].Item2 * 2.55).ToByte_Round(), values[1].Item2, values[2].Item2, values[3].Item2).ToRGB();

                ShowColor(color);

                _isProgrammaticallyChanging = true;

                SetHex(color);
                SetARGB(color);
                SetARGBp(color);
                SetAHSVp(color);
            }
            catch (Exception)
            {
                ShowColor(null);
            }
            finally
            {
                _isProgrammaticallyChanging = false;
            }
        }
        private void txtHSVp_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isProgrammaticallyChanging)     // this needs to return outside the finally
                return;

            try
            {
                ClearEffects();

                var percents = new[] { txtHSVp_A, txtHSVp_H, txtHSVp_S, txtHSVp_V }.
                    Select(o =>
                    {
                        if (double.TryParse(o.Text, out double result) && result >= 0d && result <= 1d)
                        {
                            return (true, result);
                        }
                        else
                        {
                            o.Effect = _errorEffect;
                            return (false, 0d);
                        }
                    }).
                    ToArray();

                if (percents.Any(o => !o.Item1))
                {
                    ShowColor(null);
                    return;
                }

                Color color = new ColorHSV((percents[0].Item2 * 255).ToByte_Round(), percents[1].Item2 * 360, percents[2].Item2 * 100, percents[3].Item2 * 100).ToRGB();

                ShowColor(color);

                _isProgrammaticallyChanging = true;

                SetHex(color);
                SetARGB(color);
                SetARGBp(color);
                SetAHSV(color);
            }
            catch (Exception)
            {
                ShowColor(null);
            }
            finally
            {
                _isProgrammaticallyChanging = false;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            //TODO: Up and down arrows should increment the value a bit
        }

        #endregion

        #region Private Methods

        private void ClearEffects()
        {
            txtHex.Effect = null;

            txtRGB_A.Effect = null;
            txtRGB_R.Effect = null;
            txtRGB_G.Effect = null;
            txtRGB_B.Effect = null;

            txtRGBp_A.Effect = null;
            txtRGBp_R.Effect = null;
            txtRGBp_G.Effect = null;
            txtRGBp_B.Effect = null;

            txtHSV_A.Effect = null;
            txtHSV_H.Effect = null;
            txtHSV_S.Effect = null;
            txtHSV_V.Effect = null;

            txtHSVp_A.Effect = null;
            txtHSVp_H.Effect = null;
            txtHSVp_S.Effect = null;
            txtHSVp_V.Effect = null;
        }

        private void ShowColor(Color? color)
        {
            if (color == null)
            {
                pnlColorSample.Background = Brushes.Transparent;
            }
            else
            {
                pnlColorSample.Background = new SolidColorBrush(color.Value);
            }
        }

        private void SetHex(Color color)
        {
            txtHex.Text = UtilityWPF.ColorToHex(color, true, false);
            txtHexDisplay.Text = txtHex.Text;
        }
        private void SetARGB(Color color)
        {
            txtRGB_A.Text = color.A.ToString();
            txtRGB_R.Text = color.R.ToString();
            txtRGB_G.Text = color.G.ToString();
            txtRGB_B.Text = color.B.ToString();
        }
        private void SetARGBp(Color color)
        {
            txtRGBp_A.Text = Math.Round(color.A / 255d, 2).ToString();
            txtRGBp_R.Text = Math.Round(color.R / 255d, 2).ToString();
            txtRGBp_G.Text = Math.Round(color.G / 255d, 2).ToString();
            txtRGBp_B.Text = Math.Round(color.B / 255d, 2).ToString();
        }
        private void SetAHSV(Color color)
        {
            ColorHSV hsv = color.ToHSV();

            txtHSV_A.Text = Math.Round(hsv.A / 2.55).ToString();
            txtHSV_H.Text = Math.Round(hsv.H).ToString();
            txtHSV_S.Text = Math.Round(hsv.S).ToString();
            txtHSV_V.Text = Math.Round(hsv.V).ToString();
        }
        private void SetAHSVp(Color color)
        {
            ColorHSV hsv = color.ToHSV();

            txtHSVp_A.Text = Math.Round(hsv.A / 255d, 2).ToString();
            txtHSVp_H.Text = Math.Round(hsv.H / 360d, 2).ToString();
            txtHSVp_S.Text = Math.Round(hsv.S / 100d, 2).ToString();
            txtHSVp_V.Text = Math.Round(hsv.V / 100d, 2).ToString();
        }

        #endregion
    }
}
