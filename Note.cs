using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    [ProtoContract]
    internal class Note
    {
        [ProtoMember(1)]
        public int idAuthor;
        [ProtoMember(2)]
        public string text;
        [ProtoMember(3)]
        public List<byte[]> imgs;
    }
}
