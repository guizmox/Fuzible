using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static FuzibleFramework.SQLTools_Enums;

namespace FuzibleFramework
{
    internal class ThreadFunc
    {
        internal Task Thread;
        internal MethodBase Function;
    }
    public class Monitoring
    {
        private static bool _bLocker = false;
        private static List<ThreadFunc> _tListThreads = new();
        private static CancellationTokenSource _ctsTaskCancel = new();
        private static System.Timers.Timer _timerHB = null;
        private static int _timerMin = 0;
        public static event EventHandler<int> OnHeartBeat;
        private static LogTools _log;

        public static CancellationToken TaskCancellationToken
        {
            get
            {
                return _ctsTaskCancel.Token;
            }
        }
        public static int GetQteThreads
        {
            get
            {
                int iQteThreads = 0;
                if (!_bLocker)
                {
                    lock (_tListThreads)
                    {
                        int iQte = _tListThreads.Count;
                        for (int iT = 0; iT < iQte; iT++)
                        {
                            if (!_tListThreads[iT].Thread.IsCompleted) { iQteThreads++; }
                        }
                    }
                }
                return iQteThreads;
            }
        }

        internal static string GetThreadsInfo()
        {
            try
            {
                string sData = "";
                List<string> th = new List<string>();
                List<int> thi = new List<int>();
                int it = 0;

                if (!_bLocker)
                {
                    lock (_tListThreads)
                    {
                        for (int iT = 0; iT < _tListThreads.Count; iT++)
                        {
                            if (!_tListThreads[iT].Thread.IsCompleted)
                            {
                                it++;
                                int iDX = th.IndexOf(_tListThreads[iT].Function.Name);
                                if (iDX > -1)
                                {
                                    th[iDX] = _tListThreads[iT].Function.Name;
                                    thi[iDX] += 1;
                                }
                                else
                                {
                                    th.Add(_tListThreads[iT].Function.Name);
                                    thi.Add(1);
                                }
                            }
                        }
                    }
                }

                sData = string.Concat("Thread(s) : ", it.ToString(), " [");
                for (int i = 0; i < th.Count; i++)
                {
                    sData = string.Concat(sData, th[i].ToString(), "=", thi[i].ToString(), ",");
                }
                sData = string.Concat(sData[0..^1], "]");
                return sData;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string InformationsRunningThreads
        {
            get
            {
                StringBuilder sbI = new();
                if (!_bLocker)
                {
                    string sName;
                    string sPriority;
                    string sState;
                    string sContext;

                    lock (_tListThreads)
                    {
                        foreach (var t in _tListThreads)
                        {
                            if (!t.Thread.IsCompleted)
                            {
                                try
                                {
                                    FieldInfo fieldInfo = typeof(Task).GetField("m_action", BindingFlags.NonPublic | BindingFlags.Instance);
                                    object value = fieldInfo.GetValue(t);
                                    sName = ((Action)value).Method.Name;
                                    sPriority = "N/A";
                                    sState = t.Thread.Status.ToString();
                                    sContext = t.Thread.Id.ToString();
                                    sbI.AppendLine(string.Concat("Name: ", sName, " - Priority:", sPriority, " - State:", sState, " - ID:", sContext));
                                }
                                catch (Exception)
                                { //TODO
                                }
                            }
                        }
                    }
                }
                return sbI.ToString();
            }
        }

        public static void AddThread(Task th, MethodBase sMethod)
        {
            if (!_bLocker)
            {
                //if (th.Status == TaskStatus.WaitingForActivation)
                //{ th.Start(); }
                lock (_tListThreads)
                {
                    _tListThreads.Add(new ThreadFunc { Thread = th, Function = sMethod });
                }
            }
        }

        public static void KillAllRunningThreads()
        {
            _bLocker = true;

            //foreach (Thread t in _tListThreads)
            //{
            //    t.Abort();
            //}
            _ctsTaskCancel.Cancel();
            _tListThreads = _tListThreads.Where(t => !t.Thread.IsCompleted).ToList();

            bool bAllKilled = false;
            while (!bAllKilled)
            {
                Thread.Sleep(100);
                int iCountStopped = 0;
                foreach (var t in _tListThreads)
                {
                    if (t.Thread.IsCompleted)
                    { iCountStopped++; }
                    else
                    {
                        var exc = t.Thread.Exception;
                    }
                }
                if (iCountStopped == _tListThreads.Count) // je mets un -1 car on ne veut pas kicker le thread principal
                { bAllKilled = true; }
            }

            _tListThreads = new List<ThreadFunc>();
            _ctsTaskCancel = new CancellationTokenSource();
            _bLocker = false;
        }

        public static void StartStopProgram(LogTools myLog)
        {
            _log = myLog;
            _timerMin = 0;

            if (_timerHB != null)
            {
                _timerHB.Enabled = false;
                _timerHB.Stop();
                _timerHB = null;
            }
            else
            {
                _timerHB = new System.Timers.Timer(60 * 1000);
                _timerHB.Elapsed += Event_HeartBeat;
                _timerHB.Enabled = true;
            }

        }

        private static void Event_HeartBeat(object sender, ElapsedEventArgs e)
        {
            _timerMin++;
            string sThreads = GetThreadsInfo();
            string sMessage = string.Concat(Languages.Languages.monitoring_heartbeat, " : ", sThreads, " - ", Languages.Languages.monitoring_heartbeat_runtime, " : ", _timerMin, " min. - LOG Err : ", _log.JobErrors.ToString(), " - LOG Wng : ", _log.JobWarnings.ToString());
            _log.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), SQLTools_Enums.CLASS_PURPOSE.PRG, null, sMessage, LOG_TYPEINFO.INF);
        }

        public static bool IsRelease(Assembly assembly)
        {
            object[] attributes = assembly.GetCustomAttributes(typeof(DebuggableAttribute), true);
            if (attributes == null || attributes.Length == 0)
            {
                return true;
            }

            var d = (DebuggableAttribute)attributes[0];
            if ((d.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.None)
            {
                return true;
            }

            return false;
        }

        public static bool IsDebug(Assembly assembly)
        {
            object[] attributes = assembly.GetCustomAttributes(typeof(DebuggableAttribute), true);
            if (attributes == null || attributes.Length == 0)
            {
                return true;
            }

            var d = (DebuggableAttribute)attributes[0];
            if (d.IsJITTrackingEnabled)
            {
                return true;
            }

            return false;
        }

        public static string GetThreadsCPUConsumption
        {
            get
            {
                StringBuilder sbCPU = new();
                var p = Process.GetCurrentProcess(); // getting current running process of the app
                string sCalcCPU;
                long sTimeUser;
                long sTimeTotal;
                foreach (ProcessThread pt in p.Threads)
                {
                    sTimeUser = pt.UserProcessorTime.Ticks;
                    sTimeTotal = pt.PrivilegedProcessorTime.Ticks;
                    if (sTimeTotal == 0)
                    { sCalcCPU = "0"; }
                    else
                    { sCalcCPU = (sTimeUser / sTimeTotal).ToString(); }
                    sbCPU.AppendLine(string.Concat("ID:", pt.Id, " - Time:", sCalcCPU));
                    // use pt.Id / pt.TotalProcessorTime / pt.UserProcessorTime / pt.PrivilegedProcessorTime
                }

                return sbCPU.ToString();
            }
        }

        public static string GetBuildDate(Assembly assembly)
        {
            string location = assembly.Location;
            const int headerOffset = 60;
            const int linkerTimestampOffset = 8;
            System.Byte[] buffer = new byte[2048];
            Stream stream = null;

            try
            {
                stream = new FileStream(location, FileMode.Open, FileAccess.Read);
                stream.Read(buffer, 0, 2048);
            }
            finally
            {
                stream?.Close();
            }

            int i = BitConverter.ToInt32(buffer, headerOffset);
            int secondsSince1970 = BitConverter.ToInt32(buffer, i + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.AddHours(TimeZoneInfo.Local.GetUtcOffset(dt).Hours);
            return dt.ToString("yyyyMMdd-HHmm");
        }

        public static string GetLocalIPAddress()
        {
            string sIP = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    sIP = ip.ToString();
                    break;
                }
            }

            return sIP;
        }

        public static void CancelJob()
        {
            _ctsTaskCancel.Cancel();
        }

        public static void RenewCancellationToken()
        {
            _ctsTaskCancel = new CancellationTokenSource();
        }
    }
}
