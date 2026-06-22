using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ScaleSwitcher.Views
{
    public partial class OsdWindow : Window
    {
        public OsdWindow(string message, double fontSize = 48, bool hideCursor = true)
        {
            InitializeComponent();
            MessageText.Text = message;
            MessageText.FontSize = fontSize;
            Cursor = hideCursor ? System.Windows.Input.Cursors.None : System.Windows.Input.Cursors.Arrow;
        }

        public void CloseWithFade()
        {
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3));
            anim.Completed += (s, e) => this.Close();
            this.BeginAnimation(Window.OpacityProperty, anim);
        }
    }
}
