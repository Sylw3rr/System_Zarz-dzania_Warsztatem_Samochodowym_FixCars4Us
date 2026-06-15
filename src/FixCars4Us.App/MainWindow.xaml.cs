using System.Windows;
using System.Windows.Controls;
using FixCars4Us.Core.ViewModels;

namespace FixCars4Us.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Tworzy bazę (SQLite, EnsureCreated), wczytuje dane startowe i podpina ViewModele.
        DataContext = new MainViewModel();
    }

    /// <summary>
    /// Odświeża dane aktywnej zakładki przy przełączeniu — ViewModele dzielą jeden
    /// WorkshopContext, więc zmiany dokonane w innym module (np. nowy wpis historii
    /// serwisowej, rezerwacja terminu, zmiana stanu magazynu) muszą zostać dociągnięte.
    /// </summary>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.OriginalSource is not TabControl tabs) return;

        switch (tabs.SelectedIndex)
        {
            case 0: vm.Customers.Refresh(); break;
            case 2: vm.Inventory.Refresh(); break;
            case 3: vm.Appointments.Refresh(); break;
            case 4: vm.Repairs.Refresh(); break;
        }
    }
}
