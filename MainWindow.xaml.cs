using System.Windows;
using System.Windows.Controls;
using SysMarkWPF.Views;

namespace SysMarkWPF
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            MainFrame.Navigate(new MainPage());
        }

        public void NavigateTo(Page page)
        {
            MainFrame.Navigate(page);
        }
    }
}