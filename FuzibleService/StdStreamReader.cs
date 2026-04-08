using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ProcessEventHandler
{
    public class DataReceived : EventArgs
    {
        public string Data { get; set; }
        public Process Process { get; set; }
    }

    public class StdStreamReader
    {
        static readonly int bufferSize = 1024;
        readonly System.Byte[] _buffer = new byte[bufferSize];
        Process _Process = null;
        readonly StringBuilder _DataQueue = new();
        readonly ManualResetEvent _DoneEvent = new(false);

        public event EventHandler<DataReceived> DataReceivedEvent;

        public string Data
        {
            get
            {
                Monitor.Enter(_DataQueue);
                string data = _DataQueue.ToString();
                Monitor.Exit(_DataQueue);
                return data;
            }
        }

        public bool IsDone() { return _DoneEvent.WaitOne(); }
        public bool IsDone(int milliseconds) { return _DoneEvent.WaitOne(milliseconds); }

        public void StartReader(Stream stream, Process process)
        {
            _Process = process;
            stream.BeginRead(_buffer, 0, bufferSize, ReaderCallback, stream);
        }

        public void ReaderCallback(IAsyncResult result)
        {
            if (result != null)
            {
                int count = 0;
                try { count = ((Stream)result.AsyncState).EndRead(result); } catch { count = 0; }
                try
                {
                    if (count > 0)
                    {
                        string x = Encoding.ASCII.GetString(_buffer, 0, count);

                        Monitor.Enter(_DataQueue);
                        _DataQueue.Append(x);
                        if (DataReceivedEvent != null)
                        {
                            DataReceivedEvent(((Stream)result.AsyncState), new DataReceived { Data = _DataQueue.ToString(), Process = _Process });
                            _DataQueue.Clear();
                        }
                        Monitor.Exit(_DataQueue);

                        ((Stream)result.AsyncState).BeginRead(_buffer, 0, bufferSize, ReaderCallback, result.AsyncState);
                    }
                    else
                        _DoneEvent.Set();
                }
                catch
                {
                    Monitor.Exit(_DataQueue);
                }
            }
        }
    }
}
