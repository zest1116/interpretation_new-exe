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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LGCNS.axink.App.Controls
{
    /// <summary>
    /// NotificationItem.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NotificationItem : UserControl
    {
        public event Action<NotificationItem> RemoveRequested;

        public NotificationItem(string title, string message)
        {
            InitializeComponent();
            //TitleText.Text = title;
            MessageText.Text = message;
        }

        public void AnimateIn()
        {
            Animate(320, 0, 300, null);
        }

        public void AnimateOut()
        {
            Animate(0, 320, 250, () => RemoveRequested?.Invoke(this));
        }

        private void Animate(double from, double to, int durationMs, Action onComplete)
        {
            SlideTransform.X = from;
            int steps = 20;
            int interval = durationMs / steps;
            int step = 0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
            timer.Tick += (s, e) =>
            {
                step++;
                double t = EaseOut((double)step / steps);
                SlideTransform.X = from + (to - from) * t;

                if (step >= steps)
                {
                    SlideTransform.X = to;
                    timer.Stop();
                    onComplete?.Invoke();
                }
            };
            timer.Start();
        }

        private static double EaseOut(double t)
        {
            return 1 - (1 - t) * (1 - t);
        }
    }
}
