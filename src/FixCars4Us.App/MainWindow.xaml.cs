using System.Windows;
using System.Windows.Controls;
using FixCars4Us.Core.ViewModels;

namespace FixCars4Us.App;

// Code-behind okna głównego, zgodnie z MVVM trzymany w minimum.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    // Zakładki dzielą jeden kontekst bazy, więc trzeba ręcznie odświeżyć dane po zmianie.
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.OriginalSource is not TabControl tabs) return;

        switch (tabs.SelectedIndex)
        {
            case 0: vm.Customers.Refresh();    break;
            case 2: vm.Inventory.Refresh();    break;
            case 3: vm.Appointments.Refresh(); break;
            case 4: vm.Repairs.Refresh();      break;
        }
    }
}
