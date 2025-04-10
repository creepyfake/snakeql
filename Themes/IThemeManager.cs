using Avalonia;

namespace snakeql.Themes;

public interface IThemeManager
{
    void Initialize(Application application);

    void Switch(int index);
}
