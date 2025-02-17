﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;

namespace RX_Explorer.Class
{
    public static class MSStoreHelper
    {
        private static StoreAppLicense License;
        private static StoreProductResult ProductResult;
        private static Task PreLoadTask;
        private static Task<bool> CheckPurchaseStatusTask;
        private static Task<bool> CheckHasUpdate;
        private static Task<bool> CheckIfUpdateIsMandatory;
        private static IReadOnlyList<StorePackageUpdate> Updates;
        private static readonly StoreContext Store = StoreContext.GetDefault();
        private static readonly object Locker = new object();

        public static Task<bool> CheckPurchaseStatusAsync()
        {
#if DEBUG
            lock (Locker)
            {
                return CheckPurchaseStatusTask ??= Task.FromResult(true);
            }
#else
            if (ApplicationData.Current.LocalSettings.Values["LicenseGrant"] is bool IsGrant && IsGrant)
            {
                if (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.TotalLaunchCount % 5 > 0)
                {
                    return Task.FromResult(true);
                }
            }

            lock (Locker)
            {
                return CheckPurchaseStatusTask ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        if (License != null)
                        {
                            if ((License.IsActive && !License.IsTrial) || (License.AddOnLicenses?.Any((Item) => Item.Value.InAppOfferToken == "Donation")).GetValueOrDefault())
                            {
                                ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                return true;
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckPurchaseStatusAsync)} threw an exception");
                    }

                    return false;
                });
            }
#endif
        }

        public static Task<bool> CheckHasUpdateAsync()
        {
            lock (Locker)
            {
                return CheckHasUpdate ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        return (Updates?.Any()).GetValueOrDefault();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckHasUpdateAsync)} threw an exception");
                    }

                    return false;
                });
            }
        }

        public static Task<bool> CheckIfUpdateIsMandatoryAsync()
        {
            lock (Locker)
            {
                return CheckIfUpdateIsMandatory ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        return (Updates?.Any((Update) => Update.Mandatory)).GetValueOrDefault();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckIfUpdateIsMandatoryAsync)} threw an exception");
                    }

                    return false;
                });
            }
        }

        public static Task<StorePurchaseStatus> PurchaseAsync()
        {
            return PreLoadStoreData().ContinueWith((_) =>
            {
                try
                {
                    if (ProductResult != null && ProductResult.ExtendedError == null)
                    {
                        if (ProductResult.Product != null)
                        {
                            StorePurchaseResult Result = ProductResult.Product.RequestPurchaseAsync().AsTask().Result;

                            switch (Result.Status)
                            {
                                case StorePurchaseStatus.AlreadyPurchased:
                                case StorePurchaseStatus.Succeeded:
                                    {
                                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                        break;
                                    }
                                default:
                                    {
                                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                                        break;
                                    }
                            }

                            return Result.Status;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(PurchaseAsync)} threw an exception");
                }

                return StorePurchaseStatus.NetworkError;
            });
        }

        public static Task PreLoadStoreData()
        {
            lock (Locker)
            {
                return PreLoadTask ??= Task.Factory.StartNew(() =>
                {
                    try
                    {
                        License = Store.GetAppLicenseAsync().AsTask().Result;
                        ProductResult = Store.GetStoreProductForCurrentAppAsync().AsTask().Result;

#if DEBUG
                        Updates = new List<StorePackageUpdate>(0);
#else
                        if (Windows.ApplicationModel.Package.Current.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store)
                        {
                            Updates = Store.GetAppAndOptionalStorePackageUpdatesAsync().AsTask().Result;
                        }
#endif
                    }
                    catch (Exception)
                    {
                        LogTracer.Log("Could not load MSStore data");
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        public static async Task<string> GetCustomerCollectionsIdAsync(string AzureADToken, string UserId)
        {
            if (string.IsNullOrWhiteSpace(AzureADToken))
            {
                throw new ArgumentException(nameof(AzureADToken));
            }

            return await Store.GetCustomerCollectionsIdAsync(AzureADToken, UserId);
        }

        private static async void Store_OfflineLicensesChanged(StoreContext sender, object args)
        {
            try
            {
                StoreAppLicense License = await sender.GetAppLicenseAsync();

                if (License.IsActive && !License.IsTrial)
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Store_OfflineLicensesChanged)} threw an exception");
            }
        }

        static MSStoreHelper()
        {
            Store.OfflineLicensesChanged += Store_OfflineLicensesChanged;
        }
    }
}
