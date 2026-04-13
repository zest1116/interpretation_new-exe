using LGCNS.axink.App.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LGCNS.axink.App
{
    public static class NotificationHelper
    {
        private static ItemsControl _container;

        public static void Init(ItemsControl container)
        {
            _container = container;
        }

        public static void Show(string title, string message, int durationMs = 3000)
        {
            if (_container == null)
                throw new InvalidOperationException("NotificationHelper.Init()을 먼저 호출하세요.");

            _container.Dispatcher.Invoke(() =>
            {
                ShowInternal(title, message, durationMs);
            });
        }

        private static void ShowInternal(string title, string message, int durationMs)
        {
            var item = new NotificationItem(title, message);

            item.RemoveRequested += n =>
            {
                _container.Items.Remove(n);

                if (_container.Items.Count == 0)
                    _container.Visibility = Visibility.Collapsed;
            };

            // 새 알림을 위에 표시
            _container.Items.Insert(0, item);
            _container.Visibility = Visibility.Visible;
            _container.UpdateLayout();

            _container.Dispatcher.BeginInvoke(new Action(() =>
            {
                item.AnimateIn();
            }), DispatcherPriority.Loaded);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                item.AnimateOut();
            };

            timer.Start();
        }
    }
}