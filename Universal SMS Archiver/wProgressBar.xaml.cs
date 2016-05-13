using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Universal_SMS_Archiver
{
    /// <summary>
    /// Interaction logic for wProgressBar.xaml
    /// </summary>
    public partial class wProgressBar : Window
    {
        public wProgressBar()
        {
            InitializeComponent();
        }

        public void UpdateProgressBarProgressive(string Message, int Progress)
        {
            UpdateProgressBar(Message, Convert.ToInt32(theProgressBar.Value) + Progress);
        }

        public void UpdateProgressBar(string Message, int Progress)
        {
            if (Progress > 100)
                Progress = 100;
            else if (Progress < 0)
                Progress = 0;

            lbl.Content = Message;
            theProgressBar.Value = Progress;
            theBusyIndicator.IsBusyIndicatorShowing = (Progress < 100);
        }
    }
}
