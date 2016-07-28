using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Table;
using easydeploy.Models;

namespace easydeploy.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            //This is a simple example showing how to use the table on azure storage

            // Parse the connection string and return a reference to the storage account.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=vtexdaystorage;AccountKey=ZZY9YbalGrFOFNm1v4TYKDZ00zzGmaIBQ2PlMT591xSwdLef0l7L2GJy1pSJznQvtqb2icRNEnV+MSYl3XAZ7Q==;");
            //CloudStorageAccount.Parse(
            //ConfigurationManager.AppSettings["StorageConnectionString"]);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to the table.
            CloudTable table = tableClient.GetTableReference("orders");

            // Create the table if it doesn't exist.
            table.CreateIfNotExists();

            // Create a new order entity.
            Order order1 = new Order(DateTime.Now.Date.ToString("d"), Guid.NewGuid().ToString());
            order1.Status = "Opened";
            order1.LastChange = DateTime.Now.Date.ToString();

            Order order2 = new Order(DateTime.Now.Date.ToString("d"), Guid.NewGuid().ToString());
            order2.Status = "Closed";
            order2.LastChange = DateTime.Now.Date.ToString();

            //When necessary batch operations help a lot to have a better performance
            //if possible prefer to use batch over single operations

            try
            {
                bool executeBatch = false;
                if (executeBatch)
                {
                    // Add both customer entities to the batch insert operation.
                    TableBatchOperation batchOperation = new TableBatchOperation();
                    batchOperation.Insert(order1);
                    batchOperation.Insert(order2);
                    // Execute the batch operation.
                    table.ExecuteBatch(batchOperation);
                }
                else
                {
                    // Create the TableOperation object that inserts the customer entity.
                    TableOperation insertOperation = TableOperation.Insert(order1);
                    // Execute the insert operation.
                    table.Execute(insertOperation);
                }

                ViewBag.Message = "Checkout the code from controller HomeController.Index";
            }catch(Exception ex)
            {
                ViewBag.Message = "Error Occured: " + ex.Message + "</br>" + ex.StackTrace ;
            }

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}