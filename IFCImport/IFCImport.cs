using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;
using Xbim.CobieLiteUk;
using XbimExchanger.IfcToCOBieLiteUK;
using System.Text.RegularExpressions;

/*
 * Takes an ".ifc" file as an input and creates a number of ones to use with 
 * Spider Project software: 
 * - a ".txt" one to import operations, 
 * - a ".wexbim" to display in a web browser,
 * - a ".json" to hold the hierarchy of the IFC structure. 
 The command line argument is an ".ini" with the following keys 
"SourceFilePath"
"TargetFilesDir"
"TargetFileName"
*/

namespace IFCImport
{
    class IFCImport
    {
        static bool silentMode = false;

        static string delimiter = "\t";  

        static string sourceFilePath=null;
        static string targetFilesDir=null;
        static string targetFileName=null;


        static void Main(string[] args)
        {
            if( args.Length >= 1)
            {
                ReadRunSettings(args[0]);
            }
            if (sourceFilePath == null)
            {
                sourceFilePath = "source.ifc";
            }
            if (targetFilesDir == null)
            {
                targetFilesDir = "";
            }
            if (targetFileName == null)
            {
                targetFileName = "oper";
            }

            // If the target directory name is not empty and doesn't ends with '\'...
            if( targetFilesDir.Length > 0 && targetFilesDir[targetFilesDir.Length-1] != '\\')
            {
                targetFilesDir = targetFilesDir + "\\";
            }

            string opFileName = targetFilesDir + targetFileName + ".txt";
            string wexbimFileName = targetFilesDir + targetFileName + ".wexbim";
            string jsonFileName = targetFilesDir + targetFileName + ".json";

            log("Using as input: " + sourceFilePath);
            bool sourceFileOpened = false;
            bool opFileCreated = false;
            bool wexbimCreated = false;
            bool jsonCreated = false;
            try
            {
                using (IfcStore model = IfcStore.Open(sourceFilePath))
                {
                    sourceFileOpened = true;

                    // Creating a ".csv" file for importing operations into Spider Project
                    using (FileStream opFile = File.Create(opFileName))
                    {
                        opFileCreated = true;
                        using (StreamWriter opFileStreamWriter = new StreamWriter(opFile))
                        {
                            opFileStreamWriter.WriteLine("Level" + delimiter + "Code" + delimiter + "Name" +
                                delimiter + "Type" + delimiter + "Volume" + delimiter + "f_Model");
                            var project = model.Instances.FirstOrDefault<IIfcProject>();
                            PrintHierarchy(opFileStreamWriter, project, 0);
                        }
                        opFile.Close();
                        log("Success: " + opFileName + " created");
                    }
                    var context = new Xbim3DModelContext(model);
                    context.CreateContext();

                    // Creating a ".wexbim" one
                    using (FileStream wexbimFile = File.Create(wexbimFileName))
                    {
                        using (BinaryWriter wexbimBinaryWriter = new BinaryWriter(wexbimFile))
                        {
                            model.SaveAsWexBim(wexbimBinaryWriter);
                            wexbimBinaryWriter.Close();
                        }
                        wexbimFile.Close();
                        wexbimCreated = true;
                        log("Success: " + wexbimFileName + " created");
                    }

                    // Importing CoBie data into a ".json"...
                    var facilities = new List<Facility>();
                    /*
                    var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
                    facilities = ifcToCoBieLiteUkExchanger.Convert();

                    var facilityType = facilities.FirstOrDefault();
                    if (facilityType != null)
                    {
                        facilityType.WriteJson(jsonFileName, true);
                        jsonCreated = true;
                        log("Success: " + jsonFileName + " created");
                        //facilityType.WriteXml("source.xml", true);
                        //string errMsg;
                        //facilityType.WriteCobie("source.xls", out errMsg);
                        //Console.WriteLine(errMsg);
                    }
                    */
                }
            }
            catch (Exception e)
            {
                string errorMessage = "An error occured!";
                if( !sourceFileOpened )
                {
                    errorMessage += "\n  - Failed to open the source file " + sourceFilePath;
                } else if (!opFileCreated)
                {
                    errorMessage += "\n  - Failed to create the destination file " + opFileName;
                } else if (!wexbimCreated)
                {
                    log("\n - Failed to generate a geometry file " + wexbimFileName);
                } else if (!jsonCreated)
                {
                    log("\n - Failed to generate a json file " + jsonFileName);
                }
                log(errorMessage);
            }
        }


        private static void PrintHierarchy(StreamWriter sw, IIfcObjectDefinition o, int level)
        {
            string volume="";
            var oProduct = o as IIfcProduct;
            if(oProduct != null)
                ReadProp(oProduct, out volume);
            sw.WriteLine( "{0}" + delimiter + "{1}" + delimiter + "{2}" + 
                delimiter + "{3}" +delimiter+"{4}"+delimiter+"{5}",
                level.ToString(), o.GlobalId, o.Name, o.GetType().Name, volume, o.GlobalId);
            Console.WriteLine("{0}" + delimiter + "{1}" + delimiter + "{2}" +
                delimiter + "{3}" + delimiter + "{4}" + delimiter + "{5}",
                level.ToString(), o.GlobalId, o.Name, o.GetType().Name, volume, o.GlobalId);

            //only spatial elements can contain building elements 
            var oSpatialElement = o as IIfcSpatialStructureElement;
            if (oSpatialElement != null)
            {
                //using IfcRelContainedInSpatialElement to get contained elements 
                var containedElements = oSpatialElement.ContainsElements.SelectMany(rel => rel.RelatedElements);
                foreach (var element in containedElements)
                {
                    ReadProp( element, out volume );
                    sw.WriteLine( "{0}" + delimiter + "{1}" + delimiter + "{2}" + delimiter + 
                        "{3}" + delimiter + "{4}" + delimiter + "{5}",
                        (level + 1).ToString(), element.GlobalId, element.Name,
                        element.GetType().Name, volume, element.GlobalId);
                    Console.WriteLine("{0}" + delimiter + "{1}" + delimiter + "{2}" + delimiter +
                        "{3}" + delimiter + "{4}" + delimiter + "{5}",
                        (level + 1).ToString(), element.GlobalId, element.Name,
                        element.GetType().Name, volume, element.GlobalId);
                }
            }

            //using IfcRelAggregates to get spatial decomposition of spatial structure elements 
            foreach (var item in o.IsDecomposedBy.SelectMany(r => r.RelatedObjects))
            {
                PrintHierarchy(sw, item, level + 1);
            }
        }


        private static void ReadProp( IIfcProduct element, out string volume )
        {
            var properties = element.IsDefinedBy.
                Where(r => r.RelatingPropertyDefinition is IIfcPropertySet).
                SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties).
                OfType<IIfcPropertySingleValue>();
            volume = "";
            foreach (var property in properties)
            {
                Console.WriteLine( "Name="+property.Name + ", value=" + property.NominalValue.ToString() );
                if ((property.Name).ToString().ToLower() == "volume")
                    volume = property.NominalValue.ToString();
            }
            var properties2 = element.IsDefinedBy.
                Where(r => r.RelatingPropertyDefinition is IIfcPropertySet).
                SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties).
                OfType<IIfcPropertySingleValue>();
            volume = "";
            foreach (var property in properties2)
            {
                Console.WriteLine("Name=" + property.Name + ", value=" + property.ToString());
                if ((property.Name).ToString().ToLower() == "volume")
                    volume = property.NominalValue.ToString();
            }
        }

        private static int ReadRunSettings(string fileName)
        {
            int returnValue = -1;

            StreamReader file = null;
            string line;
            try
            {
                file = new StreamReader(fileName);
                while ((line = file.ReadLine()) != null)
                {
                    Regex regex = new Regex(@"([a-zA-Z]+)\=(.*)");
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        string key = match.Groups[1].ToString();
                        if (key == "SourceFilePath")
                        {
                            sourceFilePath = match.Groups[2].ToString().Trim();
                        }
                        if ( key == "TargetFilesDir" )
                        {
                            targetFilesDir = match.Groups[2].ToString().Trim();
                        }
                        if (key == "TargetFileName")
                        {
                            targetFileName = match.Groups[2].ToString().Trim();
                        }
                    }
                }
                returnValue = 0;
            }
            catch (IOException e)
            {
                ;
            }
            catch (Exception e)
            {
                ;
            }
            if (file != null)
            {
                file.Close();
            }
            return returnValue;
        }

        private static void log( string msg )
        {
            if( !silentMode )
            {
                Console.WriteLine(msg);
            }
        }
    }
} 

  
/* 
// ============================ A "Code-Store" Section ======================================
          
            using (var model = IfcStore.Open(ifcFileName))
            {
                var context = new Xbim3DModelContext(model);
                context.CreateContext();

                var wexBimFilename = Path.ChangeExtension(ifcFileName, "wexBIM");
                using (var wexBiMfile = File.Create(wexBimFilename))
                {
                    using (var wexBimBinaryWriter = new BinaryWriter(wexBiMfile))
                    {
                        model.SaveAsWexBim(wexBimBinaryWriter);
                        wexBimBinaryWriter.Close();
                    }
                    wexBiMfile.Close();
                }
          
                var facilities = new List<Facility>();
                var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
                facilities = ifcToCoBieLiteUkExchanger.Convert();

                var facilityType = facilities.FirstOrDefault();
                if (facilityType != null)
                {
                    //write the cobie data in json format
                    facilityType.WriteJson("source.json", true);
                    //write the cobie data in xml format
                    facilityType.WriteXml("source.xml", true);
                    //write the cobie data in spreadsheet format                 
                    string errMsg;
                    facilityType.WriteCobie("source.xls", out errMsg);
                    Console.WriteLine(errMsg);
                }
            }
*/
            