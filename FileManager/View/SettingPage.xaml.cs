﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TinyPinyin.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager
{
    public sealed partial class SettingPage : Page
    {
        private ObservableCollection<FeedBackItem> FeedBackCollection;

        public string UserName { get; set; }

        public string UserID { get; set; }

        public static SettingPage ThisPage { get; private set; }

        public static bool IsDoubleClickEnable { get; set; } = true;

        private ObservableCollection<BackgroundPicture> PictureList = new ObservableCollection<BackgroundPicture>();

        public SettingPage()
        {
            InitializeComponent();
            ThisPage = this;
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
            PictureGirdView.ItemsSource = PictureList;

            Loading += SettingPage_Loading;
            Loading += SettingPage_Loading1;
            Loaded += SettingPage_Loaded;

            UserName = ApplicationData.Current.LocalSettings.Values["SystemUserName"].ToString();
            UserID = ApplicationData.Current.LocalSettings.Values["SystemUserID"].ToString();

            EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "正在加载..." : "Loading...";
        }

        private async void SettingPage_Loading1(FrameworkElement sender, object args)
        {
            AutoBoot.Toggled -= AutoBoot_Toggled;
            switch ((await StartupTask.GetAsync("RXExplorer")).State)
            {
                case StartupTaskState.DisabledByPolicy:
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.Disabled:
                    {
                        AutoBoot.IsOn = false;
                        break;
                    }
                default:
                    {
                        AutoBoot.IsOn = true;
                        break;
                    }
            }
            AutoBoot.Toggled += AutoBoot_Toggled;
        }

        private async void SettingPage_Loaded1(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            if (PictureMode.IsChecked.GetValueOrDefault() && PictureGirdView.SelectedItem != null)
            {
                PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem);
            }
        }

        private void SettingPage_Loading(FrameworkElement sender, object args)
        {
            Loading -= SettingPage_Loading;

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                UIMode.Items.Add("推荐");
                UIMode.Items.Add("自定义");
            }
            else
            {
                UIMode.Items.Add("Recommand");
                UIMode.Items.Add("Custom");
            }

            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode)
            {
                UIMode.SelectedItem = UIMode.Items.Where((Item) => Item.ToString() == Mode).FirstOrDefault();
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = Globalization.Language == LanguageEnum.Chinese
                                                                                ? "推荐"
                                                                                : "Recommand";
                UIMode.SelectedIndex = 0;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
            {
                OpenLeftArea.IsOn = Enable;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
            {
                FolderOpenMethod.IsOn = IsDoubleClick;
            }

            if (AppThemeController.Current.Theme == ElementTheme.Light)
            {
                CustomFontColor.IsOn = true;
            }

            Loaded += SettingPage_Loaded1;
        }

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingPage_Loaded;

            FeedBackCollection = new ObservableCollection<FeedBackItem>();
            FeedBackCollection.CollectionChanged += (s, t) =>
            {
                if (FeedBackCollection.Count == 0)
                {
                    EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "无任何反馈或建议" : "No feedback or suggestions";
                    EmptyFeedBack.Visibility = Visibility.Visible;
                    FeedBackList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyFeedBack.Visibility = Visibility.Collapsed;
                    FeedBackList.Visibility = Visibility.Visible;
                }
            };
            FeedBackList.ItemsSource = FeedBackCollection;

            try
            {
                await foreach (FeedBackItem FeedBackItem in MySQL.Current.GetAllFeedBackAsync())
                {
                    if (FeedBackItem.Title.StartsWith("@"))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            FeedBackItem.UpdateTitleAndSuggestion(FeedBackItem.Title, await FeedBackItem.Suggestion.Translate());
                        }
                        else
                        {
                            FeedBackItem.UpdateTitleAndSuggestion(FeedBackItem.Title.All((Char) => !PinyinHelper.IsChinese(Char)) ? FeedBackItem.Title : PinyinHelper.GetPinyin(FeedBackItem.Title), await FeedBackItem.Suggestion.Translate());
                        }
                    }
                    else
                    {
                        FeedBackItem.UpdateTitleAndSuggestion(await FeedBackItem.Title.Translate(), await FeedBackItem.Suggestion.Translate());
                    }

                    FeedBackCollection.Add(FeedBackItem);
                }
            }
            finally
            {
                if (FeedBackCollection.Count == 0)
                {
                    EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "无任何反馈或建议" : "No feedback or suggestions";
                }
                else
                {
                    await Task.Delay(1000);
                    FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
                }
            }
        }

        private void Like_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.Yellow);
            LikeText.Foreground = new SolidColorBrush(Colors.Yellow);
        }

        private void Like_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.White);
            LikeText.Foreground = new SolidColorBrush(Colors.White);
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.Nav.Navigate(typeof(AboutMe), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private async void Like_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _ = await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
        }

        private async void FlyoutContinue_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
            await SQLite.Current.ClearSearchHistoryRecord();

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "搜索历史记录清理完成",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Tips",
                    Content = "Search history cleanup completed",
                    CloseButtonText = "Confirm"
                };
                _ = await dialog.ShowAsync();
            }
        }

        private void FlyoutCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            ResetDialog Dialog = new ResetDialog();
            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (Dialog.IsClearSecureFolder)
                {
                    SQLite.Current.Dispose();
                    MySQL.Current.Dispose();
                    try
                    {
                        await ApplicationData.Current.ClearAsync();
                    }
                    catch (Exception)
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders();
                    }

                    Window.Current.Activate();
                    switch (await CoreApplication.RequestRestartAsync(string.Empty))
                    {
                        case AppRestartFailureReason.InvalidUser:
                        case AppRestartFailureReason.NotInForeground:
                        case AppRestartFailureReason.Other:
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "自动重新启动过程中出现问题，请手动重启RX文件管理器",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "There was a problem during the automatic restart, please restart the RX Explorer manually",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                break;
                            }
                    }
                }
                else
                {
                    LoadingText.Text = Globalization.Language == LanguageEnum.Chinese ? "正在导出..." : "Exporting";
                    LoadingControl.IsLoading = true;

                    StorageFolder SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);
                    string FileEncryptionAesKey = KeyGenerator.GetMD5FromKey(CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword"), 16);

                    foreach (var Item in await SecureFolder.GetFilesAsync())
                    {
                        try
                        {
                            _ = await Item.DecryptAsync(Dialog.ExportFolder, FileEncryptionAesKey);

                            await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                        catch (Exception ex)
                        {
                            await Item.MoveAsync(Dialog.ExportFolder, Item.Name + (Globalization.Language == LanguageEnum.Chinese ? "-解密错误备份" : "-Decrypt Error Backup"), NameCollisionOption.GenerateUniqueName);
                            if (ex is PasswordErrorException)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "由于解密密码错误，解密失败，导出任务已经终止\r\r这可能是由于待解密文件数据不匹配造成的",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "The decryption failed due to the wrong decryption password, the export task has been terminated \r \rThis may be caused by a mismatch in the data of the files to be decrypted",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                            }
                            else if (ex is FileDamagedException)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "由于待解密文件的内部结构损坏，解密失败，导出任务已经终止\r\r这可能是由于文件数据已损坏或被修改造成的",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "Because the internal structure of the file to be decrypted is damaged and the decryption fails, the export task has been terminated \r \rThis may be caused by the file data being damaged or modified",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                            }
                        }
                    }

                    SQLite.Current.Dispose();
                    MySQL.Current.Dispose();
                    try
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Roaming);
                    }
                    catch (Exception)
                    {
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.RoamingFolder.DeleteAllSubFilesAndFolders();
                    }

                    await Task.Delay(1000);

                    LoadingControl.IsLoading = false;

                    await Task.Delay(1000);

                    Window.Current.Activate();
                    switch (await CoreApplication.RequestRestartAsync(string.Empty))
                    {
                        case AppRestartFailureReason.InvalidUser:
                        case AppRestartFailureReason.NotInForeground:
                        case AppRestartFailureReason.Other:
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "自动重新启动过程中出现问题，请手动重启RX文件管理器",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "There was a problem during the automatic restart, please restart the RX Explorer manually",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync();
                                }
                                break;
                            }
                    }
                }
            }
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = UIMode.SelectedItem.ToString();

            if (UIMode.SelectedIndex == 0)
            {
                CustomUIArea.Visibility = Visibility.Collapsed;

                AcrylicMode.IsChecked = null;
                PictureMode.IsChecked = null;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
                BackgroundController.Current.TintOpacity = 0.6;
                BackgroundController.Current.TintLuminosityOpacity = -1;
                BackgroundController.Current.AcrylicColor = Colors.LightSlateGray;
            }
            else
            {
                CustomUIArea.Visibility = Visibility.Visible;

                if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string Mode)
                {
                    if ((BackgroundBrushType)Enum.Parse(typeof(BackgroundBrushType), Mode) == BackgroundBrushType.Acrylic)
                    {
                        AcrylicMode.IsChecked = true;
                    }
                    else
                    {
                        PictureMode.IsChecked = true;
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Acrylic);
                    AcrylicMode.IsChecked = true;

                    if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                    {
                        float Value = Convert.ToSingle(Luminosity);
                        TintLuminositySlider.Value = Value;
                        BackgroundController.Current.TintLuminosityOpacity = Value;
                    }
                    else
                    {
                        TintLuminositySlider.Value = 0.8;
                        BackgroundController.Current.TintLuminosityOpacity = 0.8;
                    }

                    if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                    {
                        float Value = Convert.ToSingle(Opacity);
                        TintOpacitySlider.Value = Value;
                        BackgroundController.Current.TintOpacity = Value;
                    }
                    else
                    {
                        TintOpacitySlider.Value = 0.6;
                        BackgroundController.Current.TintOpacity = 0.6;
                    }

                    if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                    {
                        BackgroundController.Current.AcrylicColor = BackgroundController.Current.GetColorFromHexString(AcrylicColor);
                    }
                }
            }
        }

        private void AcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerTeachTip.IsOpen = true;
        }

        private void TintOpacityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OpacityTip.IsOpen = true;
        }

        private void TintLuminosityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LuminosityTip.IsOpen = true;
        }

        private void ColorPickerTeachTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = AcrylicColorPicker.Color.ToString();
        }

        private async void Donation_Click(object sender, RoutedEventArgs e)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "支持",
                    Content = "开发者开发RX文件管理器花费了大量精力\r" +
                              "🎉您可以自愿为开发者贡献一点小零花钱🎉\r\r" +
                              "若您不愿意，则可以点击\"跪安\"以取消\r" +
                              "若您愿意支持开发者，则可以点击\"准奏\"\r\r" +
                              "Tips: 支持的小伙伴可以解锁独有文件保险柜功能：“安全域”",
                    PrimaryButtonText = "准奏",
                    CloseButtonText = "跪安"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult PurchasedProductResult = await Store.GetUserCollectionAsync(new string[] { "Durable" });
                    if (PurchasedProductResult.ExtendedError == null)
                    {
                        if (PurchasedProductResult.Products.Count > 0)
                        {
                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                            {
                                Title = "再次感谢",
                                Content = "您已为RX支持过一次了，您的心意开发者已心领\r\r" +
                                          "RX的初衷并非是赚钱，因此不可重复支持哦\r\r" +
                                          "您可以向周围的人宣传一下RX，也是对RX的最好的支持哦（*＾-＾*）\r\r" +
                                          "Ruofan,\r敬上",
                                CloseButtonText = "朕知道了"
                            };
                            _ = await QueueContenDialog.ShowAsync();
                        }
                        else
                        {
                            StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                            if (StoreProductResult.ExtendedError == null)
                            {
                                StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                                if (Product != null)
                                {
                                    switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                                    {
                                        case StorePurchaseStatus.Succeeded:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "感谢",
                                                    Content = "感谢您的支持，我们将努力将RX做得越来越好q(≧▽≦q)\r\r" +
                                                              "RX文件管理器的诞生，是为了填补UWP文件管理器缺位的空白\r" +
                                                              "它并非是一个盈利项目，因此下载和使用都是免费的，并且不含有广告\r" +
                                                              "RX的目标是打造一个免费且功能全面文件管理器\r" +
                                                              "RX文件管理器是我利用业余时间开发的项目\r" +
                                                              "希望大家能够喜欢\r\r" +
                                                              "Ruofan,\r敬上",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.NotPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "感谢",
                                                    Content = "无论支持与否，RX始终如一\r\r" +
                                                              "即使您最终决定放弃支持本项目，依然十分感谢您能够点进来看一看\r\r" +
                                                              "Ruofan,\r敬上",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        default:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "抱歉",
                                                    Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                {
                                    Title = "抱歉",
                                    Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                                    CloseButtonText = "朕知道了"
                                };
                                _ = await QueueContenDialog.ShowAsync();
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "抱歉",
                            Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                            CloseButtonText = "朕知道了"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Donation",
                    Content = "It takes a lot of effort for developers to develop RX file manager\r" +
                              "🎉You can volunteer to contribute a little pocket money to developers.🎉\r\r" +
                              "Please donate 0.99$ 🍪\r\r" +
                              "If you don't want to, you can click \"Later\" to cancel\r" +
                              "if you want to donate, you can click \"Donate\" to support developer\r\r" +
                              "Tips: Donator can unlock the unique file safe feature: \"Security Area\"",
                    PrimaryButtonText = "Donate",
                    CloseButtonText = "Later"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult PurchasedProductResult = await Store.GetUserCollectionAsync(new string[] { "Durable" });
                    if (PurchasedProductResult.ExtendedError == null)
                    {
                        if (PurchasedProductResult.Products.Count > 0)
                        {
                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                            {
                                Title = "Thanks again",
                                Content = "You have already supported RX once, thank you very much\r\r" +
                                          "The original intention of RX is not to make money, so you can't repeat purchase it.\r\r" +
                                          "You can advertise the RX to the people around you, and it is also the best support for RX（*＾-＾*）\r\r" +
                                          "Sincerely,\rRuofan",
                                CloseButtonText = "Got it"
                            };
                            _ = await QueueContenDialog.ShowAsync();
                        }
                        else
                        {
                            StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                            if (StoreProductResult.ExtendedError == null)
                            {
                                StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                                if (Product != null)
                                {
                                    switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                                    {
                                        case StorePurchaseStatus.Succeeded:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Appreciation",
                                                    Content = "Thank you for your support, we will work hard to make RX better and better q(≧▽≦q)\r\r" +
                                                              "The RX file manager was born to fill the gaps in the UWP file manager\r" +
                                                              "This is not a profitable project, so downloading and using are free and do not include ads\r" +
                                                              "RX's goal is to create a free and full-featured file manager\r" +
                                                              "RX File Manager is a project I developed in my spare time\r" +
                                                              "I hope everyone likes\r\r" +
                                                              "Sincerely,\rRuofan",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.NotPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Appreciation",
                                                    Content = "Whether supported or not, RX is always the same\r\r" +
                                                              "Even if you finally decide to give up supporting the project, thank you very much for being able to click to see it\r\r" +
                                                              "Sincerely,\rRuofan",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        default:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Sorry",
                                                    Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                {
                                    Title = "Sorry",
                                    Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                                    CloseButtonText = "Got it"
                                };
                                _ = await QueueContenDialog.ShowAsync();
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Sorry",
                            Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                            CloseButtonText = "Got it"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }
        }

        private async void UpdateLogLink_Click(object sender, RoutedEventArgs e)
        {
            WhatIsNew Dialog = new WhatIsNew();
            await Dialog.ShowAsync();
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86)
            {
                SystemInfoDialog dialog = new SystemInfoDialog();
                _ = await dialog.ShowAsync();
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "抱歉",
                        Content = "系统信息窗口所依赖的部分组件仅支持在X86或X64处理器上实现\rARM处理器暂不支持，因此无法打开此窗口",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Sorry",
                        Content = "Some components that the system information dialog depends on only support X86 or X64 processors\rUnsupport ARM processor for now, so this dialog will not be opened",
                        CloseButtonText = "Got it"
                    };
                    _ = await dialog.ShowAsync();
                }
            }

        }

        private async void AddFeedBack_Click(object sender, RoutedEventArgs e)
        {
            FeedBackDialog Dialog = new FeedBackDialog();
            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (FeedBackCollection.Count != 0)
                {
                    if (FeedBackCollection.FirstOrDefault((It) => It.UserName == UserName && It.Suggestion == Dialog.FeedBack && It.Title == Dialog.TitleName) == null)
                    {
                        FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                        if (await MySQL.Current.SetFeedBackAsync(Item))
                        {
                            FeedBackCollection.Add(Item);
                            await Task.Delay(1000);
                            FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
                        }
                        else
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "因网络原因无法进行此项操作",
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "This operation cannot be performed due to network reasons",
                                    CloseButtonText = "Got it"
                                };
                                _ = await dialog.ShowAsync();
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog TipsDialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "The same feedback already exists, please do not submit it repeatedly",
                            CloseButtonText = "Got it"
                        };
                        _ = await TipsDialog.ShowAsync();
                    }
                }
                else
                {
                    FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                    if (!await MySQL.Current.SetFeedBackAsync(Item))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        FeedBackCollection.Add(Item);
                    }
                }
            }
        }

        private void FeedBackList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                FeedBackList.SelectedItem = Item;
                FeedBackList.ContextFlyout = UserID == "zhuxb711@yeah.net" ? FeedBackFlyout : (Item.UserID == UserID ? FeedBackFlyout : null);
            }
        }

        private async void FeedBackEdit_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                FeedBackDialog Dialog = new FeedBackDialog(SelectItem.Title, SelectItem.Suggestion);
                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (!await MySQL.Current.UpdateFeedBackTitleAndSuggestionAsync(Dialog.TitleName, Dialog.FeedBack, SelectItem.GUID))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        SelectItem.UpdateTitleAndSuggestion(Dialog.TitleName, Dialog.FeedBack);
                    }
                }
            }
        }

        private async void FeedBackDelete_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                if (!await MySQL.Current.DeleteFeedBackAsync(SelectItem))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
                else
                {
                    FeedBackCollection.Remove(SelectItem);
                }
            }
        }

        private void FeedBackQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FeedBackTip.IsOpen = true;
        }

        private void OpenLeftArea_Toggled(object sender, RoutedEventArgs e)
        {
            if (OpenLeftArea.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;
                ThisPC.ThisPage.Gr.ColumnDefinitions[0].Width = new GridLength(300);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = false;
                ThisPC.ThisPage.Gr.ColumnDefinitions[0].Width = new GridLength(0);
            }
        }

        private void FolderOpenMethod_Toggled(object sender, RoutedEventArgs e)
        {
            if (FolderOpenMethod.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = true;
                IsDoubleClickEnable = true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = false;
                IsDoubleClickEnable = false;
            }
        }

        private void AcrylicMode_Checked(object sender, RoutedEventArgs e)
        {
            CustomAcrylicArea.Visibility = Visibility.Visible;
            CustomPictureArea.Visibility = Visibility.Collapsed;

            BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
            ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Acrylic);

            if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
            {
                float Value = Convert.ToSingle(Luminosity);
                TintLuminositySlider.Value = Value;
                BackgroundController.Current.TintLuminosityOpacity = Value;
            }
            else
            {
                TintLuminositySlider.Value = 0.8;
                BackgroundController.Current.TintLuminosityOpacity = 0.8;
            }

            if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
            {
                float Value = Convert.ToSingle(Opacity);
                TintOpacitySlider.Value = Value;
                BackgroundController.Current.TintOpacity = Value;
            }
            else
            {
                TintOpacitySlider.Value = 0.6;
                BackgroundController.Current.TintOpacity = 0.6;
            }

            if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
            {
                BackgroundController.Current.AcrylicColor = BackgroundController.Current.GetColorFromHexString(AcrylicColor);
            }
        }

        private async void PictureMode_Checked(object sender, RoutedEventArgs e)
        {
            CustomAcrylicArea.Visibility = Visibility.Collapsed;
            CustomPictureArea.Visibility = Visibility.Visible;

            if (PictureList.Count == 0)
            {
                foreach (Uri ImageUri in await SQLite.Current.GetBackgroundPictureAsync())
                {
                    BitmapImage Image = new BitmapImage
                    {
                        DecodePixelHeight = 90,
                        DecodePixelWidth = 160
                    };
                    PictureList.Add(new BackgroundPicture(Image, ImageUri));
                    Image.UriSource = ImageUri;
                }
            }

            ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
            {
                BackgroundPicture PictureItem = PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri);

                PictureGirdView.SelectedItem = PictureItem;
                PictureGirdView.ScrollIntoViewSmoothly(PictureItem);
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureItem.PictureUri.ToString());
            }
            else
            {
                PictureGirdView.SelectedIndex = 0;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureList.FirstOrDefault().PictureUri.ToString());
            }
        }

        private void PictureGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Picture.PictureUri.ToString());
            }
        }

        private async void AddImageToPictureButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add(".png");
            Picker.FileTypeFilter.Add(".jpg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".bmp");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                StorageFolder ImageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("CustomImageFolder");

                StorageFile CopyedFile = await File.CopyAsync(ImageFolder, $"BackgroundPicture_{Guid.NewGuid().ToString("N")}{File.FileType}", NameCollisionOption.GenerateUniqueName);

                BitmapImage Bitmap = new BitmapImage
                {
                    DecodePixelWidth = 160,
                    DecodePixelHeight = 90
                };
                BackgroundPicture Picture = new BackgroundPicture(Bitmap, new Uri($"ms-appdata:///local/CustomImageFolder/{CopyedFile.Name}"));
                PictureList.Add(Picture);
                Bitmap.UriSource = Picture.PictureUri;

                PictureGirdView.ScrollIntoViewSmoothly(Picture);
                PictureGirdView.SelectedItem = Picture;

                await SQLite.Current.SetBackgroundPictureAsync(Picture.PictureUri.ToString());
            }
        }

        private async void DeletePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                await SQLite.Current.DeleteBackgroundPictureAsync(Picture.PictureUri.ToString());
                PictureList.Remove(Picture);
                PictureGirdView.SelectedIndex = PictureList.Count - 1;
            }
        }

        private void PictureGirdView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is BackgroundPicture Picture)
            {
                PictureGirdView.ContextFlyout = PictureFlyout;

                DeletePictureButton.IsEnabled = !Picture.PictureUri.ToString().StartsWith("ms-appx://");

                PictureGirdView.SelectedItem = Picture;
            }
            else
            {
                PictureGirdView.ContextFlyout = null;
            }
        }

        private void CustomFontColor_Toggled(object sender, RoutedEventArgs e)
        {
            if (CustomFontColor.IsOn)
            {
                AppThemeController.Current.ChangeThemeTo(ElementTheme.Light);
            }
            else
            {
                AppThemeController.Current.ChangeThemeTo(ElementTheme.Dark);
            }
        }

        private async void AutoBoot_Toggled(object sender, RoutedEventArgs e)
        {
            StartupTask BootTask = await StartupTask.GetAsync("RXExplorer");

            if (AutoBoot.IsOn)
            {
                switch (await BootTask.RequestEnableAsync())
                {
                    case StartupTaskState.Disabled:
                    case StartupTaskState.DisabledByPolicy:
                    case StartupTaskState.DisabledByUser:
                        {
                            AutoBoot.Toggled -= AutoBoot_Toggled;
                            AutoBoot.IsOn = false;
                            AutoBoot.Toggled += AutoBoot_Toggled;

                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "由于自动启动被系统禁用，RX无法自动开启此功能\r您可以前往[系统设置]页面管理",
                                    PrimaryButtonText = "立即开启",
                                    CloseButtonText = "暂不开启"
                                };
                                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Tips",
                                    Content = "RX cannot be turned on automatically because startup is disabled by the system\rYou can go to the [System Settings] page to manage",
                                    PrimaryButtonText = "Now",
                                    CloseButtonText = "Later"
                                };
                                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                }
                            }
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
            else
            {
                BootTask.Disable();
            }
        }
    }
}
