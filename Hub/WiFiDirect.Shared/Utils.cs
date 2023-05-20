using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace WiFiDirect.Shared
{
    public static class Utils
    {
        private static async Task ShowPinToUserAsync(DispatcherQueue dispatcher, string strPin)
        {
            //await dispatcher.TryEnqueue(async () =>
            //{
            //    var messageDialog = new MessageDialog($"Enter this PIN on the remote device: {strPin}");

            //    // Add commands
            //    messageDialog.Commands.Add(new UICommand("OK", null, 0));

            //    // Set the command that will be invoked by default 
            //    messageDialog.DefaultCommandIndex = 0;

            //    // Set the command that will be invoked if the user cancels
            //    messageDialog.CancelCommandIndex = 0;

            //    // Show the Pin 
            //    await messageDialog.ShowAsync();
            //});
        }

        private static async Task<string> GetPinFromUserAsync(DispatcherQueue dispatcher)
        {
            //return await dispatcher.TryEnqueue(async () =>
            //{
            //    var pinBox = new TextBox();
            //    var dialog = new ContentDialog()
            //    {
            //        Title = "Enter Pin",
            //        PrimaryButtonText = "OK",
            //        Content = pinBox
            //    };
            //    await dialog.ShowAsync();
            //    return pinBox.Text;
            //});
            return "123456";
        }

        public static async void HandlePairing(DispatcherQueue dispatcher, DevicePairingRequestedEventArgs args)
        {
            using Deferral deferral = args.GetDeferral();

            switch (args.PairingKind)
            {
                case DevicePairingKinds.DisplayPin:
                    await ShowPinToUserAsync(dispatcher, args.Pin);
                    args.Accept();
                    break;

                case DevicePairingKinds.ConfirmOnly:
                    args.Accept();
                    break;

                case DevicePairingKinds.ProvidePin:
                    {
                        string pin = await GetPinFromUserAsync(dispatcher);
                        if (!String.IsNullOrEmpty(pin))
                        {
                            args.Accept(pin);
                        }
                    }
                    break;
            }
        }
    }
}
