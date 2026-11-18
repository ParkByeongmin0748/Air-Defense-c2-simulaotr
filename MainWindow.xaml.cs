
using System;
using System.Windows;

namespace AirDefenseC2
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void EngageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // 현재 시간 가져오기
            string time = DateTime.Now.ToString("HH:mm:ss");

            // 교전 로그 추가
            LogText.Text +=
                $"\n[{time}] T-007 교전 명령 실행";

            // 메시지 창 표시
            MessageBox.Show(
                "T-007 표적에 대한 모의 교전 명령이 실행되었습니다.",
                "Air Defense C2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
