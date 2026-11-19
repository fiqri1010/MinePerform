using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MinePerform.PC.ViewModels;

namespace MinePerform.PC.Views;

public partial class DataTableView : UserControl
{
    public DataTableView()
    {
        InitializeComponent();
    }

    private async void OnImportExcelClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not DataTablePageViewModel vm) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pilih Data IP 2026.xlsx / DATA_INPUT.xlsx",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Excel Workbook") { Patterns = ["*.xlsx"] }
            ]
        });

        string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrEmpty(path))
        {
            vm.ImportExcelFile(path);
        }
    }
}
