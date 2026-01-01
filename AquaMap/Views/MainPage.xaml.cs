using System;
using System.Windows.Input;
using AquaMap.ViewModels;

namespace AquaMap.Views;

public partial class MainPage : ContentPage
{
    // Construtor sem parâmetros necessário para o XAML/DataTemplate do Shell
    public MainPage() : this(new MainViewModel())
    {
    }

    // O construtor que recebe a ViewModel
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Manipulador para o evento Clicked definido no XAML.
    private void OnCounterClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not null)
        {
            var incrementProp = BindingContext.GetType().GetProperty("IncrementCommand");
            if (incrementProp?.GetValue(BindingContext) is ICommand cmd && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                return;
            }

            var counterProp = BindingContext.GetType().GetProperty("Counter");
            if (counterProp is not null && counterProp.PropertyType == typeof(int) && counterProp.CanWrite)
            {
                var current = (int?)counterProp.GetValue(BindingContext) ?? 0;
                counterProp.SetValue(BindingContext, current + 1);
                return;
            }
        }

        if (sender is Button btn)
        {
            if (int.TryParse(btn.Text, out var value))
            {
                btn.Text = (value + 1).ToString();
            }
            else
            {
                btn.Text = "1";
            }
        }
    }
}