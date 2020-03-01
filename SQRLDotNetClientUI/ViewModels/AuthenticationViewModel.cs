﻿using Avalonia;
using ReactiveUI;
using Sodium;
using SQRLDotNetClientUI.Views;
using SQRLUtilsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SQRLDotNetClientUI.Utils;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia;

namespace SQRLDotNetClientUI.ViewModels
{
    public class AuthenticationViewModel : ViewModelBase
    {
        public enum LoginAction
        {
            Login,
            Disable,
            Remove
        };

        private LoginAction action = LoginAction.Login;
        public LoginAction Action
        {
            get => action; set
            {
                this.RaiseAndSetIfChanged(ref action, value);
            }
        }

        private SQRL _sqrlInstance = SQRL.GetInstance(true);

        public Uri Site { get; set; }
        public string AltID { get; set; }
        public string Password { get; set; }

        public bool AuthAction { get; set; }

        public string _siteID = "";
        public string SiteID 
        { 
            get { return $"{this.Site.Host}"; } 
            set => this.RaiseAndSetIfChanged(ref _siteID, value); 
        }

        private int _Block1Progress = 0;

        public int Block1Progress 
        { 
            get => _Block1Progress; 
            set => this.RaiseAndSetIfChanged(ref _Block1Progress, value); 
        }

        public int MaxProgress { get => 100; }

        public AuthenticationViewModel()
        {
            Init();
            this.Site = new Uri("https://google.com");
            this.SiteID = this.Site.Host;
        }

        public AuthenticationViewModel(Uri site)
        {
            Init();
            this.Site = site;
            this.SiteID = site.Host;
        }

        private void Init()
        {
            this.Title = _loc.GetLocalizationValue("AuthenticationWindowTitle");
        }

        public void Cancel()
        {
            if (_sqrlInstance.cps.PendingResponse)
            {
                _sqrlInstance.cps.cpsBC.Add(_sqrlInstance.cps.Can);
            }
            while (_sqrlInstance.cps.PendingResponse)
                ;
            _mainWindow.Close();
        }

        public async void Login()
        {
            var progressBlock1 = new Progress<KeyValuePair<int, string>>(percent =>
            {
                this.Block1Progress = (int)percent.Key;
            });

            var result = await SQRL.DecryptBlock1(_identityManager.CurrentIdentity, this.Password, progressBlock1);
            if (result.Item1)
            {

                var siteKvp = SQRL.CreateSiteKey(this.Site, this.AltID, result.Item2);

                Dictionary<byte[], Tuple<byte[], KeyPair>> priorKvps = null;
                priorKvps = GeneratePriorKeyInfo(result, priorKvps);
                SQRLOptions sqrlOpts = new SQRLOptions(SQRLOptions.SQRLOpts.CPS | SQRLOptions.SQRLOpts.SUK);
                var serverResponse = SQRL.GenerateQueryCommand(this.Site, siteKvp, sqrlOpts, null, 0, priorKvps);
                if (!serverResponse.CommandFailed)
                {
                    // New Account ask if they want to create one

                    if (!serverResponse.CurrentIDMatch && !serverResponse.PreviousIDMatch)
                    {
                        serverResponse = await HandleNewAccount(result, siteKvp, sqrlOpts, serverResponse);
                    }
                    else if (serverResponse.PreviousIDMatch)
                    {
                        byte[] ursKey = null;
                        ursKey = SQRL.GetURSKey(serverResponse.PriorMatchedKey.Key, Sodium.Utilities.Base64ToBinary(serverResponse.SUK, string.Empty, Sodium.Utilities.Base64Variant.UrlSafeNoPadding));
                        StringBuilder additionalData = null;
                        if (!string.IsNullOrEmpty(serverResponse.SIN))
                        {
                            additionalData = new StringBuilder();
                            byte[] ids = SQRL.CreateIndexedSecret(this.Site, AltID, result.Item2, Encoding.UTF8.GetBytes(serverResponse.SIN));
                            additionalData.AppendLineWindows($"ins={Sodium.Utilities.BinaryToBase64(ids, Utilities.Base64Variant.UrlSafeNoPadding)}");
                            byte[] pids = SQRL.CreateIndexedSecret(serverResponse.PriorMatchedKey.Value.Item1, Encoding.UTF8.GetBytes(serverResponse.SIN));
                            additionalData.AppendLineWindows($"pins={Sodium.Utilities.BinaryToBase64(pids, Utilities.Base64Variant.UrlSafeNoPadding)}");

                        }
                        serverResponse = SQRL.GenerateIdentCommandWithReplace(serverResponse.NewNutURL, siteKvp, serverResponse.FullServerRequest, result.Item3, ursKey, serverResponse.PriorMatchedKey.Value.Item2, sqrlOpts, additionalData);
                    }
                    else if (serverResponse.CurrentIDMatch)
                    {
                        int askResponse = 0;
                        if (serverResponse.HasAsk)
                        {
                            MainWindow w = new MainWindow();

                            var mwTemp = new MainWindowViewModel();
                            w.DataContext = mwTemp;
                            w.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                            var avm = new AskViewModel(serverResponse)
                            {
                                CurrentWindow = w
                            };
                            mwTemp.Content = avm;
                            askResponse = await w.ShowDialog<int>(_mainWindow);
                        }

                        StringBuilder addClientData = null;
                        if (askResponse > 0)
                        {
                            addClientData = new StringBuilder();
                            addClientData.AppendLineWindows($"btn={askResponse}");
                        }
                        //Here
                        switch (Action)
                        {
                            case LoginAction.Login:
                                {
                                    addClientData = GenerateSIN(result, serverResponse, addClientData);
                                    serverResponse = SQRL.GenerateSQRLCommand(SQRLCommands.ident, serverResponse.NewNutURL, siteKvp, serverResponse.FullServerRequest, addClientData, sqrlOpts);
                                    if (SQRL.GetInstance(true).cps != null && _sqrlInstance.cps.PendingResponse)
                                    {
                                        _sqrlInstance.cps.cpsBC.Add(new Uri(serverResponse.SuccessUrl));
                                    }
                                    while (_sqrlInstance.cps.PendingResponse)
                                        ;
                                    _mainWindow.Close();
                                }
                                break;
                            case LoginAction.Disable:
                                {
                                    var disableAccountAlert = string.Format(_loc.GetLocalizationValue("DisableAccountAlert"), this.SiteID, Environment.NewLine);
                                    var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
                                        _loc.GetLocalizationValue("WarningMessageBoxTitle").ToUpper(), 
                                        $"{disableAccountAlert}", 
                                        ButtonEnum.YesNo, 
                                        Icon.Lock);
                                    messageBoxStandardWindow.SetMessageStartupLocation(Avalonia.Controls.WindowStartupLocation.CenterOwner);
                                    var btResult = await messageBoxStandardWindow.ShowDialog(_mainWindow);

                                    if (btResult == ButtonResult.Yes)
                                    {
                                        GenerateSIN(result, serverResponse, addClientData);
                                        serverResponse = SQRL.GenerateSQRLCommand(SQRLCommands.disable, serverResponse.NewNutURL, siteKvp, serverResponse.FullServerRequest, addClientData, sqrlOpts);
                                        if (_sqrlInstance.cps != null && _sqrlInstance.cps.PendingResponse)
                                        {
                                            _sqrlInstance.cps.cpsBC.Add(_sqrlInstance.cps.Can);
                                        }
                                        while (_sqrlInstance.cps.PendingResponse)
                                            ;
                                        _mainWindow.Close();
                                    }
                                }
                                break;
                            case LoginAction.Remove:
                                {

                                }
                                break;
                        }
                    }
                }
                else
                {
                    var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
                        _loc.GetLocalizationValue("ErrorTitleGeneric"), 
                        _loc.GetLocalizationValue("SQRLCommandFailedUnknown"), 
                        ButtonEnum.Ok, 
                        Icon.Error);
                    await messageBoxStandardWindow.ShowDialog(_mainWindow);
                }
            }
            else
            {
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
                    _loc.GetLocalizationValue("BadPasswordErrorTitle"),
                    _loc.GetLocalizationValue("BadPasswordError"),
                    ButtonEnum.Ok, 
                    Icon.Error);
                await messageBoxStandardWindow.ShowDialog(_mainWindow);
            }

        }

        private StringBuilder GenerateSIN(Tuple<bool, byte[], byte[]> result, SQRLServerResponse serverResponse, StringBuilder addClientData)
        {
            if (!string.IsNullOrEmpty(serverResponse.SIN))
            {
                if (addClientData == null)
                    addClientData = new StringBuilder();
                byte[] ids = SQRL.CreateIndexedSecret(this.Site, AltID, result.Item2, Encoding.UTF8.GetBytes(serverResponse.SIN));
                addClientData.AppendLineWindows($"ins={Sodium.Utilities.BinaryToBase64(ids, Utilities.Base64Variant.UrlSafeNoPadding)}");
            }

            return addClientData;
        }

        private async System.Threading.Tasks.Task<SQRLServerResponse> HandleNewAccount(Tuple<bool, byte[], byte[]> result, KeyPair siteKvp, SQRLOptions sqrlOpts, SQRLServerResponse serverResponse)
        {
            string newAccountQuestion = string.Format(_loc.GetLocalizationValue("NewAccountQuestion"), this.SiteID);
            string genericQuestionTitle = string.Format(_loc.GetLocalizationValue("GenericQuestionTitle"), this.SiteID);

            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandardWindow(
                $"{genericQuestionTitle}", 
                $"{newAccountQuestion}", 
                ButtonEnum.YesNo, 
                Icon.Plus);

            messageBoxStandardWindow.SetMessageStartupLocation(Avalonia.Controls.WindowStartupLocation.CenterOwner);
            var btnRsult = await messageBoxStandardWindow.ShowDialog(_mainWindow);

            if (btnRsult == ButtonResult.Yes)
            {
                StringBuilder additionalData = null;
                if (!string.IsNullOrEmpty(serverResponse.SIN))
                {
                    additionalData = new StringBuilder();
                    byte[] ids = SQRL.CreateIndexedSecret(this.Site, AltID, result.Item2, Encoding.UTF8.GetBytes(serverResponse.SIN));
                    additionalData.AppendLineWindows($"ins={Sodium.Utilities.BinaryToBase64(ids, Utilities.Base64Variant.UrlSafeNoPadding)}");
                }
                serverResponse = SQRL.GenerateNewIdentCommand(serverResponse.NewNutURL, siteKvp, serverResponse.FullServerRequest, result.Item3, sqrlOpts, additionalData);
                if (!serverResponse.CommandFailed)
                {
                    if (_sqrlInstance.cps.PendingResponse)
                    {
                        _sqrlInstance.cps.cpsBC.Add(new Uri(serverResponse.SuccessUrl));
                    }
                    while (_sqrlInstance.cps.PendingResponse)
                        ;
                    _mainWindow.Close();
                }
            }
            else
            {
                if (_sqrlInstance.cps.PendingResponse)
                {
                    _sqrlInstance.cps.cpsBC.Add(_sqrlInstance.cps.Can);
                }
                while (_sqrlInstance.cps.PendingResponse)
                    ;
                _mainWindow.Close();
            }

            return serverResponse;
        }

        private Dictionary<byte[], Tuple<byte[], KeyPair>> GeneratePriorKeyInfo(Tuple<bool, byte[], byte[]> result, Dictionary<byte[], Tuple<byte[], KeyPair>> priorKvps)
        {
            if (_identityManager.CurrentIdentity.Block3 != null && _identityManager.CurrentIdentity.Block3.Edition > 0)
            {
                byte[] decryptedBlock3 = SQRL.DecryptBlock3(result.Item2, _identityManager.CurrentIdentity, out bool allGood);
                List<byte[]> oldIUKs = new List<byte[]>();
                if (allGood)
                {
                    int skip = 0;
                    int ct = 0;
                    while (skip < decryptedBlock3.Length)
                    {
                        oldIUKs.Add(decryptedBlock3.Skip(skip).Take(32).ToArray());
                        skip += 32;
                        ;
                        if (++ct >= 3)
                            break;
                    }

                    SQRL.ZeroFillByteArray(ref decryptedBlock3);
                    priorKvps = SQRL.CreatePriorSiteKeys(oldIUKs, this.Site, AltID);
                    oldIUKs.Clear();
                }
            }

            return priorKvps;
        }
    }
}
