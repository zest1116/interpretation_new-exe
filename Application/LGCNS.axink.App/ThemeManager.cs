using System;
using System.Windows;

namespace LGCNS.axink.App;

public enum AppTheme { Light, Dark }

public static class ThemeManager
{
    // MergedDictionaries 로드 순서:
    //   [0] = Colors.Light.xaml 또는 Colors.Dark.xaml  ← 여기만 교체
    //   [1] = Fonts.xaml
    //   [2] = Styles.xaml
    private const int ColorDictionaryIndex = 0;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>
    /// 런타임 테마 전환. MergedDictionaries[0]을 교체하면
    /// DynamicResource로 바인딩된 모든 브러시가 즉시 갱신됩니다.
    /// </summary>
    public static void Apply(AppTheme theme)
    {
        var uri = theme switch
        {
            AppTheme.Dark => new Uri("pack://application:,,,/Themes/ThemeDark.xaml"),
            _ => new Uri("pack://application:,,,/Themes/ThemeLight.xaml"),
        };

        var dict = new ResourceDictionary { Source = uri };
        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        if (mergedDicts.Count > ColorDictionaryIndex)
            mergedDicts[ColorDictionaryIndex] = dict;
        else
            mergedDicts.Insert(ColorDictionaryIndex, dict);

        Current = theme;
    }

    /// <summary>Light ↔ Dark 토글</summary>
    public static AppTheme Toggle()
    {
        var next = Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Apply(next);
        return next;
    }
}