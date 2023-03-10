using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APICheck
{
    public class api_objects
    {
        /// <summary>
        /// OS API Order schema, representing each data package
        /// </summary>
        public class Order
        {
            public int id { get; set; }
            public string url { get; set; }
            public string createdOn { get; set; }
            public string reason { get; set; }
            public string supplyType { get; set; }
            public string productVersion { get; set; }
            public string format { get; set; }
            public string dataPackageUrl { get; set; }
            public downloads[] downloads { get; set; }
        }

        /// <summary>
        ///  OS API Downloads schema, representing every file
        /// </summary>
        public class downloads
        {
            public string fileName { get; set; }
            public string url { get; set; }
            public Int64 size { get; set; }
            public string md5 { get; set; }
        }
    }
}
