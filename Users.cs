using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    [ProtoContract]
    public class Users
    {
        [ProtoMember(1)]
        public List<User> users;

        [ProtoContract]
        public class User
        {
            [ProtoMember(1)]
            public int id;
            [ProtoMember(2)]
            public string[] fullname;
            [ProtoMember(3)]
            public string persona;
            [ProtoMember(4)]
            public string login;
            [ProtoMember(5)]
            public byte[] img;
            [ProtoMember(6)]
            public string permission;
        }
    }
}
