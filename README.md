# VTEX OMS To Azure Table

_This project was the base approach for [VtexInsights](https://github.com/vtex/VtexInsights) project._

With **VtexOmsToAzureTable** you can easily create an Azure environment with and a simple worker capable of receive VTEX OMS updates from your accounts sales.

Prerequisite
====================
To use this project you will need:

- [VTEX "Account", "AppKey" and "AppToken"](https://github.com/vtex/VtexInsights/wiki/VTEX-Configurations)
- [Microsoft Azure Subscription](https://github.com/vtex/VtexInsights/wiki/Azure-Account-Creation)

Deploy the application
=======================
To deploy the application on Microsoft Azure is really simple, use the button bellow and follow the wizard, to help with the wizard check the document [App Deployment To Azure](https://github.com/vtex/VtexOmsToAzureTable/wiki/App-Deployment-to-Azure)

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://azuredeploy.net/)

How it works
====================
Clicking **Deploy** button above a web application will be deployed on your Azure enviroment and also all necessary Azure resources will be created.

The Web Application runs data-mining routines to pull data from VTEX OMS feed Api and storage it to your Microsoft Storage Account (Azure Table).

Security
====================
**Hey, Don't Worry !!** Everything will be deployed into your own Azure subscription so just you (or allowed ones) can access your data.

What will be created in your Microsoft Azure Subscription
============================
- Azure Hosting Standard S1 (need always on)
- Azure Web App 
- Azure Storage Account 

You can check what will be created using the file [azuredeploy.json](https://github.com/vtex/VtexOmsToAzureTable/blob/azure_easydeploy/azuredeploy.json), this file is a Azure Template that descrives all the infrastructure needed.

Cheking your data
================
You can check your collected VTEX orders dadts using another open source project available on CodePlex [Azure Storage Explorer](https://azurestorageexplorer.codeplex.com/)

You will need your storage account name and also the storage account key.

This data can be retrieved from [Azure Portal](https://porta.azure.com) in the Storage Account Settings.


