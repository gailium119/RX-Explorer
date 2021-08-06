﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Services.Store;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class MSStoreHelper
    {
        private static MSStoreHelper Instance;

        private StoreContext Store;
        private StoreAppLicense License;
        private StoreProductResult ProductResult;
        private Task PreLoadTask;
        private IReadOnlyList<StorePackageUpdate> Updates;

        public static MSStoreHelper Current => Instance ??= new MSStoreHelper();

        public Task<bool> CheckPurchaseStatusAsync()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("LicenseGrant", out object GrantState) && Convert.ToBoolean(GrantState))
                {
                    return Task.FromResult(true);
                }

                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                return PreLoadTask.ContinueWith((_) =>
                {
                    if (License != null)
                    {
                        if (License.AddOnLicenses.Any((Item) => Item.Value.InAppOfferToken == "Donation"))
                        {
                            ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                            return true;
                        }
                        else
                        {
                            if (License.IsActive)
                            {
                                if (License.IsTrial)
                                {
                                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                                    return false;
                                }
                                else
                                {
                                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                    return true;
                                }
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                                return false;
                            }
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckPurchaseStatusAsync)} threw an exception");
                return Task.FromResult(false);
            }
        }

        public Task<bool> CheckHasUpdateAsync()
        {
            try
            {
                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                return PreLoadTask.ContinueWith((_) =>
                {
                    if (Updates != null)
                    {
                        return Updates.Any();
                    }
                    else
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckHasUpdateAsync)} threw an exception");
                return Task.FromResult(false);
            }
        }

        public Task<bool> CheckIfUpdateIsMandatoryAsync()
        {
            try
            {
                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                return PreLoadTask.ContinueWith((_) =>
                {
                    if (Updates != null)
                    {
                        foreach (StorePackageUpdate Update in Updates)
                        {
                            if (Update.Mandatory)
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                    else
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckIfUpdateIsMandatoryAsync)} threw an exception");
                return Task.FromResult(false);
            }
        }

        public async Task<StorePurchaseStatus> PurchaseAsync()
        {
            try
            {
                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                await PreLoadTask;

                if (ProductResult != null && ProductResult.ExtendedError == null)
                {
                    if (ProductResult.Product != null)
                    {
                        StorePurchaseResult Result = await ProductResult.Product.RequestPurchaseAsync();

                        switch (Result.Status)
                        {
                            case StorePurchaseStatus.AlreadyPurchased:
                            case StorePurchaseStatus.Succeeded:
                                {
                                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                    break;
                                }
                        }

                        return Result.Status;
                    }
                    else
                    {
                        return StorePurchaseStatus.NetworkError;
                    }
                }
                else
                {
                    return StorePurchaseStatus.NetworkError;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(PurchaseAsync)} threw an exception");
                return StorePurchaseStatus.NetworkError;
            }
        }

        public void PreLoadStoreData()
        {
            PreLoadTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    Store = StoreContext.GetDefault();
                    Store.OfflineLicensesChanged += Store_OfflineLicensesChanged;

                    License = Store.GetAppLicenseAsync().AsTask().Result;
                    ProductResult = Store.GetStoreProductForCurrentAppAsync().AsTask().Result;

                    if (!Package.Current.IsDevelopmentMode)
                    {
                        Updates = Store.GetAppAndOptionalStorePackageUpdatesAsync().AsTask().Result;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not load MSStore data");
                }
            }, TaskCreationOptions.LongRunning);
        }

        private MSStoreHelper()
        {

        }

        private async void Store_OfflineLicensesChanged(StoreContext sender, object args)
        {
            try
            {
                StoreAppLicense License = await sender.GetAppLicenseAsync();

                if (License.IsActive && !License.IsTrial)
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Store_OfflineLicensesChanged)} threw an exception");
            }
        }
    }
}
