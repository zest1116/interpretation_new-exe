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
using System.Windows.Shapes;

namespace LGCNS.axink.App.Windows
{
    /// <summary>
    /// NotificationWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NotificationWindow : Window
    {
        public NotificationWindow()
        {
            InitializeComponent();
        }

        public async Task CloseWithAnimation()
        {
            // 닫기 애니메이션 정의
            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));

            // 애니메이션 실행
            this.BeginAnimation(Window.OpacityProperty, fadeOut);

            // 애니메이션 지속 시간만큼 대기
            await Task.Delay(200);

            // [수정] 바로 Close하지 않고 숨긴 뒤 처리
            this.Hide();
            this.Owner = null; // 소유권 관계를 먼저 끊어 포커스 꼬임 방지
            this.Close();
        }
    }
}
