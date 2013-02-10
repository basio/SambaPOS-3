using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Samba.Modules.AccountModule
{
    /// <summary>
    /// Interaction logic for LocationSelectorView.xaml
    /// </summary>

    [Export]
    public partial class AccountButtonSelectorView : UserControl
    {
        [ImportingConstructor]
        public AccountButtonSelectorView(AccountButtonSelectorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DiagramCanvas.EditingMode == InkCanvasEditingMode.None)
            {
                brd.BorderBrush = Brushes.Red;
                miDesignMode.IsChecked = true;
                DiagramCanvas.EditingMode = InkCanvasEditingMode.Select;
                ((AccountButtonSelectorViewModel)DataContext).LoadTrackableLocations();
            }
            else
            {
                brd.BorderBrush = Brushes.Silver;
                miDesignMode.IsChecked = false;
                DiagramCanvas.EditingMode = InkCanvasEditingMode.None;
                ((AccountButtonSelectorViewModel)DataContext).SaveTrackableLocations();
            }
        }
    }
}
