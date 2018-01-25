using System;
using System.Linq;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using JetBrains.Annotations;

namespace Buttplug.Server.Managers.SimulatorManager
{
    internal class SimulatedButtplugDevice : ButtplugDevice
    {
        private readonly SimulatorManager _manager;

        private readonly uint _vibratorCount;

        public SimulatedButtplugDevice(
            SimulatorManager aManager,
            [NotNull] IButtplugLogManager aLogManager,
            [NotNull] DeviceSimulator.PipeMessages.DeviceAdded da)
            : base(aLogManager, da.Name, da.Id)
        {
            _manager = aManager;
            if (da.HasLinear)
            {
                MsgFuncs.Add(typeof(FleshlightLaunchFW12Cmd), new ButtplugDeviceWrapper(HandleFleshlightLaunchFW12Cmd));
                MsgFuncs.Add(typeof(LinearCmd), new ButtplugDeviceWrapper(HandleLinearCmd, new MessageAttributes() { FeatureCount = 1 }));
            }

            if (da.VibratorCount > 0)
            {
                _vibratorCount = da.VibratorCount;
                MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
                MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleVibrateCmd, new MessageAttributes() { FeatureCount = da.VibratorCount }));
            }

            if (da.HasRotator)
            {
                MsgFuncs.Add(typeof(VorzeA10CycloneCmd), new ButtplugDeviceWrapper(HandleVorzeA10CycloneCmd));
            }

            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));
        }

        public override void Disconnect()
        {
        }

        private Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            _manager.StopDevice(this);
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is SingleMotorVibrateCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            for (uint i = 0; i < _vibratorCount; i++)
            {
                _manager.Vibrate(this, cmdMsg.Speed, i);
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is VibrateCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            var vis = cmdMsg.Speeds.Where(x => x.Index < _vibratorCount).ToList();
            if (!vis.Any())
            {
                return Task.FromResult<ButtplugMessage>(new Error("Invalid vibrator index!", Error.ErrorClass.ERROR_DEVICE, aMsg.Id));
            }

            foreach (var vi in vis)
            {
                _manager.Vibrate(this, vi.Speed, vi.Index);
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleVorzeA10CycloneCmd(ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is VorzeA10CycloneCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            _manager.Rotate(this, cmdMsg.Speed, cmdMsg.Clockwise);
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        // ReSharper disable once InconsistentNaming
        private Task<ButtplugMessage> HandleFleshlightLaunchFW12Cmd(ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is FleshlightLaunchFW12Cmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            _manager.Linear(this,
                Convert.ToDouble(cmdMsg.Speed) / 99,
                Convert.ToDouble(cmdMsg.Position) / 99);
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleLinearCmd(ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is LinearCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            var vis = cmdMsg.Vectors.Where(x => x.Index == 0).ToList();
            if (!vis.Any())
            {
                return Task.FromResult<ButtplugMessage>(new Error("Invalid vibrator index!", Error.ErrorClass.ERROR_DEVICE, aMsg.Id));
            }

            foreach (var vi in vis)
            {
                _manager.Linear2(this, vi.Duration, vi.Position);
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }
    }
}