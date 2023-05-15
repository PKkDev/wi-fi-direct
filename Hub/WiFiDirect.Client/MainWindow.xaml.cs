using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WiFiDirect.Shared;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Networking.Sockets;
using Windows.Networking;
using System.IO;

namespace WiFiDirect.Client
{
    public sealed partial class MainWindow : Window
    {
        DeviceWatcher _deviceWatcher = null;

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; set; }
        public ObservableCollection<ConnectedDevice> ConnectedDevices { get; set; }

        public MainWindow()
        {
            DiscoveredDevices = new();
            ConnectedDevices = new();

            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            DiscoveredDevices.Clear();

            // AssociationEndpoint
            string deviceSelector = "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{0407d24e-53de-4c9a-9ba1-9ced54641188}\" AND System.Devices.Aep.IsPresent:=System.StructuredQueryType.Boolean#True";
            // DeviceInterface
            //string deviceSelector = "System.Devices.InterfaceClassGuid:=\"{439B20AF-8955-405B-99F0-A62AF0C68D43}\" AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";
            _deviceWatcher = DeviceInformation.CreateWatcher(
                deviceSelector,
                new string[] { "System.Devices.WiFiDirect.InformationElements" });

            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
            _deviceWatcher.Stopped += OnStopped;

            _deviceWatcher.Start();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _deviceWatcher.Added -= OnDeviceAdded;
            _deviceWatcher.Removed -= OnDeviceRemoved;
            _deviceWatcher.Updated -= OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
            _deviceWatcher.Stopped -= OnStopped;

            _deviceWatcher.Stop();

            _deviceWatcher = null;
        }

        #region DeviceWatcherEvents

        private async void OnDeviceAdded(DeviceWatcher deviceWatcher, DeviceInformation deviceInfo)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DiscoveredDevices.Add(new DiscoveredDevice(deviceInfo));
            });
        }

        private async void OnDeviceRemoved(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInfoUpdate)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (DiscoveredDevice discoveredDevice in DiscoveredDevices)
                {
                    if (discoveredDevice.DeviceInfo.Id == deviceInfoUpdate.Id)
                    {
                        DiscoveredDevices.Remove(discoveredDevice);
                        break;
                    }
                }
            });
        }

        private async void OnDeviceUpdated(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInfoUpdate)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (DiscoveredDevice discoveredDevice in DiscoveredDevices)
                {
                    if (discoveredDevice.DeviceInfo.Id == deviceInfoUpdate.Id)
                    {
                        discoveredDevice.UpdateDeviceInfo(deviceInfoUpdate);
                        break;
                    }
                }
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher deviceWatcher, object o)
        {

        }

        private void OnStopped(DeviceWatcher deviceWatcher, object o)
        {

        }

        #endregion DeviceWatcherEvents

        private readonly string strServerPort = "50001";
        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            var discoveredDevice = (DiscoveredDevice)lvDiscoveredDevices.SelectedItem;

            if (discoveredDevice == null) return;


            if (!discoveredDevice.DeviceInfo.Pairing.IsPaired)
            {
                if (!await RequestPairDeviceAsync(discoveredDevice.DeviceInfo.Pairing))
                {
                    return;
                }
            }

            WiFiDirectDevice wfdDevice = null;
            try
            {
                // IMPORTANT: FromIdAsync needs to be called from the UI thread
                wfdDevice = await WiFiDirectDevice.FromIdAsync(discoveredDevice.DeviceInfo.Id);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            // Register for the ConnectionStatusChanged event handler
            wfdDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            IReadOnlyList<EndpointPair> endpointPairs = wfdDevice.GetConnectionEndpointPairs();
            HostName remoteHostName = endpointPairs[0].RemoteHostName;

            // Wait for server to start listening on a socket
            await Task.Delay(2000);

            // Connect to Advertiser on L4 layer
            StreamSocket clientSocket = new StreamSocket();
            try
            {
                await clientSocket.ConnectAsync(remoteHostName, strServerPort);
            }
            catch (Exception ex)
            {
                return;
            }

            SocketReaderWriter socketRW = new SocketReaderWriter(clientSocket);

            string sessionId = Path.GetRandomFileName();
            ConnectedDevice connectedDevice = new ConnectedDevice(sessionId, wfdDevice, socketRW);
            ConnectedDevices.Add(connectedDevice);

            // The first message sent over the socket is the name of the connection.
            await socketRW.WriteMessageAsync(sessionId);

            while (await socketRW.ReadMessageAsync() != null)
            {
                // Keep reading messages
            }
        }
        private void OnConnectionStatusChanged(WiFiDirectDevice sender, object arg)
        {
            if (sender.ConnectionStatus == WiFiDirectConnectionStatus.Disconnected)
            {
                // TODO: Should we remove this connection from the list?
                // (Yes, probably.)
            }
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

        private async void SendMessageBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null)
            {
                await connectedDevice.SocketRW.WriteMessageAsync(SendMessageTxt.Text);
            }
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null)
            {
                ConnectedDevices.Remove(connectedDevice);

                // Close socket and WiFiDirect object
                connectedDevice.Dispose();
            }
        }
    }
}
