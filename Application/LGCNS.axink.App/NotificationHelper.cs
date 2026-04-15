using LGCNS.axink.App.Controls;
using LGCNS.axink.App.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LGCNS.axink.App
{
    public static class NotificationHelper
    {
        // 현재 화면에 떠 있는 알림창 리스트
        private static List<NotificationWindow> _openNotifications = new List<NotificationWindow>();

        // 이 값들을 조절하여 간격과 여백을 설정하세요
        private const double MarginRight = 5;  // 오른쪽 끝에서 5px 띄움 (0이면 딱 붙음)
        private const double MarginTop = 25;   // 맨 위 알림과 창 상단의 간격
        private const double Spacing = 5;      // 알림과 알림 사이의 수직 간격 (값을 줄임)

        public static void Show(Window owner, string message)
        {

            var notifyWin = new NotificationWindow();
            notifyWin.Owner = owner;
            notifyWin.MessageText.Text = message;

            // 1. [핵심] 시작 위치를 MainWindow의 오른쪽 경계선 '밖'으로 설정
            // 창이 나타날 때 왼쪽으로 밀려 들어오는 느낌을 줍니다.
            notifyWin.Left = owner.Left + owner.Width;
            notifyWin.Top = owner.Top + MarginTop;

            notifyWin.Loaded += (s, e) =>
            {
                if (!_openNotifications.Contains(notifyWin))
                {
                    _openNotifications.Add(notifyWin);
                }
                RearrangeNotifications(owner);
            };

            notifyWin.Closed += (s, e) =>
            {
                _openNotifications.Remove(notifyWin);
                RearrangeNotifications(owner);
            };

            notifyWin.Show();

            // 3초 후 자동 닫기
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                if (notifyWin.IsLoaded) await notifyWin.CloseWithAnimation();
            };
            timer.Start();
        }

        public static void RearrangeNotifications(Window owner)
        {
            double currentTop = owner.Top + MarginTop;
            double maxBottom = owner.Top + owner.Height - 20;

            foreach (var win in _openNotifications.ToList())
            {
                double winWidth = win.ActualWidth > 0 ? win.ActualWidth : 300;
                double winHeight = win.ActualHeight > 0 ? win.ActualHeight : 60;

                // 목표 위치: MainWindow 오른쪽 경계선에서 MarginRight만큼 안쪽으로
                double targetLeft = owner.Left + owner.Width - winWidth - MarginRight;
                double targetTop = currentTop;

                // 창 하단을 벗어나면 숨김
                if (targetTop + winHeight > maxBottom)
                {
                    win.Visibility = Visibility.Collapsed;
                    continue;
                }
                win.Visibility = Visibility.Visible;

                // 애니메이션 설정 (From을 명시하지 않으면 현재 위치에서 목표 위치로 부드럽게 이동)
                DoubleAnimation leftAnim = new DoubleAnimation(targetLeft, TimeSpan.FromSeconds(0.4))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                DoubleAnimation topAnim = new DoubleAnimation(targetTop, TimeSpan.FromSeconds(0.4))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                win.BeginAnimation(Window.LeftProperty, leftAnim);
                win.BeginAnimation(Window.TopProperty, topAnim);

                currentTop += winHeight + Spacing;
            }
        }
    }
}