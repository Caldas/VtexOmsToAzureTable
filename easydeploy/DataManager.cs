using easydeploy.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace easydeploy
{
    public class DataManager : IDisposable
    {
        #region [ Private Constants Fields ]

        private const int FeedTimerIntervalInMiliseconds = 60000;

        #endregion

        #region [ Private Static Readonly Fields ]

        private static readonly MediaTypeWithQualityHeaderValue JsonMediaTypeWithQualityHeaderValue = new MediaTypeWithQualityHeaderValue("application/json");
        private static readonly Queue<HttpClient> HttpClientQueue = new Queue<HttpClient>();

        #endregion

        #region [ Private Fields ]

        private string accountName = string.Empty;
        private string appKey = string.Empty;
        private string appToken = string.Empty;
        private string instrumentationkey = string.Empty;
        private CloudStorageAccount storageAccount = null;
        private CloudTable ordersPaymentPendingTable = null;
        private CloudTable ordersPaymentApprovedTable = null;
        private CloudTable ordersCanceledTable = null;
        private Timer feedTimer = null;
        private TelemetryClient appInsightsClient = null;

        #endregion

        #region [ Constructors ]

        public DataManager()
        {
            this.InitializeConfigurationsFields();
            this.InitializeStorageAccount();
            this.InitializeTableClient();
            this.InitializaAppInsights();
            this.InitializeFeedTimer();
        }

        #endregion

        #region [ Initialization Methods ]

        private void InitializaAppInsights()
        {
            this.appInsightsClient = new TelemetryClient();
            this.appInsightsClient.InstrumentationKey = this.instrumentationkey;
        }

        /// <summary>
        /// Parse the connection string and create reference to the storage account
        /// </summary>
        private void InitializeStorageAccount()
        {
            this.storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["storageconnectionstring"]);
        }

        /// <summary>
        /// Create the table client
        /// </summary>
        private void InitializeTableClient()
        {
            CloudTableClient tableClient = this.storageAccount.CreateCloudTableClient();

            this.ordersPaymentPendingTable = tableClient.GetTableReference("ordersPaymentPending");
            this.ordersPaymentApprovedTable = tableClient.GetTableReference("ordersPaymentApproved");
            this.ordersCanceledTable = tableClient.GetTableReference("ordersCanceled");

            // Create the table if it doesn't exist.
            this.ordersPaymentPendingTable.CreateIfNotExists();
            this.ordersPaymentApprovedTable.CreateIfNotExists();
            this.ordersCanceledTable.CreateIfNotExists();
        }

        /// <summary>
        /// Create data retrieve timer
        /// </summary>
        private void InitializeFeedTimer()
        {
            this.feedTimer = new Timer(FeedTimerIntervalInMiliseconds);
            this.feedTimer.AutoReset = false;
            this.feedTimer.Enabled = true;
            this.feedTimer.Elapsed += this.FeedTimer_Elapsed;
            this.FeedTimer_Elapsed(this, null);
        }

        /// <summary>
        /// Initialize fields from configuration application settings
        /// </summary>
        private void InitializeConfigurationsFields()
        {
            this.accountName = ConfigurationManager.AppSettings["vtexaccountname"];
            this.appKey = ConfigurationManager.AppSettings["vtexappkey"];
            this.appToken = ConfigurationManager.AppSettings["vtexapptoken"];
            this.instrumentationkey = ConfigurationManager.AppSettings["instrumentationkey"];
        }

        #endregion

        #region [ Retrieve Data Methods ]

        private void FeedTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var retrieveTask = this.RetreiveOmsFeedData();
            retrieveTask.ConfigureAwait(false);
            retrieveTask.ContinueWith(task =>
            {
                JArray omsFeedJsonData = task.Result;
                List<VtexFeedOrder> feedOrders = this.TransformToVtexFeedOrders(omsFeedJsonData);
                this.appInsightsClient.TrackMetric("FeedRetrievedItens", feedOrders.Count);
                foreach (var feedOrder in feedOrders)
                {
                    var order = this.GetVtexOrder(feedOrder).Result;
                    this.ProcessOrder(order);
                    this.CommitFeedToken(feedOrder.CommitToken).Wait();
                }
                this.feedTimer.Start();
            });
        }

        private HttpClient DequeueHttpClientInstance()
        {
            HttpClient client = null;

            if (HttpClientQueue.Count > 0)
            {
                try
                {
                    client = HttpClientQueue.Dequeue();
                }
                catch (InvalidOperationException)
                {
                }
            }
            if (client == null)
                client = GetNewHttpClientInstance();
            return client;
        }

        private void EnqueueHttpClientInstance(HttpClient client)
        {
            HttpClientQueue.Enqueue(client);
        }

        private HttpClient GetNewHttpClientInstance()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-vtex-api-appKey", this.appKey);
            client.DefaultRequestHeaders.Add("x-vtex-api-appToken", this.appToken);
            client.DefaultRequestHeaders.Accept.Add(JsonMediaTypeWithQualityHeaderValue);
            return client;
        }

        /// <summary>
        /// Retrieve OMS Feed data
        /// </summary>
        private async Task<JArray> RetreiveOmsFeedData()
        {
            JArray result = null;
            string response = string.Empty;
            HttpClient client = this.DequeueHttpClientInstance();
            Uri address = new Uri("http://" + this.accountName + ".vtexcommercestable.com.br/api/oms/pvt/feed/orders/status");
            try
            {
                response = await client.GetStringAsync(address);
                result = JArray.Parse(response);
            }
            catch (Exception ex)
            {
                this.appInsightsClient.TrackException(ex);
                this.EnqueueHttpClientInstance(client);
                throw ex.GetBaseException();
            }
            return result;
        }

        /// <summary>
        /// Transform json array to OMS Feed data
        /// </summary>
        private List<VtexFeedOrder> TransformToVtexFeedOrders(JArray jsonData)
        {
            List<VtexFeedOrder> orders = new List<VtexFeedOrder>();
            foreach (var jsonFeedOrder in jsonData)
            {
                VtexFeedOrder feedOrder = new VtexFeedOrder(jsonFeedOrder);
                orders.Add(feedOrder);
            }
            return orders;
        }

        private VtexOrder TransformToVtexOrder(string orderId, string status, string orderJsonContent)
        {
            JObject jsonObject = JObject.Parse(orderJsonContent);
            return new VtexOrder(this.accountName, orderId, status, jsonObject);
        }

        private async Task<VtexOrder> GetVtexOrder(VtexFeedOrder feedOrder)
        {
            VtexOrder result = null;
            string response = string.Empty;
            HttpClient client = this.DequeueHttpClientInstance();

            Uri address = new Uri("http://" + this.accountName + ".vtexcommercestable.com.br/api/oms/pvt/orders/" + feedOrder.OrderId);
            try
            {
                response = await client.GetStringAsync(address);
                result = this.TransformToVtexOrder(feedOrder.OrderId, feedOrder.Status, response);
            }
            catch (Exception ex)
            {
                this.appInsightsClient.TrackException(ex);
                this.EnqueueHttpClientInstance(client);
                throw ex.GetBaseException();
            }

            return result;
        }

        private async Task CommitFeedToken(string feedCommitToken)
        {
            HttpClient client = this.DequeueHttpClientInstance();
            Uri address = new Uri("http://" + this.accountName + ".vtexcommercestable.com.br/api/oms/pvt/feed/orders/status/confirm");
            try
            {
                feedCommitToken = feedCommitToken.Replace("\"", "\\\"");
                string postContent = "[{\"commitToken\":\"" + feedCommitToken + "\"}]";
                var response = await client.PostAsync(address, new StringContent(postContent, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                this.appInsightsClient.TrackException(ex);
                this.EnqueueHttpClientInstance(client);
                throw ex.GetBaseException();
            }
        }

        #endregion

        #region [ Process Data Methods ]

        private void ProcessOrder(VtexOrder order)
        {
            switch (order.Status)
            {
                case "waiting-for-seller-confirmation":
                    this.RegisterOrder(this.ordersPaymentPendingTable, order);
                    break;
                case "payment-approved":
                    this.RegisterOrder(this.ordersPaymentApprovedTable, order);
                    break;
                case "canceled":
                    this.RegisterOrder(this.ordersCanceledTable, order);
                    break;
            }
        }

        private void RegisterAppInsightEvent(VtexOrder order)
        {
            var realValue = Double.Parse(order.Value) / 100; //TODO: Check how to make this configurable for account that use more than 2 digits
            this.TrackEvent(order, realValue);
            this.appInsightsClient.Flush();
        }

        private void TrackEvent(VtexOrder order, double realValue)
        {
            EventTelemetry telemetry = new EventTelemetry("Order");
            telemetry.Properties.Add("AccountName", order.AccountName);
            telemetry.Properties.Add("AffiliateId", order.AffiliateId);
            telemetry.Properties.Add("LastChange", DateTimeOffset.Parse(order.LastChange).ToString("o"));
            telemetry.Properties.Add("Origin", order.Origin);
            telemetry.Properties.Add("SalesChannel", order.SalesChannel);
            telemetry.Properties.Add("Status", order.Status);
            telemetry.Timestamp = DateTimeOffset.Parse(order.CreationDate);
            telemetry.Metrics.Add("Value", realValue);
            this.appInsightsClient.TrackEvent(telemetry);
        }

        private void TrackMetric(VtexOrder order, double realValue)
        {
            MetricTelemetry telemetry = new MetricTelemetry("OrderAmount", realValue);
            telemetry.Timestamp = DateTimeOffset.Parse(order.CreationDate);
            telemetry.Properties.Add("AccountName", order.AccountName);
            telemetry.Properties.Add("AffiliateId", order.AffiliateId);
            telemetry.Properties.Add("LastChange", DateTimeOffset.Parse(order.LastChange).ToString("o"));
            telemetry.Properties.Add("Origin", order.Origin);
            telemetry.Properties.Add("SalesChannel", order.SalesChannel);
            telemetry.Properties.Add("Status", order.Status);
            this.appInsightsClient.TrackMetric(telemetry);
        }

        private void RegisterOrder(CloudTable cloudTable, VtexOrder order)
        {
            try
            {
                this.RegisterAppInsightEvent(order);
                // Create the TableOperation object that inserts the customer entity.
                TableOperation insertOperation = TableOperation.Insert(order);
                // Execute the insert operation.
                cloudTable.Execute(insertOperation);
            }
            catch (Exception ex)
            {
                this.appInsightsClient.TrackException(ex);
                throw ex.GetBaseException();
            }
        }

        #endregion

        #region [ IDisposable Interface Methods ]

        public void Dispose()
        {
            this.feedTimer.Stop();
            this.feedTimer.Dispose();
            this.feedTimer = null;
        }

        #endregion
    }
}