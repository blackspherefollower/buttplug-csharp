using Newtonsoft.Json;
using System;

namespace Buttplug.Core
{
    public class ButtplugMessage
    {
        /*
         * Message versions
         *
         * These are here for backwards compatibility support, but
         * this also serves as a changelog of sorts.
         *
         * Version 0 - Schema 0.1.0
         *   First release with no backwards compatibility
         *
         * Version 1 - Schema 0.2.0
         *   Introduction of MessageVersioning
         *   Addition of generic VibrateCmd
         */
        [JsonIgnore]
        public const uint CurrentMessageVersion = 1;

        [JsonProperty(Required = Required.Always)]
        public uint Id { get; set; }

        [JsonIgnore]
        public uint MessageVersioningVersion
        {
            get
            {
                return _messageVersioningVersion;
            }

            protected set
            {
                _messageVersioningVersion = value;
            }
        }

        [JsonIgnore]
        public Type MessageVersioningPrevious
        {
            get
            {
                return _messageVersioningPrevious;
            }

            protected set
            {
                _messageVersioningPrevious = value;
            }
        }

        // Base class starts at version 0
        [JsonIgnore]
        private uint _messageVersioningVersion = 0;
        // No previous version for base classes
        [JsonIgnore]
        private Type _messageVersioningPrevious = null;

        public ButtplugMessage(uint aId)
        {
            Id = aId;
        }
    }

    public interface IButtplugMessageOutgoingOnly
    {
    }
}