using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class FindDistinctStrings : Window
    {
        #region Declaration Section

        private readonly DropShadowEffect _errorEffect;

        #endregion

        #region Constructor

        public FindDistinctStrings()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };
        }

        #endregion

        #region Event Listeners

        private void txtFindWhat_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                RefreshScan();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void chkCaseSensitive_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshScan();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void chkRegex_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshScan();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtSource_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                RefreshScan();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void RefreshScan()
        {
            string[] source = txtSource.Text.
                Replace("\r\n", "\n").
                Split('\n', StringSplitOptions.RemoveEmptyEntries);

            string[] results = null;
            try
            {
                results = GetMatches(source, txtFindWhat.Text, chkCaseSensitive.IsChecked.Value, chkRegex.IsChecked.Value);
            }
            catch (Exception)
            {
                txtFindWhat.Effect = _errorEffect;
                txtResults.Text = "";
                return;
            }

            txtFindWhat.Effect = null;

            var distinct = results.
                    Distinct().
                    OrderBy(o => o);

            txtResults.Text = string.Join("\r\n", distinct);
        }

        private static string[] GetMatches(string[] source, string find, bool caseSensitive, bool isRegex)
        {
            if (string.IsNullOrEmpty(find))
                return source;

            if (isRegex)
                return GetMatches_Regex(source, find, caseSensitive);
            else
                return GetMatches_Regex(source, Regex.Escape(find), caseSensitive);
        }

        private static string[] GetMatches_Regex(string[] source, string find, bool caseSensitive)
        {
            var retVal = new List<string>();

            RegexOptions options = caseSensitive ?
                RegexOptions.None :
                RegexOptions.IgnoreCase;

            foreach (string line in source)
            {
                foreach (Match match in Regex.Matches(line, find, options))
                {
                    retVal.Add(match.Value);
                }
            }

            return retVal.ToArray();
        }

        #endregion
    }
}
