﻿using HotLyric.Win32.Controls;
using HotLyric.Win32.Models;
using HotLyric.Win32.Utils;
using HotLyric.Win32.Views;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;

namespace HotLyric.Win32.ViewModels
{
    public class SettingsWindowViewModel : ObservableObject
    {
        //private const string IsTranslationVisibleSettingKey = "Settings_TranslationVisible";
        private const string KaraokeEnabledSettingKey = "Settings_KaraokeEnabled";
        private const string LyricHorizontalAlignmentSettingKey = "Settings_LyricHorizontalAlignment";
        private const string AlwaysShowBackgroundSettingKey = "Settings_AlwaysShowBackground";
        private const string ShowShadowSettingKey = "Settings_ShowShadow";
        private const string TextStrokeEnabledSettingKey = "Settings_TextStrokeEnabled";
        private const string LyricFontFamilySettingKey = "Settings_LyricFontFamily";
        private const string SecondRowSettingKey = "Settings_SecondRow";
        private const string SkipEmptyLyricLineSettingKey = "Settings_SkipEmptyLyricLine";
        private const string TextOpacityMaskSettingKey = "Settings_TextOpacityMask";
        private const string HideWhenFullScreenAppOpenSettingKey = "Settings_HideWhenFullScreenAppOpen";
        private const string WindowTransparentSettingKey = "Settings_WindowTransparent";
        private const string KeepWindowTransparentSettingKey = "Settings_KeepWindowTransparent";
        private const string LowFrameRateModeSettingKey = "Settings_LowFrameRateMode";
        private const string RenderSoftwareOnlySettingKey = "Settings_RenderSoftwareOnly";
        private const string LyricOpacitySettingKey = "Settings_LyricOpacity";
        private const string ShowLauncherWindowOnStartupSettingKey = "Settings_ShowLauncherWindowOnStartup";
        private const string HideOnPausedSettingKey = "Settings_HideOnPaused";
        private const string AutoResetWindowPosSettingsKey = "Settings_AutoResetWindowPos";
        private const string ReadMeAlreadyShowedOnStartUpSettingsKey = "Settings_ReadMeAlreadyShowedOnStartUp";

        public SettingsWindowViewModel()
        {
            keepWindowTransparent = LoadSetting(KeepWindowTransparentSettingKey, false);
            if (keepWindowTransparent)
            {
                windowTransparent = LoadSetting(WindowTransparentSettingKey, false);
            }

            var translationVisible = LoadSetting<bool?>("Settings_TranslationVisible", null);
            secondRowType = LoadSetting(SecondRowSettingKey, translationVisible == false ? SecondRowType.Collapsed : SecondRowType.TranslationOrNextLyric);
            if (secondRowType == SecondRowType.TranslationOnly) secondRowType = SecondRowType.TranslationOrNextLyric;

            karaokeEnabled = LoadSetting(KaraokeEnabledSettingKey, true);
            lyricHorizontalAlignment = LoadSetting(LyricHorizontalAlignmentSettingKey, HorizontalAlignment.Center);

            alwaysShowBackground = LoadSetting(AlwaysShowBackgroundSettingKey, false);
            showShadow = LoadSetting(ShowShadowSettingKey, true);
            textStrokeEnabled = LoadSetting(TextStrokeEnabledSettingKey, true);

            currentTheme = LyricThemeManager.CurrentThemeView ?? LyricThemeManager.LyricThemes[0];
            AllPresetThemes = LyricThemeManager.LyricThemes.ToArray();

            themeIsPresetVisible = currentTheme.Name != "customize";
            customizeTheme = currentTheme;

            allFontfamilies = Fonts.SystemFontFamilies
                .Select(c => new FontFamilyDisplayModel(c))
                .OrderBy(c => c.Order)
                .ToList();

            lyricFontFamilySource = LoadSetting(LyricFontFamilySettingKey, "");
            if (string.IsNullOrEmpty(lyricFontFamilySource) || allFontfamilies.All(c => c.Source != lyricFontFamilySource))
            {
                lyricFontFamilySource = "Global User Interface";
            }

            lyricFontFamily = allFontfamilies.FirstOrDefault(c => c.Source == lyricFontFamilySource);

            skipEmptyLyricLine = LoadSetting(SkipEmptyLyricLineSettingKey, true);
            textOpacityMask = LoadSetting(TextOpacityMaskSettingKey, !DeviceHelper.IsLowPerformanceDevice);
            hideWhenFullScreenAppOpen = LoadSetting(HideWhenFullScreenAppOpenSettingKey, true);
            lowFrameRateMode = LoadSetting(LowFrameRateModeSettingKey, DeviceHelper.IsLowPerformanceDevice);

            renderSoftwareOnly = LoadSetting(RenderSoftwareOnlySettingKey, false);
            if (renderSoftwareOnly)
            {
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            }

            lyricOpacity = Math.Clamp(LoadSetting(LyricOpacitySettingKey, 1d), 0.1d, 1d);

            showLauncherWindowOnStartup = LoadSetting(ShowLauncherWindowOnStartupSettingKey, true);

            StartupTaskHelper = new StartupTaskHelper("HotLyricStartupTask");
            StartupTaskHelper.PropertyChanged += StartupTaskHelper_PropertyChanged;

            hideOnPaused = LoadSetting(HideOnPausedSettingKey, false);

            autoResetWindowPos = LoadSetting(AutoResetWindowPosSettingsKey, true);
        }

        private bool windowTransparent;
        private bool keepWindowTransparent;
        private SecondRowType secondRowType;
        private bool karaokeEnabled;
        private HorizontalAlignment lyricHorizontalAlignment;

        private bool alwaysShowBackground;
        private bool showShadow;
        private bool textStrokeEnabled;
        private LyricThemeView currentTheme;
        private LyricThemeView customizeTheme;
        private bool themeIsPresetVisible;
        private string? lyricFontFamilySource;
        private FontFamilyDisplayModel? lyricFontFamily;
        private List<FontFamilyDisplayModel> allFontfamilies;
        private AsyncRelayCommand? clearCacheCmd;
        private AsyncRelayCommand? openStorePageCmd;
        private AsyncRelayCommand? feedbackCmd;
        private AsyncRelayCommand? checkUpdateCmd;
        private AsyncRelayCommand? thirdPartyNoticeCmd;
        private AsyncRelayCommand? githubCmd;
        private AsyncRelayCommand? fontSizeCmd;
        private bool skipEmptyLyricLine;
        private bool textOpacityMask;
        private bool hideWhenFullScreenAppOpen;
        private bool lowFrameRateMode;
        private bool renderSoftwareOnly;
        private double lyricOpacity;
        private bool showLauncherWindowOnStartup;
        private bool hideOnPaused;
        private bool autoResetWindowPos;
        private AsyncRelayCommand? spotifySetLanguage;

        public StartupTaskHelper StartupTaskHelper { get; }

        public bool WindowTransparent
        {
            get => windowTransparent;
            set => ChangeSettings(ref windowTransparent, value, WindowTransparentSettingKey);
        }

        public bool KeepWindowTransparent
        {
            get => keepWindowTransparent;
            set => ChangeSettings(ref keepWindowTransparent, value, KeepWindowTransparentSettingKey);
        }

        public SecondRowType SecondRowType
        {
            get => secondRowType;
            set => ChangeSettings(ref secondRowType, value, SecondRowSettingKey);
        }

        public bool KaraokeEnabled
        {
            get => karaokeEnabled;
            set => ChangeSettings(ref karaokeEnabled, value, KaraokeEnabledSettingKey);
        }

        public HorizontalAlignment LyricHorizontalAlignment
        {
            get => lyricHorizontalAlignment;
            set => ChangeSettings(ref lyricHorizontalAlignment, value, LyricHorizontalAlignmentSettingKey);
        }

        public bool AlwaysShowBackground
        {
            get => alwaysShowBackground;
            set => ChangeSettings(ref alwaysShowBackground, value, AlwaysShowBackgroundSettingKey);
        }

        public bool ShowShadow
        {
            get => showShadow;
            set => ChangeSettings(ref showShadow, value, ShowShadowSettingKey);
        }

        public bool TextStrokeEnabled
        {
            get => textStrokeEnabled;
            set => ChangeSettings(ref textStrokeEnabled, value, TextStrokeEnabledSettingKey);
        }

        public LyricThemeView[] AllPresetThemes { get; }

        public LyricThemeView CurrentTheme
        {
            get => currentTheme;
            set
            {
                if (ChangeSettings(ref currentTheme, value))
                {
                    LyricThemeManager.CurrentThemeView = value;
                    customizeTheme = value;
                    OnPropertyChanged(nameof(CustomizeTheme));
                    ThemeIsPresetVisible = currentTheme.Name != "customize";
                }
            }
        }

        public LyricThemeView CustomizeTheme
        {
            get => customizeTheme;
            set
            {
                if (SetProperty(ref customizeTheme, value))
                {
                    LyricThemeManager.CurrentThemeView = value;
                    CurrentTheme = value ?? LyricThemeManager.LyricThemes[0];
                }
            }
        }

        public bool ThemeIsPresetVisible
        {
            get => themeIsPresetVisible;
            set => SetProperty(ref themeIsPresetVisible, value);
        }

        public IReadOnlyList<FontFamilyDisplayModel> AllFontFamilies => allFontfamilies.ToArray();

        public FontFamilyDisplayModel? LyricFontFamily
        {
            get => lyricFontFamily;
            set
            {
                lyricFontFamily = value;
                if (ChangeSettings(ref lyricFontFamilySource, value?.Source ?? "", LyricFontFamilySettingKey))
                {
                    OnPropertyChanged(nameof(LyricFontFamilyWrapper));
                }
            }
        }

        public bool SkipEmptyLyricLine
        {
            get => skipEmptyLyricLine;
            set => ChangeSettings(ref skipEmptyLyricLine, value, SkipEmptyLyricLineSettingKey);
        }

        public bool TextOpacityMask
        {
            get => textOpacityMask;
            set => ChangeSettings(ref textOpacityMask, value, TextOpacityMaskSettingKey);
        }

        public bool HideWhenFullScreenAppOpen
        {
            get => hideWhenFullScreenAppOpen;
            set => ChangeSettings(ref hideWhenFullScreenAppOpen, value, HideWhenFullScreenAppOpenSettingKey);
        }

        public bool LowFrameRateMode
        {
            get => lowFrameRateMode;
            set => ChangeSettings(ref lowFrameRateMode, value, LowFrameRateModeSettingKey);
        }

        public bool HideOnPaused
        {
            get => hideOnPaused;
            set => ChangeSettings(ref hideOnPaused, value, HideOnPausedSettingKey);
        }

        public bool AutoResetWindowPos
        {
            get => autoResetWindowPos;
            set => ChangeSettings(ref autoResetWindowPos, value, AutoResetWindowPosSettingsKey);
        }

        public FontFamily LyricFontFamilyWrapper => LyricFontFamily?.FontFamily ?? new FontFamily("Global User Interface");

        public bool RenderSoftwareOnly
        {
            get => renderSoftwareOnly;
            set
            {
                if (ChangeSettings(ref renderSoftwareOnly, value, RenderSoftwareOnlySettingKey))
                {
                    if (value)
                    {
                        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                    }
                    else
                    {
                        DispatcherHelper.UIDispatcher?.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(async () =>
                        {
                            var ownerWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                            if (ownerWindow == null) return;

                            var contentDialog = new ModernWpf.Controls.ContentDialog()
                            {
                                Title = "热词",
                                Content = "关闭强制软件渲染需要重启应用才能生效",
                                IsPrimaryButtonEnabled = true,
                                IsSecondaryButtonEnabled = false,
                                PrimaryButtonText = "立即重启",
                                CloseButtonText = "稍后重启",
                                CornerRadius = new CornerRadius(8)
                            };

                            contentDialog.Owner = ownerWindow;

                            var result = await contentDialog.ShowAsync();
                            if (result == ModernWpf.Controls.ContentDialogResult.Primary)
                            {
                                await ApplicationHelper.RestartApplicationAsync();
                            }
                        }));
                    }
                }
            }
        }

        public double LyricOpacity
        {
            get => lyricOpacity;
            set => ChangeSettings(ref lyricOpacity, value, LyricOpacitySettingKey);
        }

        public bool ShowLauncherWindowOnStartup
        {
            get => showLauncherWindowOnStartup;
            set => ChangeSettings(ref showLauncherWindowOnStartup, value, ShowLauncherWindowOnStartupSettingKey);
        }

        public bool ShowLauncherWindowOnStartupEnabled => !StartupTaskHelper.IsStartupTaskEnabled;

        private void StartupTaskHelper_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!ShowLauncherWindowOnStartupEnabled)
            {
                ShowLauncherWindowOnStartup = false;
            }
            OnPropertyChanged(nameof(ShowLauncherWindowOnStartupEnabled));
        }

        public string AppName => Package.Current.DisplayName;

        public string Author => Package.Current.PublisherDisplayName;

        public string AppVersion
        {
            get
            {
                var v = Package.Current.Id.Version;
                return new Version(v.Major, v.Minor, v.Build, v.Revision).ToString();
            }
        }

        public AsyncRelayCommand ClearCacheCmd => clearCacheCmd ?? (clearCacheCmd = new AsyncRelayCommand(async () =>
        {
            try
            {
                await LrcHelper.ClearCacheAsync();
            }
            catch { }
        }, () => !ClearCacheCmd.IsRunning));

        public AsyncRelayCommand OpenStorePageCmd => openStorePageCmd ?? (openStorePageCmd = new AsyncRelayCommand(async () =>
        {
            try
            {
                var uri = new Uri("ms-windows-store://pdp/?productid=9MXFFHVQVBV9");
                var fallbackUri = new Uri("https://www.microsoft.com/store/apps/9MXFFHVQVBV9");

                await Launcher.LaunchUriAsync(uri, new LauncherOptions()
                {
                    FallbackUri = fallbackUri,
                });
            }
            catch { }
        }));

        public AsyncRelayCommand FeedbackCmd => feedbackCmd ?? (feedbackCmd = new AsyncRelayCommand(async () =>
        {
            try
            {
                //var dict = new Dictionary<string, string>()
                //{
                //    ["subject"] = $"热词 v{AppVersion} 问题反馈"
                //};

                //var query = string.Join('&', dict.Select(c => $"{Uri.EscapeDataString(c.Key)}={Uri.EscapeDataString(c.Value)}"));

                //await Launcher.LaunchUriAsync(new Uri($"mailto://blue-fire@outlook.com?{query}"));

                await Launcher.LaunchUriAsync(new Uri("https://jq.qq.com/?_wv=1027&k=K4Ixe2Gw"));
            }
            catch { }
        }));

        public AsyncRelayCommand CheckUpdateCmd => checkUpdateCmd ?? (checkUpdateCmd = new AsyncRelayCommand(async () =>
        {
            try
            {
                var ownerWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                if (ownerWindow == null) return;

                var updateResult = await ApplicationHelper.CheckUpdateAsync();

                if (updateResult.HasUpdate)
                {
                    var contentDialog = new ModernWpf.Controls.ContentDialog()
                    {
                        Title = "热词",
                        Content = "发现新版本，是否跳转到商店下载？",
                        IsPrimaryButtonEnabled = true,
                        IsSecondaryButtonEnabled = false,
                        PrimaryButtonText = "是",
                        CloseButtonText = "否",
                        CornerRadius = new CornerRadius(8)
                    };

                    contentDialog.Owner = ownerWindow;

                    var result = await contentDialog.ShowAsync();
                    if (result == ModernWpf.Controls.ContentDialogResult.Primary)
                    {
                        _ = updateResult.TryStartUpdateAsync();
                        OpenStorePageCmd.Execute(null);
                    }
                }
                else
                {
                    var contentDialog = new ModernWpf.Controls.ContentDialog()
                    {
                        Title = "热词",
                        Content = "未发现新版本",
                        IsPrimaryButtonEnabled = true,
                        IsSecondaryButtonEnabled = false,
                        PrimaryButtonText = "确定",
                        CornerRadius = new CornerRadius(8),
                    };

                    contentDialog.Owner = ownerWindow;
                    await contentDialog.ShowAsync();
                }
            }
            catch { }

        }, () => !CheckUpdateCmd.IsRunning));

        public AsyncRelayCommand ThirdPartyNoticeCmd => thirdPartyNoticeCmd ?? (thirdPartyNoticeCmd = new AsyncRelayCommand(async () =>
        {
            try
            {
                var path = System.IO.Path.Combine(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName, "ThirdPartyNotices.txt");
                if (System.IO.File.Exists(path))
                {
                    var allText = await System.IO.File.ReadAllTextAsync(path);
                    if (!string.IsNullOrEmpty(allText))
                    {
                        var ownerWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();

                        var contentDialog = new ModernWpf.Controls.ContentDialog()
                        {
                            Title = "第三方通知",
                            Content = new ModernWpf.Controls.ScrollViewerEx()
                            {
                                Content = new System.Windows.Controls.TextBlock()
                                {
                                    Text = allText,
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = 12
                                }
                            },
                            IsPrimaryButtonEnabled = true,
                            IsSecondaryButtonEnabled = false,
                            PrimaryButtonText = "确定",
                            CornerRadius = new CornerRadius(8),
                        };

                        contentDialog.Owner = ownerWindow;
                        await contentDialog.ShowAsync();
                    }
                }
            }
            catch { }
        }, () => !ThirdPartyNoticeCmd.IsRunning));

        public AsyncRelayCommand GithubCmd => githubCmd ?? (githubCmd = new AsyncRelayCommand(async () =>
        {
            var uri = new Uri("https://github.com/cnbluefire/HotLyric");
            await Launcher.LaunchUriAsync(uri);
        }));

        public AsyncRelayCommand FontSizeCmd => fontSizeCmd ?? (fontSizeCmd = new AsyncRelayCommand(async () =>
        {
            var ownerWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();

            var contentDialog = new ModernWpf.Controls.ContentDialog()
            {
                Title = "字号帮助",
                Content = new ModernWpf.Controls.ScrollViewerEx()
                {
                    Content = new System.Windows.Controls.TextBlock()
                    {
                        Text = "拖拽歌词窗口尺寸即可改变文字大小",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    }
                },
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "查看更多帮助",
                SecondaryButtonText = "确定",
                CornerRadius = new CornerRadius(8),
            };

            contentDialog.Owner = ownerWindow;
            var res = await contentDialog.ShowAsync();

            if (res == ModernWpf.Controls.ContentDialogResult.Primary)
            {
                ShowReadMe();
            }
        }));

        public AsyncRelayCommand SpotifySetLanguage => spotifySetLanguage ?? (spotifySetLanguage = new AsyncRelayCommand(async () =>
        {
            SpotifySetLanguage.NotifyCanExecuteChanged();

            List<string> prefFileList = new List<string>();

            try
            {
                var storePackageFolder = await ApplicationHelper.TryGetPackageFromAppUserModelIdAsync("SpotifyAB.SpotifyMusic_zpdnekdrzrea0");
                if (storePackageFolder != null)
                {
                    var folder = ApplicationHelper.GetAppDataFolderLocation(storePackageFolder);

                    if (!string.IsNullOrEmpty(folder))
                    {
                        var filePath = System.IO.Path.Combine(folder, "LocalState", "Spotify", "prefs");
                        if (System.IO.File.Exists(filePath))
                        {
                            prefFileList.Add(filePath);
                        }
                    }
                }
            }
            catch { }

            try
            {
                var filePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Spotify", "prefs");

                if (System.IO.File.Exists(filePath))
                {
                    prefFileList.Add(filePath);
                }
            }
            catch { }

            var regex = new Regex("language=\".+?\"");

            int failedCount = 0;

            foreach (var file in prefFileList)
            {
                try
                {
                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        string content = "";
                        using (var reader = new StreamReader(fileStream, leaveOpen: true))
                        {
                            content = await reader.ReadToEndAsync();
                        }

                        bool flag = false;
                        if (regex.IsMatch(content))
                        {
                            var content2 = regex.Replace(content, m => "language=\"zh-CN\"");
                            flag = content != content2;
                            content = content2;
                        }
                        else
                        {
                            flag = true;

                            var sb = new StringBuilder(content);
                            if (!content.EndsWith("\n"))
                            {
                                sb.Append('\n');
                            }
                            sb.Append("language=\"zh-CN\"\n");
                            content = sb.ToString();
                        }

                        if (flag)
                        {
                            fileStream.Seek(0, SeekOrigin.Begin);
                            using (var writer = new StreamWriter(fileStream, Encoding.UTF8, leaveOpen: true))
                            {
                                await writer.WriteAsync(content);
                                await writer.FlushAsync();
                            }

                            fileStream.SetLength(fileStream.Position);
                        }
                    }
                }
                catch
                {
                    failedCount++;
                }
            }

            var ownerWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (ownerWindow != null)
            {
                var contentDialog = new ModernWpf.Controls.ContentDialog()
                {
                    Title = "热词",
                    IsPrimaryButtonEnabled = false,
                    IsSecondaryButtonEnabled = false,
                    CloseButtonText = "确定",
                    CornerRadius = new CornerRadius(8),
                    Owner = ownerWindow
                };


                if (failedCount == prefFileList.Count)
                {
                    if (prefFileList.Count == 0)
                    {
                        contentDialog.Content = "设置失败。未找到Spotify配置文件。";
                    }
                    else
                    {
                        contentDialog.Content = "设置失败。";
                    }
                }
                else
                {
                    contentDialog.Content = "设置成功，请手动重启Spotify。";
                }

                await contentDialog.ShowAsync();
            }

        }, () => !SpotifySetLanguage.IsRunning));

        [return: MaybeNull]
        private T LoadSetting<T>(string? settingsKey, [AllowNull] T defaultValue = default)
        {
            settingsKey = settingsKey?.Trim();
            if (string.IsNullOrEmpty(settingsKey)) return defaultValue;

            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(settingsKey, out var _json)
                    && _json is string json)
                {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch { }
            return defaultValue;
        }

        private bool ChangeSettings<T>(ref T field, T value, string? settingsKey = null, [CallerMemberName] string propertyName = "")
        {
            if (SetProperty(ref field, value, propertyName))
            {
                SetSettings(settingsKey, value);

                try
                {
                    NotifySettingsChanged();
                }
                catch { }

                return true;
            }
            return false;
        }

        private void SetSettings<T>(string? settingsKey, T value)
        {
            settingsKey = settingsKey?.Trim();
            if (!string.IsNullOrEmpty(settingsKey))
            {
                try
                {
                    if (value is null)
                    {
                        ApplicationData.Current.LocalSettings.Values.Remove(settingsKey);
                    }
                    else
                    {
                        var json = JsonConvert.SerializeObject(value);
                        ApplicationData.Current.LocalSettings.Values[settingsKey] = json;
                    }
                }
                catch { }
            }
        }

        private void NotifySettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? SettingsChanged;

        public void ShowSettingsWindow()
        {
            var settingsWindow = App.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();
            }

            try
            {
                settingsWindow.Show();
                settingsWindow.Activate();
            }
            catch { }
        }

        public void ShowLauncherWindow()
        {
            var launcherWindow = App.Current.Windows.OfType<LauncherWindow>().FirstOrDefault();
            if (launcherWindow == null)
            {
                launcherWindow = new LauncherWindow();
            }

            try
            {
                launcherWindow.Show();
                launcherWindow.Activate();
            }
            catch { }
        }

        public void ShowReadMe()
        {
            DispatcherHelper.UIDispatcher?.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(async () =>
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/cnbluefire/HotLyric/blob/main/README.md"));
            }));
        }

        public void TryShowReadMeOnStartup()
        {
            if (!LoadSetting(ReadMeAlreadyShowedOnStartUpSettingsKey, false))
            {
                SetSettings(ReadMeAlreadyShowedOnStartUpSettingsKey, true);
                ShowReadMe();
            }
        }

        public void ActivateInstance()
        {
            if (ViewModelLocator.Instance.LyricWindowViewModel.SelectedSession != null
                || CommandLineArgsHelper.HasLaunchParameters)
            {
                if (ViewModelLocator.Instance.LyricWindowViewModel.IsMinimized)
                {
                    ViewModelLocator.Instance.LyricWindowViewModel.IsMinimized = false;
                }
                else
                {
                    ViewModelLocator.Instance.LyricWindowViewModel.ShowBackgroundTransient(TimeSpan.FromSeconds(2));
                }
            }
        }
    }

}
