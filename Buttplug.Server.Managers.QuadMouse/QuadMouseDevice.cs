using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Buttplug.Core;
using Buttplug.Core.Messages;
using System.Threading;

namespace Buttplug.Server.Managers.QuadMouse
{
    public class QuadMouseDevice : ButtplugDevice
    {
        private SerialPort _serialPort;
        private Thread _reader;
        private object _movementLock;

        private Task _posThread;
        private CancellationTokenSource _tokenSource;

        public class Vector
        {
            public int X;
            public int Y;

            public Vector(int aX, int aY)
            {
                X = aX;
                Y = aY;
            }
        }

        private Dictionary<uint, Vector> _last = new Dictionary<uint, Vector>();

        public QuadMouseDevice(SerialPort port, IButtplugLogManager aLogManager, string name, string id)
            : base(aLogManager, name, id)
        {
            _movementLock = new object();

            // Handshake with the box
            _serialPort = port;

            // We're now ready to receive events
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));
            MsgFuncs.Add(typeof(StartMovementCmd), new ButtplugDeviceWrapper(HandleStartMovementCmd));
            MsgFuncs.Add(typeof(StopMovementCmd), new ButtplugDeviceWrapper(HandleStopMovementCmd));

            _last.Add(0, new Vector(0, 0));
            _last.Add(1, new Vector(0, 0));
            _last.Add(2, new Vector(0, 0));
            _last.Add(3, new Vector(0, 0));

            _tokenSource = new CancellationTokenSource();
            _posThread = new Task(() => { deltaReader(_tokenSource.Token); }, _tokenSource.Token, TaskCreationOptions.LongRunning);
            _posThread.Start();
        }

        private void deltaReader(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string sub, line;
                try
                {
                    line = _serialPort.ReadLine();
                    if (!line.StartsWith("0:"))
                    {
                        continue;
                    }

                    foreach (var s in line.Split(';'))
                    {
                        sub = s;
                        var cl = s.IndexOf(':');
                        var cm = s.IndexOf(',');
                        if (s.Length == 0 || cl == -1 || cl != s.LastIndexOf(':') || cm == -1 || cm != s.LastIndexOf(','))
                        {
                            continue;
                        }

                        var sId = Convert.ToUInt32(s.Substring(0, cl));
                        var sx = Convert.ToInt32(s.Substring(cl + 1, cm - (cl + 1)));
                        var sy = Convert.ToInt32(s.Substring(cm + 1));

                        lock (_movementLock)
                        {
                            if (sx == 0 && sy == 0 && _last[sId].X == 0 && _last[sId].Y == 0)
                            {
                                continue;
                            }

                            _last[sId].X = sx;
                            _last[sId].Y = sy;
                            EmitMessage(new MovementData(sx, sy, 0, sId, Index));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e.Message);
                }
            }
        }

        /// Reset the box to defaults when application closes
        public override void Disconnect()
        {
            _tokenSource.Cancel();
            _serialPort.Close();
            InvokeDeviceRemoved();
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleStopMovementCmd(ButtplugDeviceMessage aMsg)
        {
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleStartMovementCmd(ButtplugDeviceMessage aMsg)
        {
            return new Ok(aMsg.Id);
        }
    }
}
