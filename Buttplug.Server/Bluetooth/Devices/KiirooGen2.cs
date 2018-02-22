using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using JetBrains.Annotations;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class KiirooGen2BluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Rx = 0,
            Tx,
            Cmd,
        }

        public string[] Names { get; } = { "Onyx2", "Pearl2" };

        public Guid[] Services { get; } = { new Guid("f60402a6-0294-4bdb-9f20-6758133f7090") };

        public Guid[] Characteristics { get; } =
        {
            // rx - 0x0074
            new Guid("d44d0393-0731-43b3-a373-8fc70b1f3323"),

            // tx - 0x0072
            new Guid("02962ac9-e86f-4094-989d-231d69995fc2"),

            // cmd - 0x0077
            new Guid("c7b7a04b-2cc4-40ff-8b10-5d531d1161db"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new KiirooGen2(aLogManager, aInterface, this);
        }
    }

    internal class KiirooGen2 : ButtplugBluetoothDevice
    {
        private readonly object _onyxLock = new object();
        private double _deviceSpeed;
        private double _targetPosition;
        private double _currentPosition;
        private DateTime _targetTime = DateTime.Now;
        private DateTime _currentTime = DateTime.Now;
        private Timer _onyxTimer;

        public KiirooGen2([NotNull] IButtplugLogManager aLogManager,
                      [NotNull] IBluetoothDeviceInterface aInterface,
                      [NotNull] IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Kiiroo {aInterface.Name}",
                   aInterface,
                   aInfo)
        {
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));

            MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleVibrateCmd,
                    new MessageAttributes() { FeatureCount = 1 }));
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
            MsgFuncs.Add(typeof(LinearCmd), new ButtplugDeviceWrapper(HandleLinearCmd,
                    new MessageAttributes() { FeatureCount = 1 }));
        }

        private void OnBluetoothMessageReceived(object sender, BluetoothMessageReceivedEventArgs aArgs)
        {
            BpLogger.Trace($"Kirroo sent data: {BitConverter.ToString(aArgs.Data)}");
        }

        public override async Task<ButtplugMessage> Initialize()
        {
            // Start listening for incoming
            Interface.BluetoothMessageReceived += OnBluetoothMessageReceived;
            await Interface.SubscribeValue(ButtplugConsts.SystemMsgId, Info.Characteristics[(int)KiirooGen1BluetoothInfo.Chrs.Rx]);

            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooGen2BluetoothInfo.Chrs.Cmd],
                new byte[] { 0x05 }, true);

            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooGen2BluetoothInfo.Chrs.Tx],
                new byte[] { 0x00 }, true);

            var res = await Interface.ReadValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooGen2BluetoothInfo.Chrs.Cmd]);
            BpLogger.Trace($"Kirroo read data: {BitConverter.ToString(res.Value)}");

            res = await Interface.ReadValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooGen2BluetoothInfo.Chrs.Tx]);
            BpLogger.Trace($"Kirroo read data: {BitConverter.ToString(res.Value)}");

            if (Interface.Name != "ONYX")
            {
                return new Ok(ButtplugConsts.SystemMsgId);
            }

            // Onyx2 specific
            Interface.DeviceRemoved += (sender, args) =>
            {
                _onyxTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _onyxTimer = null;
            };

            _currentTime = DateTime.Now;
            _onyxTimer = new Timer(OnOnyxTimer, null, 500, 500);

            return new Ok(ButtplugConsts.SystemMsgId);
        }

        public override void Disconnect()
        {
            if (Interface.Name == "ONYX")
            {
                _onyxTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _onyxTimer = null;
            }

            Interface.Disconnect();
        }

        private async void OnOnyxTimer(object state)
        {
            lock (_onyxLock)
            {
                // Given the time since the last iteration and the difference in distance, work out have far we should move
                var distance = Math.Abs(_targetPosition - _currentPosition);
                var oldTime = new DateTime(_currentTime.Ticks);
                var nowTime = DateTime.Now;
                var delta = nowTime.Subtract(oldTime);

                if (Convert.ToUInt32(distance * 4) == 0 && delta.TotalMilliseconds < 500)
                {
                    // Skip. We do want to occationally ping the Onyx, but only every half a second
                    return;
                }

                _currentTime = DateTime.Now;

                if (_currentTime.CompareTo(_targetTime) >= 0 || distance < 0.000001)
                {
                    // We've overdue: jump to target
                    _currentPosition = _targetPosition;
                    _onyxTimer?.Change(500, 500);
                }
                else
                {
                    // The hard part: find the persentage time gone, then add that percentate of the movement delta
                    var delta2 = _targetTime.Subtract(oldTime);

                    var move = Convert.ToDouble(delta.TotalMilliseconds) / (Convert.ToDouble(delta2.TotalMilliseconds) + 1) * distance;
                    _currentPosition += move * (_targetPosition > _currentPosition ? 1 : -1);
                    _currentPosition = Math.Max(0, Math.Min(1, _currentPosition));
                }
            }

            var res = await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooGen2BluetoothInfo.Chrs.Tx],
                new byte[] { Convert.ToByte(_currentPosition * 99), 0x46 }, true);
            if (res is Error err)
            {
                BpLogger.Error(err.ErrorMessage);
            }
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            // Right now, this is a nop. The Onyx doesn't have any sort of permanent movement state,
            // and its longest movement is like 150ms or so. The Pearl is supposed to vibrate but I've
            // never gotten that to work. So for now, we just return ok.
            BpLogger.Debug("Stopping Device " + Name);

            if (Interface.Name == "PEARL" && _deviceSpeed > 0)
            {
                return await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                    Info.Characteristics[(uint)KiirooGen2BluetoothInfo.Chrs.Tx],
                    new byte[] { 0x00, 0x00 }, true);
            }

            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is SingleMotorVibrateCmd cmdMsg) || Interface.Name != "PEARL")
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            if (Math.Abs(_deviceSpeed - cmdMsg.Speed) < 0.001)
            {
                return new Ok(cmdMsg.Id);
            }

            _deviceSpeed = cmdMsg.Speed;

            return await HandleVibrateCmd(new VibrateCmd(cmdMsg.DeviceIndex,
                new List<VibrateCmd.VibrateSubcommand>() { new VibrateCmd.VibrateSubcommand(0, cmdMsg.Speed) },
                cmdMsg.Id));
        }

        private async Task<ButtplugMessage> HandleVibrateCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is VibrateCmd cmdMsg) || Interface.Name != "PEARL")
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            if (cmdMsg.Speeds.Count != 1)
            {
                return new Error(
                    "VibrateCmd requires 1 vector for this device.",
                    Error.ErrorClass.ERROR_DEVICE,
                    cmdMsg.Id);
            }

            foreach (var v in cmdMsg.Speeds)
            {
                if (v.Index != 0)
                {
                    return new Error(
                        $"Index {v.Index} is out of bounds for VibrateCmd for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id);
                }

                _deviceSpeed = v.Speed;
            }

            return await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooGen2BluetoothInfo.Chrs.Tx],
                new byte[] { Convert.ToByte(_currentPosition * 99), 0x46 }, true);
        }

        private Task<ButtplugMessage> HandleLinearCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is LinearCmd cmdMsg) || Interface.Name != "ONYX")
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(
                    aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            if (cmdMsg.Vectors.Count != 1)
            {
                return Task.FromResult<ButtplugMessage>(new Error(
                    "LinearCmd requires 1 vector for this device.",
                    Error.ErrorClass.ERROR_DEVICE,
                    cmdMsg.Id));
            }

            foreach (var v in cmdMsg.Vectors)
            {
                if (v.Index != 0)
                {
                    return Task.FromResult<ButtplugMessage>(new Error(
                        $"Index {v.Index} is out of bounds for LinearCmd for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id));
                }

                // Invert the position
                lock (_onyxLock)
                {
                    _targetPosition = 1 - v.Position;
                    _currentTime = DateTime.Now;
                    _targetTime = DateTime.Now.AddMilliseconds(v.Duration);
                    _onyxTimer?.Change(0, 50);
                }
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }
    }
}
