﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using MonoMac.AppKit;
using MonoMac.Foundation;
using SQRLDotNetClientUI.Models;
using SQRLDotNetClientUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace SQRLDotNetClientUI.Platform.OSX
{
    /// <summary>
    /// This class is an AppDelegate helper specifically for Mac OSX
    /// Int it's infinite wisdom and unlike Linux and or Windows Mac does not pass in the URL from a sqrl:// invokation 
    /// directly as a startup app paramter, instead it uses a System Event to do this which has to be registered
    /// and listed to.
    /// This requires us to use MonoMac to make it work with .net core
    /// </summary>
    [Register("AppDelegate")]
    class AppDelegate : NSApplicationDelegate, INotifyIcon
    {

        NSStatusItem statusBarItem;
        
        // Instance Window of our App
        Window mainWindow = null;

        public event EventHandler<EventArgs> Click;
        public event EventHandler<EventArgs> DoubleClick;
        public event EventHandler<EventArgs> RightClick;

        private string _iconPath = "";
        public string IconPath { get => _iconPath; set => _iconPath=value; }
        public string ToolTipText { get; set;}
        private ContextMenu _menu;
        public ContextMenu ContextMenu { get=> _menu; set {
                _menu = value;
                foreach(var x in _menu.Items.Cast<MenuItem>())
                {
                    NSMenuItem menuItem = new NSMenuItem(x.Header.ToString());
                    menuItem.Activated += (s, e) => { x.Command.Execute(null); };
                }
            } }
        public bool Visible { get; set; }

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
                    if (mwvm.Content.GetType() == typeof(MainMenuViewModel))
                    {
                        var mmvm = mwvm.Content as MainMenuViewModel;
                        //If there is a Loaded Identity then Invoke the Authentication Dialog
                        if (mmvm.CurrentIdentity != null)
                        {
                            mmvm.AuthVM = new AuthenticationViewModel(new Uri(innerDesc.StringValue));
                            mwvm.PriorContent = mwvm.Content;
                            mwvm.Content = mmvm.AuthVM;
                        }

                    }

                }
            }

        }

        public override void DidFinishLaunching(MonoMac.Foundation.NSNotification notification)
        {
            var systemStatusBar = NSStatusBar.SystemStatusBar;
            statusBarItem = systemStatusBar.CreateStatusItem(30);
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();

            statusBarItem.Image = NSImage.FromStream(assets.Open(new Uri("resm:SQRLDotNetClientUI.Assets.SQRL_icon_normal_16.png")));
            statusBarItem.DoubleClick += (s, e) => { DoubleClick?.Invoke(this, new EventArgs()); };
            statusBarItem.ToolTip = this.ToolTipText;
            statusBarItem.Menu = new NSMenu();
            
        }
        

        public void Remove()
        {
            throw new NotImplementedException();
        }
    }
}