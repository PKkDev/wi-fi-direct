using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using WiFiDirect.Shared;
using Windows.Devices.Enumeration;

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
    }
}
