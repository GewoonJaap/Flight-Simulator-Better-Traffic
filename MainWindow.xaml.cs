using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Simvars
{
    internal interface IBaseSimConnectWrapper
    {
        int GetUserSimConnectWinEvent();

        void ReceiveSimConnectMessage();

        void SetWindowHandle(IntPtr _hWnd);

        void Disconnect();
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new SimvarsViewModel();

            InitializeComponent();
        }

        protected HwndSource GetHWinSource()
        {
            return PresentationSource.FromVisual(this) as HwndSource;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            GetHWinSource().AddHook(WndProc);
            if (DataContext is IBaseSimConnectWrapper oBaseSimConnectWrapper)
                oBaseSimConnectWrapper.SetWindowHandle(GetHWinSource().Handle);
        }

        private IntPtr WndProc(IntPtr hWnd, int iMsg, IntPtr hWParam, IntPtr hLParam, ref bool bHandled)
        {
            if (DataContext is IBaseSimConnectWrapper oBaseSimConnectWrapper)
                try
                {
                    if (iMsg == oBaseSimConnectWrapper.GetUserSimConnectWinEvent())
                        oBaseSimConnectWrapper.ReceiveSimConnectMessage();
                }
                catch
                {
                    oBaseSimConnectWrapper.Disconnect();
                }

            return IntPtr.Zero;
        }

        private void lv_Simvars_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
