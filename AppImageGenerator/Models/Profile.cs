using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace WWA.WebUI.Models
{
    public class Profile
    {
        [DataMember(Name = "width")]
        public int Width { get; set; }

        [DataMember(Name = "height")]
        public int Height { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "desc")]
        public string Desc { get; set; }

        [DataMember(Name = "folder")]
        public string Folder { get; set; }

        [DataMember(Name = "format")]
        public string Format { get; set; }

        [DataMember(Name = "padding")]
        public double? Padding { get; set; }
    }
}