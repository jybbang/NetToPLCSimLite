using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Protocols
{
    [ProtoContract]
    public class PipeProtocol
    {
        [ProtoMember(1)]
        public Type Command { get; set; }
        [ProtoMember(2)]
        public int Heartbit { get; set; }
        [ProtoMember(3)]
        public string Uid { get; set; }
        [ProtoMember(4)]
        public List<byte[]> PlcList { get; set; }

        public enum Type
        {
            KEEPALIVE,
            KEEPALIVE_ACK,
            RESET_PLC,
            RESET_PLC_ACK,
            ERROR_PLC,
        }
    }
}
