using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Helpers
{
    public static class Lib
    {
        public static byte[] ToProtobuf<T>(this T obj)
        {
            var serialize = new MemoryStream();
            ProtoBuf.Serializer.Serialize<T>(serialize, obj);
            return serialize.ToArray();
        }

        public static T ProtobufDeserialize<T>(this byte[] proto)
        {
            var serialize = new MemoryStream(proto);
            return ProtoBuf.Serializer.Deserialize<T>(serialize);
        }
    }
}
