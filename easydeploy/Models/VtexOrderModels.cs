using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
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
        public string JsonOrderContent { get; set; }

        public VtexFeedOrder()
        {
        }

        public VtexFeedOrder(JToken jsonFeedOrder)
        {
            this.OrderId = jsonFeedOrder["orderId"].Value<string>();
            this.Status = jsonFeedOrder["status"].Value<string>();
            this.DateTime = jsonFeedOrder["dateTime"].Value<string>();
            this.CommitToken = jsonFeedOrder["commitToken"].Value<string>();
            this.JsonOrderContent = string.Empty;
        }
    }

    public class VtexOrder : TableEntity
    {
        
        [JsonProperty("accountname")]
        public string AccountName { get; set; }

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("affiliateid")]
        public string AffiliateId { get; set; }

        [JsonProperty("saleschannel")]
        public string SalesChannel { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("creationdate")]
        public string CreationDate { get; set; }

        [JsonProperty("lastchange")]
        public string LastChange { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }



        public VtexOrder(string accountName, string orderId, string status, JObject jsonObject)
        {
            this.Timestamp = DateTime.UtcNow;
            this.SetupPartitionKey();
            this.RowKey = orderId;

            this.AccountName = accountName;
            this.Status = status;
            this.SetupFields(jsonObject);
        }

        private void SetupPartitionKey()
        {
            //Check this link http://msdn.microsoft.com/en-us/library/dd179338.aspx to see the allowed values for this fields
            //This two fields works as the record key
            //The partionkey will be used to determinate how the data will be clustered, so use it with this in your mind
            this.PartitionKey = this.Timestamp.ToString("d").Replace("/", "");
        }

        private void SetupFields(JObject jsonObject)
        {
            this.Origin = jsonObject["origin"].Value<string>();
            this.AffiliateId = jsonObject["affiliateId"].Value<string>();
            this.SalesChannel = jsonObject["salesChannel"].Value<string>();
            this.Value = jsonObject["value"].Value<string>();
            this.CreationDate = jsonObject["creationDate"].Value<string>();
            this.LastChange = jsonObject["lastChange"].Value<string>();
        }
    }
}
