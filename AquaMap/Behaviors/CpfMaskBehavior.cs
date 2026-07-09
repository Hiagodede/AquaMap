using Microsoft.Maui.Controls;
using System.Linq;

namespace AquaMap.Behaviors
{
    public class CpfMaskBehavior : Behavior<Entry>
    {
        private bool _isFormatting;

        protected override void OnAttachedTo(Entry bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.TextChanged += OnEntryTextChanged;
        }

        protected override void OnDetachingFrom(Entry bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.TextChanged -= OnEntryTextChanged;
        }

        private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isFormatting || sender is not Entry entry) return;

            _isFormatting = true;

            var oldText = e.OldTextValue ?? string.Empty;
            var newText = e.NewTextValue ?? string.Empty;

            // Se o usuário está deletando, permite deletar sem interferir recursivamente
            if (newText.Length < oldText.Length)
            {
                _isFormatting = false;
                return;
            }

            // Filtra apenas números
            var digitsOnly = new string(newText.Where(char.IsDigit).ToArray());

            if (digitsOnly.Length > 11)
            {
                digitsOnly = digitsOnly.Substring(0, 11);
            }

            string formatted = digitsOnly;
            if (digitsOnly.Length > 9)
            {
                formatted = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3, 3)}.{digitsOnly.Substring(6, 3)}-{digitsOnly.Substring(9)}";
            }
            else if (digitsOnly.Length > 6)
            {
                formatted = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3, 3)}.{digitsOnly.Substring(6)}";
            }
            else if (digitsOnly.Length > 3)
            {
                formatted = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3)}";
            }

            if (entry.Text != formatted)
            {
                entry.Dispatcher.Dispatch(() =>
                {
                    _isFormatting = true;
                    entry.Text = formatted;
                    entry.CursorPosition = formatted.Length;
                    _isFormatting = false;
                });
            }

            _isFormatting = false;
        }
    }
}
