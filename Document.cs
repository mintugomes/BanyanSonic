using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonicConnect
{
    public class Document
    {
        public string Collection { get; set; }
        public string Bucket { get; set; }
        public string Id {  get; set; }
        public string Text {  get; set; }
        public Document(string collection,string bucket,string id,string text) 
        { 
            Collection = collection;
            Bucket = bucket;
            Id = id;
            Text = text;
        }
    }
}
