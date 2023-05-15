using System;
using Windows.Devices.WiFiDirect;

namespace WiFiDirect.Shared
{
    public class ConnectedDevice : IDisposable
    {
        public SocketReaderWriter SocketRW { get; }
        public WiFiDirectDevice WfdDevice { get; }
        public string DisplayName { get; }

        public ConnectedDevice(string displayName, WiFiDirectDevice wfdDevice, SocketReaderWriter socketRW)
        {
            DisplayName = displayName;
            WfdDevice = wfdDevice;
            SocketRW = socketRW;
        }

        public override string ToString() => DisplayName;

        public void Dispose()
        {
            // Close socket
            SocketRW.Dispose();

            // Close WiFiDirectDevice object
            WfdDevice.Dispose();
        }
    }
}
