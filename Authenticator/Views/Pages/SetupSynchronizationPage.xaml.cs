﻿using Domain.Utilities;
using Microsoft.OneDrive.Sdk;
using Synchronization;
using System.Collections.Generic;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Linq;
using Windows.UI.Xaml.Navigation;
using System.Text.RegularExpressions;
using Authenticator_for_Windows.Views.UserControls;
using Windows.ApplicationModel.Resources;
using Domain.Storage;
using Encryption;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Authenticator_for_Windows.Views.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SetupSynchronizationPage : Page
    {
        private const string RESOURCE_NAME = "EncryptionKey";
        private const string USERNAME_NAME = "Encryption Key";

        private string userKey;
        private PasswordVault vault;
        private ISynchronizer synchronizer;
        private MainPage mainPage;

        public SetupSynchronizationPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            object[] parameters = (object[])e.Parameter;

            synchronizer = (ISynchronizer)parameters[0];
            mainPage = (MainPage)parameters[1];
            vault = new PasswordVault();

            AccountStorage.Instance.SetSynchronizer(synchronizer);

            if (synchronizer.IsInitialSetup)
            {
                if (vault.RetrieveAll().Any())
                {
                    IReadOnlyList<PasswordCredential> credentials = vault.FindAllByResource(RESOURCE_NAME);

                    foreach (PasswordCredential credential in credentials)
                    {
                        vault.Remove(credential);
                    }
                }

                userKey = KeyGenerator.GetRandomKey();

                UserKey.Text = userKey;

                vault.Add(new PasswordCredential(RESOURCE_NAME, USERNAME_NAME, userKey));

                InitialSetupPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ConnectPanel.Visibility = Visibility.Visible;
            }
        }

        private async void Continue_Tapped(object sender, TappedRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, ShowLoading.Name, true);
            
            IOneDriveClient oneDriveClient = OneDriveClientExtensions.GetUniversalClient(new[] { "onedrive.appfolder" });
            AccountSession session = await oneDriveClient.AuthenticateAsync();

            IEncrypter encrypter = new AESEncrypter();

            if (session.AccountType == AccountType.MicrosoftAccount)
            {
                synchronizer.SetEncrypter(encrypter, userKey);
                await AccountStorage.Instance.Synchronize();

                Frame.Navigate(typeof(SetupSynchronizationFinishedPage), mainPage);
            }
        }

        private void UserKeyToValidate_TextChanged(object sender, TextChangedEventArgs e)
        {
            int selection = UserKeyToValidate.SelectionStart;

            UserKeyToValidate.Text = UserKeyToValidate.Text.ToUpper();
            UserKeyToValidate.Select(selection, 0);
        }

        private async void ButtonConnect_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (UserKeyToValidate.Text.Length == 23)
            {
                Status.Text = "Uw sleutel wordt gecontroleerd...";

                VisualStateManager.GoToState(this, ShowLoading.Name, true);

                IEncrypter encrypter = new AESEncrypter();
                synchronizer.SetEncrypter(encrypter, UserKeyToValidate.Text);

                bool result = await synchronizer.DecryptWithKey(UserKeyToValidate.Text);

                if (result)
                {
                    Status.Text = "Uw accounts worden met de cloud gesynchroniseerd...";
                    
                    await AccountStorage.Instance.Synchronize();
                    
                    vault.Add(new PasswordCredential(RESOURCE_NAME, USERNAME_NAME, UserKeyToValidate.Text));

                    Frame.Navigate(typeof(SetupSynchronizationFinishedPage), mainPage);
                }
                else
                {
                    MainPage.AddBanner(new Banner(BannerType.Danger, "De ingevoerde sleutel is incorrect.", true));
                }
            }
        }
    }
}