using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace easydeploy.Models
{
    public class Order : TableEntity
    {

        public Order(string date, string orderid)
        {
            //Check this link http://msdn.microsoft.com/en-us/library/dd179338.aspx to see the allowed values for this fields
            //This two fields works as the record key
            //The partionkey will be used to determinate how the data will be clustered, so use it with this in your mind
            this.PartitionKey = date.Replace("/","");
            this.RowKey = orderid;
            this.Timestamp = DateTime.Now;
        }

        public string Status { get; set; }

        public string LastChange { get; set; }
    }
}