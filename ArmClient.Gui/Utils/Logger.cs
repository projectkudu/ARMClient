using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArmGuiClient.Utils
{
    internal class Logger
    {
        public static readonly Brush InfoBrush = Brushes.LightGreen;
        public static readonly Brush WarnBrush = Brushes.Yellow;
        public static readonly Brush ErrorBrush = Brushes.Red;

        private static RichTextBox _outputRTB;
        private static FlowDocument _flowDoc;

        public static void Init(RichTextBox textBox)
        {
            _outputRTB = textBox;
            _flowDoc = _outputRTB.Document;
            Clear();
        }

        public static void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _outputRTB.Document.Blocks.Clear();
                _outputRTB.ScrollToEnd();
            });
        }

        public static void WriteLn(string content, Brush background)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var para = new Paragraph(new Run(content));
                para.Background = background;
                _outputRTB.Document.Blocks.Add(para);
                _outputRTB.ScrollToEnd();
            });
        }

        public static void InfoLn(string format, params object[] args)
        {
            WriteLn(string.Format(format, args), InfoBrush);
        }

        public static void WarnLn(string format, params object[] args)
        {
            WriteLn(string.Format(format, args), WarnBrush);
        }

        public static void ErrorLn(string format, params object[] args)
        {
            WriteLn(string.Format(format, args), ErrorBrush);
        }
    }
}
