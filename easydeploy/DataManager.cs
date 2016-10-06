using easydeploy.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
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
        private string storageconnection = string.Empty;
        private HttpClient httpClientInstance = null;
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
            InitializeConfigurationsFields();
            InitializaAppInsights();
            InitializeStorageAccount();
            InitializeTableClient();
            InitializeHttpClient();
            InitializeFeedTimer();
        }

        private void InitializeHttpClient()
        {
            httpClientInstance = new HttpClient();
            httpClientInstance.DefaultRequestHeaders.Add("x-vtex-api-appKey", appKey);
            httpClientInstance.DefaultRequestHeaders.Add("x-vtex-api-appToken", appToken);
            httpClientInstance.DefaultRequestHeaders.Accept.Add(JsonMediaTypeWithQualityHeaderValue);
        }

        #endregion

        #region [ Initialization Methods ]

        /// <summary>
        /// Initialize fields from configuration application settings
        /// </summary>
        private void InitializeConfigurationsFields()
        {
            accountName = ConfigurationManager.AppSettings["vtexaccountname"];
            appKey = ConfigurationManager.AppSettings["vtexappkey"];
            appToken = ConfigurationManager.AppSettings["vtexapptoken"];
            instrumentationkey = ConfigurationManager.AppSettings["instrumentationkey"];
            storageconnection = ConfigurationManager.AppSettings["storageconnectionstring"];
        }

        /// <summary>
        /// Initialize Azure Application Insights client
        /// </summary>
        private void InitializaAppInsights()
        {
            appInsightsClient = new TelemetryClient();
            appInsightsClient.InstrumentationKey = instrumentationkey;
        }

        /// <summary>
        /// Parse the connection string and create reference to the storage account
        /// </summary>
        private void InitializeStorageAccount()
        {
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageconnection);
            }
            catch (Exception exception)
            {
                appInsightsClient.TrackException(exception, new Dictionary<string, string>() { { "workflow", "Initializing storage account" } });
            }
        }

        /// <summary>
        /// Create the table client
        /// </summary>
        private void InitializeTableClient()
        {
            try
            {
                CloudTableClient tableClient = this.storageAccount.CreateCloudTableClient();

                ordersPaymentPendingTable = tableClient.GetTableReference("ordersPaymentPending");
                ordersPaymentApprovedTable = tableClient.GetTableReference("ordersPaymentApproved");
                ordersCanceledTable = tableClient.GetTableReference("ordersCanceled");

                // Create the table if it doesn't exist.
                ordersPaymentPendingTable.CreateIfNotExists();
                ordersPaymentApprovedTable.CreateIfNotExists();
                ordersCanceledTable.CreateIfNotExists();
            }
            catch (Exception exception)
            {
                appInsightsClient.TrackException(exception, new Dictionary<string, string>() { { "workflow", "Initializing table storage" } });
            }
        }

        /// <summary>
        /// Create data retrieve timer
        /// </summary>
        private void InitializeFeedTimer()
        {
            feedTimer = new Timer(FeedTimerIntervalInMiliseconds);
            feedTimer.AutoReset = false;
            feedTimer.Enabled = true;
            feedTimer.Elapsed += FeedTimer_Elapsed;
            FeedTimer_Elapsed(this, null);
        }

        #endregion

        #region [ Retrieve Data Methods ]

        private void FeedTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var retrieveTask = RetreiveOmsFeedData();
            retrieveTask.ConfigureAwait(false);

            retrieveTask.ContinueWith(task =>
            {
                if (task.Exception == null)
                {
                    JArray omsFeedJsonData = task.Result;
                    List<VtexFeedOrder> feedOrders = TransformToVtexFeedOrders(omsFeedJsonData);

                    appInsightsClient.TrackMetric("FeedRetrievedItens", feedOrders.Count);

                    foreach (var feedOrder in feedOrders)
                    {
                        var order = GetVtexOrder(feedOrder).Result;
                        ProcessOrder(order);
                        CommitFeedToken(feedOrder.CommitToken).Wait();
                    }

                    appInsightsClient.Flush();
                }
                
                feedTimer.Start();
            });
        }

        /// <summary>
        /// Retrieve OMS Feed data
        /// </summary>
        private async Task<JArray> RetreiveOmsFeedData()
        {
            JArray result = null;
            string response = string.Empty;
            Uri address = new Uri("http://" + this.accountName + ".vtexcommercestable.com.br/api/oms/pvt/feed/orders/status");
            try
            {
                response = await httpClientInstance.GetStringAsync(address);
                result = JArray.Parse(response);
            }
            catch (Exception ex)
            {
                appInsightsClient.TrackException(ex, new Dictionary<string, string>() { { "workflow", "Retrieve OMS feed" } });
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

            Uri address = new Uri("http://" + accountName + ".vtexcommercestable.com.br/api/oms/pvt/orders/" + feedOrder.OrderId);
            try
            {
                response = await httpClientInstance.GetStringAsync(address);
                result = TransformToVtexOrder(feedOrder.OrderId, feedOrder.Status, response);
            }
            catch (Exception ex)
            {
                appInsightsClient.TrackException(ex, new Dictionary<string, string>() { { "workflow", "Get VTEX order" } });
            }

            return result;
        }

        private async Task CommitFeedToken(string feedCommitToken)
        {
            Uri address = new Uri("http://" + this.accountName + ".vtexcommercestable.com.br/api/oms/pvt/feed/orders/status/confirm");
            try
            {
                feedCommitToken = feedCommitToken.Replace("\"", "\\\"");
                string postContent = "[{\"commitToken\":\"" + feedCommitToken + "\"}]";
                var response = await httpClientInstance.PostAsync(address, new StringContent(postContent, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                appInsightsClient.TrackException(ex, new Dictionary<string, string>() { { "workflow", "Commit feed token" } });
            }
        }

        #endregion

        #region [ Process Data Methods ]

        private void ProcessOrder(VtexOrder order)
        {
            switch (order.Status)
            {
                case "waiting-for-seller-confirmation":
                    RegisterOrder(ordersPaymentPendingTable, order);
                    break;
                case "payment-approved":
                    RegisterOrder(ordersPaymentApprovedTable, order);
                    break;
                case "canceled":
                    RegisterOrder(ordersCanceledTable, order);
                    break;
            }
        }

        private void TrackEvent(VtexOrder order, double value)
        {
            var dateTime = DateTime.ParseExact(order.LastChange, "MM/dd/yyyy hh:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

            EventTelemetry telemetry = new EventTelemetry("Order");
            telemetry.Properties.Add("AccountName", order.AccountName);
            telemetry.Properties.Add("AffiliateId", order.AffiliateId);
            telemetry.Properties.Add("LastChange", dateTime.ToString("o"));
            telemetry.Properties.Add("Origin", order.Origin);
            telemetry.Properties.Add("SalesChannel", order.SalesChannel);
            telemetry.Properties.Add("Status", order.Status);
            telemetry.Timestamp = dateTime;
            telemetry.Metrics.Add("Value", value);

            appInsightsClient.TrackEvent(telemetry);
        }

        private void RegisterOrder(CloudTable cloudTable, VtexOrder order)
        {
            try
            {
                var realValue = double.Parse(order.Value) / 100; //TODO: Check how to make this configurable for account that use more than 2 digits
                TrackEvent(order, realValue);
                
                // Create the TableOperation object that inserts the customer entity.
                TableOperation insertOperation = TableOperation.Insert(order);
                
                // Execute the insert operation.
                cloudTable.Execute(insertOperation);
            }
            catch (Exception ex)
            {
                appInsightsClient.TrackException(ex, new Dictionary<string, string>() { { "workflow", "Register order" } });
            }
        }

        #endregion

        #region [ IDisposable Interface Methods ]

        public void Dispose()
        {
            feedTimer.Stop();
            feedTimer.Dispose();
            feedTimer = null;
        }

        #endregion
    }
}