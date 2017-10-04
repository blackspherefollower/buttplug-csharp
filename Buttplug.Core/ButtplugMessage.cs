using Newtonsoft.Json;

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
        public const uint CurrentMessageVersion = 1;

        [JsonProperty(Required = Required.Always)]
        public uint Id { get; set; }

        // Base class starts at version 0
        [JsonIgnore]
        public readonly uint MessageVersioningVersion = 0;

        // No previous version for base classes
        [JsonIgnore]
        public readonly ButtplugMessage MessageVersioningPrevious = null;

        public ButtplugMessage(uint aId)
        {
            Id = aId;
        }
    }

    public interface IButtplugMessageOutgoingOnly
    {
    }
}