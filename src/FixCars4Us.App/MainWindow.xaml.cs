using System.Windows;
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
}
