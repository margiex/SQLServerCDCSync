﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dts.Runtime;
using Microsoft.SqlServer.Dts.Pipeline;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using System.Globalization;
using System.Data.SqlClient;

// Links : EzAPI
// http://social.msdn.microsoft.com/Forums/sqlserver/en-US/eb8b10bc-963c-4d36-8ea2-6c3ebbc20411/copying-600-tables-in-ssis-how-to?forum=sqlintegrationservices
// http://www.experts-exchange.com/Database/MS-SQL-Server/Q_23972361.html

namespace SQLServerCDCSync
{
    class SQLServerCDCSync
    {
        public static string GenerateMergeSQL (string name, string sourcetable, string destinationtable, string[] colums)
        {
            return null;
        }

        public static bool GenerateSSISPackage(string filename, string sourceconn, string destinationconn, string[] tables)
        {
            Application app = new Application();
            Package package = new Package();

            // Add connection managers
            ConnectionManager sourceManager = package.Connections.Add(string.Format("ADO.NET:{0}", typeof(SqlConnection).AssemblyQualifiedName));
            sourceManager.ConnectionString = sourceconn;
            sourceManager.Name = "Source Connection";
            ConnectionManager destinationManager = package.Connections.Add(string.Format("ADO.NET:{0}", typeof(SqlConnection).AssemblyQualifiedName));
            destinationManager.ConnectionString = destinationconn;
            destinationManager.Name = "Destination Connection";

            // Add Truncate Table task
            TaskHost truncateTable = package.Executables.Add("STOCK:SQLTask") as TaskHost;
            truncateTable.Name = "Truncate Table";
            truncateTable.Properties["Connection"].SetValue(truncateTable, destinationManager.ID);
            truncateTable.Properties["SqlStatementSource"].SetValue(truncateTable, String.Concat(tables.Select(s => String.Format("TRUNCATE TABLE {0};", s))));

            // Add copy table task
            TaskHost dataFlowTask = package.Executables.Add("STOCK:PipelineTask") as TaskHost;
            dataFlowTask.Name = "Copy source to destination";
            MainPipe dataFlowTaskPipe = (MainPipe)dataFlowTask.InnerObject;

            // Make sure we truncate before we run the dataflow
            PrecedenceConstraint pcTruncateDataFlow = package.PrecedenceConstraints.Add((Executable)truncateTable, (Executable)dataFlowTask);
            pcTruncateDataFlow.Value = DTSExecResult.Success;

            // Configure the source
            IDTSComponentMetaData100 adonetsrc = dataFlowTaskPipe.ComponentMetaDataCollection.New();
            adonetsrc.Name = "ADO NET Source";
            adonetsrc.ValidateExternalMetadata = true;
            adonetsrc.ComponentClassID = app.PipelineComponentInfos["ADO NET Source"].CreationName;
            IDTSDesigntimeComponent100 adonetsrcinstance = adonetsrc.Instantiate();
            adonetsrcinstance.ProvideComponentProperties();
            adonetsrcinstance.SetComponentProperty("AccessMode", 0);
            adonetsrcinstance.SetComponentProperty("TableOrViewName", "\"dbo\".\"Test1\"");
            adonetsrc.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(sourceManager);
            adonetsrc.RuntimeConnectionCollection[0].ConnectionManagerID = sourceManager.ID;
            adonetsrcinstance.AcquireConnections(null);
            adonetsrcinstance.ReinitializeMetaData();
            adonetsrcinstance.ReleaseConnections();

            // Configure the destination
            IDTSComponentMetaData100 adonetdst = dataFlowTaskPipe.ComponentMetaDataCollection.New();
            adonetdst.Name = "ADO NET Destination";
            adonetdst.ValidateExternalMetadata = true;
            adonetdst.ComponentClassID = app.PipelineComponentInfos["ADO NET Destination"].CreationName;
            IDTSDesigntimeComponent100 adonetdstinstance = adonetdst.Instantiate();
            adonetdstinstance.ProvideComponentProperties();
            adonetdstinstance.SetComponentProperty("TableOrViewName", "\"dbo\".\"Test1\"");
            adonetdst.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(destinationManager);
            adonetdst.RuntimeConnectionCollection[0].ConnectionManagerID = destinationManager.ID;
            adonetdstinstance.AcquireConnections(null);
            adonetdstinstance.ReinitializeMetaData();
            adonetdstinstance.ReleaseConnections();

            // Attach the path from data flow source to destination
            IDTSPath100 path = dataFlowTaskPipe.PathCollection.New();
            path.AttachPathAndPropagateNotifications(adonetsrc.OutputCollection[0], adonetdst.InputCollection[0]);

            
            // Do coloum mapping on the destination
            IDTSInput100 destInput = adonetdst.InputCollection[0];
            IDTSExternalMetadataColumnCollection100 externalColumnCollection = destInput.ExternalMetadataColumnCollection;
            IDTSVirtualInput100 destVirInput = destInput.GetVirtualInput();
            IDTSInputColumnCollection100 destInputCols = destInput.InputColumnCollection;
            IDTSExternalMetadataColumnCollection100 destExtCols = destInput.ExternalMetadataColumnCollection;
            IDTSOutputColumnCollection100 sourceColumns = adonetsrc.OutputCollection[0].OutputColumnCollection;

            // Hook up the external columns
            foreach (IDTSOutputColumn100 outputCol in sourceColumns)
            {
                // Get the external column id
                IDTSExternalMetadataColumn100 extCol = (IDTSExternalMetadataColumn100)destExtCols[outputCol.Name];
                if (extCol != null)
                {
                    // Create an input column from an output col of previous component.
                    destVirInput.SetUsageType(outputCol.ID, DTSUsageType.UT_READONLY);
                    IDTSInputColumn100 inputCol = destInputCols.GetInputColumnByLineageID(outputCol.ID);
                    if (inputCol != null)
                    {
                        // map the input column with an external metadata column
                        adonetdstinstance.MapInputColumn(destInput.ID, inputCol.ID, extCol.ID);
                    }
                }
            }

            app.SaveToXml(filename, package, null);

            return true;
        }

    }
}
