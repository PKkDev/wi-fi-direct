using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WiFiDirect.Shared;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Networking.Sockets;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace WiFiDirect.Hub
{
    public sealed partial class MainWindow : Window
    {
        WiFiDirectAdvertisementPublisher _publisher;
        WiFiDirectConnectionListener _listener;

        List<WiFiDirectInformationElement> _informationElements = new();

        public ObservableCollection<ConnectedDevice> ConnectedDevices = new();
        ConcurrentDictionary<StreamSocketListener, WiFiDirectDevice> _pendingConnections = new();


        public MainWindow()
        {
            InitializeComponent();

            _informationElements = new();
            AddWiFiDirectInformationElement("tetElement");
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            _listener = new WiFiDirectConnectionListener();

            try
            {
                // This can raise an exception if the machine does not support WiFi. Sorry.
                _listener.ConnectionRequested += OnConnectionRequested; ;
            }
            catch (Exception ex)
            {
            }

            _publisher = new WiFiDirectAdvertisementPublisher();
            _publisher.StatusChanged += PublisherStatusChanged;

            #region Legacy settings HostPot

            _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

            // Legacy settings are meaningful only if IsAutonomousGroupOwnerEnabled is true.
            if (_publisher.Advertisement.IsAutonomousGroupOwnerEnabled)
            {
                _publisher.Advertisement.LegacySettings.IsEnabled = true;

                _publisher.Advertisement.LegacySettings.Ssid = "mytestssid";

                PasswordCredential lCred = new();
                lCred.Password = "12345678";
                _publisher.Advertisement.LegacySettings.Passphrase = lCred;
            }

            #endregion Legacy settings HostPot

            // Add the information elements.
            foreach (var informationElement in _informationElements)
            {
                _publisher.Advertisement.InformationElements.Add(informationElement);
            }

            _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;

            _publisher.Start();

            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                // Advertisement started.
            }
            else
            {
                // Advertisement failed to start. Status is {_publisher.Status}
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _publisher.Stop();
            _publisher.StatusChanged -= PublisherStatusChanged;

            _listener.ConnectionRequested -= OnConnectionRequested;
        }

        private void OnConnectionRequested(
            WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
        {
            WiFiDirectConnectionRequest connectionRequest = args.GetConnectionRequest();

            DispatcherQueue.TryEnqueue(async () =>
            {
                var success = await HandleConnectionRequestAsync(connectionRequest);

                if (!success)
                {
                    // Decline the connection request
                    connectionRequest.Dispose();
                }
            });
        }

        private readonly string strServerPort = "50001";
        private async Task<bool> HandleConnectionRequestAsync(WiFiDirectConnectionRequest connectionRequest)
        {
            string deviceName = connectionRequest.DeviceInformation.Name;

            bool isPaired = (connectionRequest.DeviceInformation.Pairing?.IsPaired == true) ||
                            (await IsAepPairedAsync(connectionRequest.DeviceInformation.Id));

            // Show the prompt only in case of WiFiDirect reconnection or Legacy client connection.
            if (isPaired || _publisher.Advertisement.LegacySettings.IsEnabled)
            {
                var messageDialog = new MessageDialog($"Connection request received from {deviceName}", "Connection Request");

                // Add two commands, distinguished by their tag.
                // The default command is "Decline", and if the user cancels, we treat it as "Decline".
                messageDialog.Commands.Add(new UICommand("Accept", null, true));
                messageDialog.Commands.Add(new UICommand("Decline", null, null));
                messageDialog.DefaultCommandIndex = 1;
                messageDialog.CancelCommandIndex = 1;

                // Show the message dialog
                var commandChosen = await messageDialog.ShowAsync();

                if (commandChosen.Id == null)
                {
                    return false;
                }
            }

            // Pair device if not already paired and not using legacy settings
            if (!isPaired && !_publisher.Advertisement.LegacySettings.IsEnabled)
            {
                var requestPairing = connectionRequest.DeviceInformation.Pairing;

                if (!await RequestPairDeviceAsync(requestPairing))
                {
                    return false;
                }
            }

            WiFiDirectDevice wfdDevice = null;
            try
            {
                // IMPORTANT: FromIdAsync needs to be called from the UI thread
                wfdDevice = await WiFiDirectDevice.FromIdAsync(connectionRequest.DeviceInformation.Id);
            }
            catch (Exception ex)
            {
                return false;
            }

            // Register for the ConnectionStatusChanged event handler
            wfdDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            var listenerSocket = new StreamSocketListener();

            // Save this (listenerSocket, wfdDevice) pair so we can hook it up when the socket connection is made.
            _pendingConnections[listenerSocket] = wfdDevice;

            var EndpointPairs = wfdDevice.GetConnectionEndpointPairs();

            listenerSocket.ConnectionReceived += OnSocketConnectionReceived;
            try
            {
                await listenerSocket.BindEndpointAsync(EndpointPairs[0].LocalHostName, strServerPort);
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
        private async Task<bool> IsAepPairedAsync(string deviceId)
        {
            List<string> additionalProperties = new List<string>();
            additionalProperties.Add("System.Devices.Aep.DeviceAddress");
            String deviceSelector = $"System.Devices.Aep.AepId:=\"{deviceId}\"";
            DeviceInformation devInfo = null;

            try
            {
                devInfo = await DeviceInformation.CreateFromIdAsync(deviceId, additionalProperties);
            }
            catch (Exception ex)
            {
            }

            if (devInfo == null)
            {
                return false;
            }

            deviceSelector = $"System.Devices.Aep.DeviceAddress:=\"{devInfo.Properties["System.Devices.Aep.DeviceAddress"]}\"";
            DeviceInformationCollection pairedDeviceCollection = await DeviceInformation.FindAllAsync(deviceSelector, null, DeviceInformationKind.Device);
            return pairedDeviceCollection.Count > 0;
        }
        private async Task<bool> RequestPairDeviceAsync(DeviceInformationPairing pairing)
        {
            WiFiDirectConnectionParameters connectionParams = new();
            DeviceInformationCustomPairing customPairing = pairing.Custom;

            DevicePairingKinds devicePairingKinds = DevicePairingKinds.None;

            // If specific configuration methods were not added, then we'll use these pairing kinds.
            devicePairingKinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin | DevicePairingKinds.ProvidePin;

            DevicePairingResult result = await customPairing.PairAsync(
                devicePairingKinds,
                DevicePairingProtectionLevel.Default,
                connectionParams);
            if (result.Status != DevicePairingResultStatus.Paired)
            {
                return false;
            }

            return true;
        }
        private void OnSocketConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                StreamSocket serverSocket = args.Socket;

                // Look up the WiFiDirectDevice associated with this StreamSocketListener.
                WiFiDirectDevice wfdDevice;
                if (!_pendingConnections.TryRemove(sender, out wfdDevice))
                {
                    serverSocket.Dispose();
                    return;
                }

                SocketReaderWriter socketRW = new SocketReaderWriter(serverSocket);

                // The first message sent is the name of the connection.
                string message = await socketRW.ReadMessageAsync();

                // Add this connection to the list of active connections.
                ConnectedDevices.Add(new ConnectedDevice(message ?? "(unnamed)", wfdDevice, socketRW));

                while (message != null)
                {
                    message = await socketRW.ReadMessageAsync();
                }
            });
        }

        private void OnConnectionStatusChanged(WiFiDirectDevice sender, object arg)
        {
            if (sender.ConnectionStatus == WiFiDirectConnectionStatus.Disconnected)
            {
                // TODO: Should we remove this connection from the list?
                // (Yes, probably.)
            }
        }


        private void PublisherStatusChanged(
            WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
        {
            switch (args.Status)
            {
                case WiFiDirectAdvertisementPublisherStatus.Started:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Created:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Stopped:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Aborted:
                    {
                        break;
                    }
            }
        }

        // WARNING! This custom OUI is for demonstration purposes only.
        // OUI values are assigned by the IEEE Registration Authority.
        // Replace this custom OUI with the value assigned to your organization.
        private readonly byte[] CustomOui = { 0xAA, 0xBB, 0xCC };
        private readonly byte CustomOuiType = 0xDD;
        private void AddWiFiDirectInformationElement(string txtInformationElement)
        {
            WiFiDirectInformationElement informationElement = new WiFiDirectInformationElement();

            // Information element blob
            DataWriter dataWriter = new DataWriter();
            dataWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
            dataWriter.ByteOrder = ByteOrder.LittleEndian;
            dataWriter.WriteUInt32(dataWriter.MeasureString(txtInformationElement));
            dataWriter.WriteString(txtInformationElement);
            informationElement.Value = dataWriter.DetachBuffer();

            // Organizational unit identifier (OUI)
            informationElement.Oui = CryptographicBuffer.CreateFromByteArray(CustomOui);

            // OUI Type
            informationElement.OuiType = CustomOuiType;

            // Save this information element so we can add it when we advertise.
            _informationElements.Add(informationElement);
        }

        private async void SendMessageBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null) return;
            //await connectedDevice.SocketRW.WriteMessageAsync(txtSendMessage.Text);
        }

        private void CloseDeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null) return;

            ConnectedDevices.Remove(connectedDevice);

            // Close socket and WiFiDirect object
            connectedDevice.Dispose();
        }
    }
}
