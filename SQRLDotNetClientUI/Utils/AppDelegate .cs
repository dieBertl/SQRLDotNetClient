﻿using Avalonia.Controls;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using MonoMac.AppKit;
using MonoMac.Foundation;
using SQRLDotNetClientUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace SQRLDotNetClientUI.Utils
{
    /// <summary>
    /// This class is an AppDelegate helper specifically for Mac OSX
    /// Int it's infinite wisdom and unlike Linux and or Windows Mac does not pass in the URL from a sqrl:// invokation 
    /// directly as a startup app paramter, instead it uses a System Event to do this which has to be registered
    /// and listed to.
    /// This requires us to use MonoMac to make it work with .net core
    /// </summary>
    [Register("AppDelegate")]
    class AppDelegate : NSApplicationDelegate
    {
   
        // Instance Window of our App
        Window mainWindow = null;
        public AppDelegate(Window mainWindow)
        {
            this.mainWindow = mainWindow;
            Init();
        }
        public AppDelegate()
        {
            Init();
        }

        /// <summary>
        /// Registers an event for handling URL Invokation
        /// </summary>
        private void Init()
        {
            NSAppleEventManager.SharedAppleEventManager.SetEventHandler(this, new MonoMac.ObjCRuntime.Selector("handleGetURLEvent:withReplyEvent:"), AEEventClass.Internet, AEEventID.GetUrl);
        }
        /// <summary>
        /// This handles the URL invokation System Event when an App is launched  with the
        /// sqrl:// schema from a URL
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="replyEvent"></param>
        [Export("handleGetURLEvent:withReplyEvent:")]
        private void HandleOpenURL(NSAppleEventDescriptor evt, NSAppleEventDescriptor replyEvent)
        {
            for (int i = 1; i <= evt.NumberOfItems; i++)
            {
                var innerDesc = evt.DescriptorAtIndex(i);

                //Grab the URL
                if (!string.IsNullOrEmpty(innerDesc.StringValue))
                {
                    //Get a hold of the Main Application View Model
                    var mwvm = (MainWindowViewModel)this.mainWindow.DataContext;

                    //Get a hold of the currently loaded Model (main menu)
                    if(mwvm.Content.GetType()==typeof(MainMenuViewModel))
                    {
                        var mmvm = mwvm.Content as MainMenuViewModel;
                        //If there is a Loaded Identity then Invoke the Authentication Dialog
                        if(mmvm.CurrentIdentity !=null)
                        {
                            mmvm.AuthVM = new AuthenticationViewModel(mmvm.sqrlInstance, mmvm.CurrentIdentity, new Uri(innerDesc.StringValue));
                            mwvm.PriorContent = mwvm.Content;
                            mwvm.Content = mmvm.AuthVM;
                        }

                    }
                  
                }
            }

        }
    }
}