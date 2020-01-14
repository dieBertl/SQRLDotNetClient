﻿using SQRLUtilsLib;
using System;
using System.IO;
using System.Text;
using static SQRLUtilsLib.SQRLOptions;

namespace SQRLConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            SQRLUtilsLib.SQRL sqrl = new SQRLUtilsLib.SQRL(true);

            SQRLIdentity newId = SQRL.ImportSqrlIdentityFromFile(Path.Combine(Directory.GetCurrentDirectory(), @"Spec-Vectors-Identity.sqrl"));
            SQRLOptions opts = new SQRLOptions(SQRLOpts.SUK | SQRLOpts.NOIPTEST | SQRLOpts.CPS);
            bool run =true;
            do
            {
                Console.WriteLine("Enter SQRL URL:");
                string url = Console.ReadLine();
                Uri requestURI = new Uri(url);
                string AltID = "";

                sqrl.DecryptBlock1(newId, "Zingo-Bingo-Slingo-Dingo", out byte[] imk, out byte[] ilk);
                var siteKvp = sqrl.CreateSiteKey(requestURI, AltID, imk);
                SQRL.ZeroFillByteArray(imk);
                var serverRespose = sqrl.GenerateQueryCommand(requestURI, siteKvp, opts);
                if (!serverRespose.CommandFailed)
                {
                    if (!serverRespose.CurrentIDMatch)
                    {
                        Console.WriteLine("The site doesn't recognize this ID, would you like to proceed and create one? (Y/N)");
                        if (Console.ReadLine().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            var sukvuk = sqrl.GetSukVuk(ilk);
                            SQRL.ZeroFillByteArray(ilk);
                            StringBuilder addClientData = new StringBuilder();
                            addClientData.AppendLineWindows($"suk={Sodium.Utilities.BinaryToBase64(sukvuk.Key, Sodium.Utilities.Base64Variant.UrlSafeNoPadding)}");
                            addClientData.AppendLineWindows($"vuk={Sodium.Utilities.BinaryToBase64(sukvuk.Value, Sodium.Utilities.Base64Variant.UrlSafeNoPadding)}");

                            serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "ident", opts, addClientData);
                        }
                    }
                    else if (serverRespose.CurrentIDMatch)
                    {
                        int askResponse = 0;
                        if (serverRespose.HasAsk)
                        {
                            Console.WriteLine(serverRespose.AskMessage);
                            Console.WriteLine($"Enter 1 for {serverRespose.GetAskButtons[0]} or 2 for {serverRespose.GetAskButtons[1]}");
                            int resp;
                            do
                            {
                                string response = Console.ReadLine();
                                int.TryParse(response, out resp);

                                if (resp == 0)
                                {
                                    Console.WriteLine("Invalid Entry, please enter 1 or 2 as shown above");
                                }

                            } while (resp == 0);
                            askResponse = resp;

                        }

                        StringBuilder addClientData = null;
                        if (askResponse > 0)
                        {
                            addClientData = new StringBuilder();
                            addClientData.AppendLineWindows($"btn={askResponse}");
                        }

                        if (serverRespose.SQRLDisabled)
                        {
                            Console.WriteLine("SQRL Is Disabled, to Continue you must enable it. Do you want to? (Y/N)");
                            if (Console.ReadLine().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("Enter your Rescue Code (No Sapces or Dashes)");
                                string rescueCode = Console.ReadLine().Trim();
                                sqrl.DecryptBlock2(newId, rescueCode, out byte[] iuk);
                                byte[] ursKey = null;
                                ursKey = sqrl.GetURSKey(iuk, Sodium.Utilities.Base64ToBinary(serverRespose.SUK, string.Empty, Sodium.Utilities.Base64Variant.UrlSafeNoPadding));

                                serverRespose = sqrl.GenerateCommandWithURS(serverRespose.NewNutURL, siteKvp, ursKey, serverRespose.FullServerRequest, "enable", opts, null);

                            }
                        }

                        Console.WriteLine("Which Command Would you like to Issue?:");
                        Console.WriteLine("*********************************************");
                        Console.WriteLine("0- Ident (Default/Enter) ");
                        Console.WriteLine("1- Disable ");
                        Console.WriteLine("2- Remove ");
                        Console.WriteLine("3- Cancel ");
                        Console.WriteLine("10- Quit ");
                        Console.WriteLine("*********************************************");
                        var value = Console.ReadLine();
                        int.TryParse(value, out int selection);

                        switch (selection)
                        {
                            case 0:
                                {
                                    serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "ident", opts, addClientData);
                                    if (sqrl.cps != null && sqrl.cps.PendingResponse)
                                    {
                                        sqrl.cps.cpsBC.Add(new Uri(serverRespose.SuccessUrl));
                                    }
                                }
                                break;
                            case 1:
                                {
                                    Console.WriteLine("This will disable all use of this SQRL Identity on the server, are you sure you want to proceed?: (Y/N)");
                                    if (Console.ReadLine().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                                    {
                                        serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "disable", opts, addClientData);
                                        if (sqrl.cps != null && sqrl.cps.PendingResponse)
                                        {
                                            sqrl.cps.cpsBC.Add(sqrl.cps.Can);
                                        }
                                    }

                                }
                                break;
                            case 2:
                                {
                                    Console.WriteLine("Enter your Rescue Code (No Sapces or Dashes)");
                                    string rescueCode = Console.ReadLine().Trim();
                                    sqrl.DecryptBlock2(newId, rescueCode, out byte[] iuk);
                                    byte[] ursKey = sqrl.GetURSKey(iuk, Sodium.Utilities.Base64ToBinary(serverRespose.SUK, string.Empty, Sodium.Utilities.Base64Variant.UrlSafeNoPadding));

                                    serverRespose = sqrl.GenerateCommandWithURS(serverRespose.NewNutURL, siteKvp, ursKey, serverRespose.FullServerRequest, "remove", opts, null);
                                    if (sqrl.cps != null && sqrl.cps.PendingResponse)
                                    {
                                        sqrl.cps.cpsBC.Add(sqrl.cps.Can);
                                    }
                                }
                                break;
                            case 3:
                                {
                                    if (sqrl.cps != null && sqrl.cps.PendingResponse)
                                    {
                                        sqrl.cps.cpsBC.Add(sqrl.cps.Can);
                                    }
                                }
                                break;
                            default:
                                {
                                    Console.WriteLine("bye");
                                    run = false;
                                }
                                break;
                        }
                    }
                }
            } while (run);
        }
    }
}
