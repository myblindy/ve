using Avalonia;
using Avalonia.Markup.Xaml;

namespace ve
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
