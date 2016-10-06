using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;
using System;

namespace easydeploy.Models
{
    public enum OrderStatus
    {
        WaitingForSellerConfirmation = 0,
        PaymentApproved = 1,
        Canceled = 2
    }

    public class VtexFeedOrder
    {
        public string OrderId { get; set; }
        public string Status { get; set; }
        public string DateTime { get; set; }
        public string CommitToken { get; set; }

        public VtexFeedOrder()
        {
        }

        public VtexFeedOrder(JToken jsonFeedOrder)
        {
            this.OrderId = jsonFeedOrder["orderId"].Value<string>();
            this.Status = jsonFeedOrder["status"].Value<string>();
            this.DateTime = jsonFeedOrder["dateTime"].Value<string>();
            this.CommitToken = jsonFeedOrder["commitToken"].Value<string>();
        }
    }

    public class VtexOrder : TableEntity
    {
        public string AccountName { get; set; }
        public string Origin { get; set; }
        public string AffiliateId { get; set; }
        public string SalesChannel { get; set; }
        public string Value { get; set; }
        public string CreationDate { get; set; }
        public string LastChange { get; set; }
        public string Status { get; set; }

        public VtexOrder(string accountName, string orderId, string status, JObject jsonObject)
        {
            Timestamp = DateTime.UtcNow;
            SetupPartitionKey();
            RowKey = orderId;

            AccountName = accountName;
            Status = status;
            SetupFields(jsonObject);
        }

        private void SetupPartitionKey()
        {
            //Check this link http://msdn.microsoft.com/en-us/library/dd179338.aspx to see the allowed values for this fields
            //This two fields works as the record key
            //The partionkey will be used to determinate how the data will be clustered, so use it with this in your mind
            PartitionKey = Timestamp.ToString("d").Replace("/", "");
        }

        private void SetupFields(JObject jsonObject)
        {
            Origin = jsonObject["origin"].Value<string>();
            AffiliateId = jsonObject["affiliateId"].Value<string>();
            SalesChannel = jsonObject["salesChannel"].Value<string>();
            Value = jsonObject["value"].Value<string>();
            CreationDate = jsonObject["creationDate"].Value<string>();
            LastChange = jsonObject["lastChange"].Value<string>();
        }
    }
}
