using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Core.Messages;
using JetBrains.Annotations;
using static Buttplug.Core.Messages.Error;

namespace Buttplug.Core
{
    public abstract class ButtplugDevice : IButtplugDevice
    {
        public string Name { get; }

        public string Identifier { get; }

        public uint Index { get; set; }

        public bool IsConnected
        {
            get
            {
                return !_isDisconnected;
            }
        }

        public uint VibratorCount
        {
            get
            {
                return _vibratorCount;
            }

            protected set
            {
                var tmp = new double[value];
                for (var i = 0; i < value; i++)
                {
                    tmp[i] = i < _vibratorCount ? _vibratorSpeeds[i] : 0;
                }

                _vibratorCount = value;
                _vibratorSpeeds = tmp;
            }
        }

        [CanBeNull]
        public event EventHandler DeviceRemoved;

        [CanBeNull]
        public event EventHandler<MessageReceivedEventArgs> MessageEmitted;

        [NotNull]
        protected readonly IButtplugLog BpLogger;

        [NotNull]
        protected readonly Dictionary<Type, ButtplugDeviceWrapper> MsgFuncs;

        [NotNull]
        protected double[] _vibratorSpeeds;

        private bool _isDisconnected;

        private uint _vibratorCount;

        public class ButtplugDeviceWrapper
        {
            public Func<ButtplugDeviceMessage, Task<ButtplugMessage>> Func;
            public Dictionary<string, string> Attrs;

            public ButtplugDeviceWrapper(Func<ButtplugDeviceMessage, Task<ButtplugMessage>> aFunc, Dictionary<string, string> aAttrs = null)
            {
                Func = aFunc;
                Attrs = aAttrs;
                if (Attrs == null)
                {
                    Attrs = new Dictionary<string, string>();
                }
            }
        }

        protected ButtplugDevice([NotNull] IButtplugLogManager aLogManager,
            [NotNull] string aName,
            [NotNull] string aIdentifier,
            [NotNull] uint aVibratorCount = 0)
        {
            BpLogger = aLogManager.GetLogger(GetType());
            MsgFuncs = new Dictionary<Type, ButtplugDeviceWrapper>();
            Name = aName;
            Identifier = aIdentifier;
            _vibratorCount = aVibratorCount;
            _vibratorSpeeds = new double[aVibratorCount];
        }

        public IEnumerable<Type> GetAllowedMessageTypes()
        {
            return MsgFuncs.Keys;
        }

        public Dictionary<string, string> GetMessageAttrs(Type aMsg)
        {
            if (MsgFuncs.TryGetValue(aMsg, out var wrapper))
            {
                return wrapper.Attrs ?? new Dictionary<string, string>();
            }

            return new Dictionary<string, string>();
        }

        protected void InvokeDeviceRemoved()
        {
            _isDisconnected = true;
            DeviceRemoved?.Invoke(this, new EventArgs());
        }

        public async Task<ButtplugMessage> ParseMessage([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (_isDisconnected)
            {
                return BpLogger.LogErrorMsg(aMsg.Id, ErrorClass.ERROR_DEVICE,
                    $"{Name} has disconnected and can no longer process messages.");
            }

            if (!MsgFuncs.ContainsKey(aMsg.GetType()))
            {
                return BpLogger.LogErrorMsg(aMsg.Id, ErrorClass.ERROR_DEVICE,
                    $"{Name} cannot handle message of type {aMsg.GetType().Name}");
            }

            // We just checked whether the key exists above, so we're ok.
            // ReSharper disable once PossibleNullReferenceException
            return await MsgFuncs[aMsg.GetType()].Func.Invoke(aMsg);
        }

        public virtual Task<ButtplugMessage> Initialize()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return Task.FromResult<ButtplugMessage>(new Ok(ButtplugConsts.SystemMsgId));
        }

        public abstract void Disconnect();

        protected void EmitMessage(ButtplugMessage aMsg)
        {
            MessageEmitted?.Invoke(this, new MessageReceivedEventArgs(aMsg));
        }
    }
}