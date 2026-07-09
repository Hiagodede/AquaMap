using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Graphics;

namespace AquaMap.Controls
{
    public class CustomPin : Pin
    {
        public static readonly BindableProperty PinColorProperty =
            BindableProperty.Create(nameof(PinColor), typeof(Color), typeof(CustomPin), Colors.Red);

        public Color PinColor
        {
            get => (Color)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public static readonly BindableProperty ReservoirIdProperty =
            BindableProperty.Create(nameof(ReservoirId), typeof(int), typeof(CustomPin), 0);

        public int ReservoirId
        {
            get => (int)GetValue(ReservoirIdProperty);
            set => SetValue(ReservoirIdProperty, value);
        }
    }
}
