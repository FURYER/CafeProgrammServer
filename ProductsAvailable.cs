using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    [ProtoContract]
    internal class ProductsAvailable
    {
        [ProtoMember(1)]
        public List<NomenclatureType> types = new List<NomenclatureType>();
        [ProtoMember(2)]
        public List<Nomenclature> nomenclatures = new List<Nomenclature>();
        [ProtoMember(3)]
        public List<Product> products = new List<Product>();

        [ProtoContract]
        public class NomenclatureType
        {
            [ProtoMember(1)]
            public int id;
            [ProtoMember(2)]
            public string name;
        }

        [ProtoContract]
        public class Nomenclature
        {
            [ProtoMember(1)]
            public int id;
            [ProtoMember(2)]
            public string name;
            [ProtoMember(3)]
            public string articul;
            [ProtoMember(4)]
            public string type;
            [ProtoMember(5)]
            public double price;
            [ProtoMember(6)]
            public byte[] img;
        }

        [ProtoContract]
        public class Product
        {
            [ProtoMember(1)]
            public int id;
            [ProtoMember(2)]
            public string name { get; set; }
            [ProtoMember(3)]
            public int count { get; set; }
            [ProtoMember(4)]
            public double price { get; set; }
            public string Price
            {
                get
                {
                    return string.Format("{0:0.00}", price) + " руб.";
                }
            }
            [ProtoMember(5)]
            public string type;
            [ProtoMember(6)]
            public string articul;
            [ProtoMember(7)]
            public byte[] img;
        }
    }
}
