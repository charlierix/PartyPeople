using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Game.Math_WPF.WPF.Viewers
{
    public partial class DebugMarkdownWindow : Window
    {
        public DebugMarkdownWindow()
        {
            InitializeComponent();

            DataContext = this;

            Loaded += DebugMarkdownWindow_Loaded;
        }

        private void DebugMarkdownWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RenderDocument();
        }

        public string MarkdownText
        {
            get { return (string)GetValue(MarkdownTextProperty); }
            set { SetValue(MarkdownTextProperty, value); }
        }
        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.Register("MarkdownText", typeof(string), typeof(DebugMarkdownWindow),
                new PropertyMetadata("", OnMarkdownTextChanged));

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DebugMarkdownWindow window)
                window.RenderDocument();
        }

        private void RenderDocument()
        {
            if (docViewer == null)
                return;

            docViewer.Document = MarkdownToFlowDocument.Convert(MarkdownText ?? "");
        }

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is DebugTextWindow)
                        window.Close();

                    if (window is DebugMarkdownWindow)
                        window.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}