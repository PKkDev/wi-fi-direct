using System;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WiFiDirect.Hub
{
    public class SocketReaderWriter : IDisposable
    {
        DataReader _dataReader;
        DataWriter _dataWriter;
        StreamSocket _streamSocket;

        public SocketReaderWriter(StreamSocket socket)
        {
            _dataReader = new DataReader(socket.InputStream);
            _dataReader.UnicodeEncoding = UnicodeEncoding.Utf8;
            _dataReader.ByteOrder = ByteOrder.LittleEndian;

            _dataWriter = new DataWriter(socket.OutputStream);
            _dataWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
            _dataWriter.ByteOrder = ByteOrder.LittleEndian;

            _streamSocket = socket;
        }

        public void Dispose()
        {
            _dataReader.Dispose();
            _dataWriter.Dispose();
            _streamSocket.Dispose();
        }
    }
}
