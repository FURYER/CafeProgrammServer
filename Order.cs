using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    [ProtoContract]
    internal class Order
    {
        [ProtoMember(1)]
        public int id;
        [ProtoMember(2)]
        public string[] fullname;
        [ProtoMember(3)]
        public int userId;
        [ProtoMember(4)]
        public string status;
        [ProtoMember(5)]
        public DateTime date;
        [ProtoMember(6)]
        public List<Product> products = new List<Product>();
        [ProtoMember(7)]
        public double totalPrice = 0;
        [ProtoMember(8)]
        public string requisites;

        [ProtoContract]
        public class Product
        {
            [ProtoMember(1)]
            public int id;
            [ProtoMember(2)]
            public string name;
            [ProtoMember(3)]
            public int count;
            [ProtoMember(4)]
            public double price;
            [ProtoMember(5)]
            public string type;
            [ProtoMember(6)]
            public string articul;
            [ProtoMember(7)]
            public int buyCount;
        }
    }
}
