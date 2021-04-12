using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Geometry;
using Xbim.Properties;
using Xbim.Common;
using Xbim.ModelGeometry;
using Xbim.Ifc4.StructuralElementsDomain;
using Xbim.Ifc4.QuantityResource;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc2x3.MaterialResource;
using Xbim.Ifc2x3.QuantityResource;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.ModelGeometry.Scene;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.IO;

namespace IFCImportUI
{

    public struct IfcMaterialAssignment
    {
        public string _mcode, _acode, _volume;
        public IfcMaterialAssignment( string mcode, string acode, string volume ) {
            _mcode = mcode;
            _acode = acode;
            _volume = volume;
        }
    }

    class Ifc
    {
        string _path = null;

        int _parsed = 0;

        public bool isParsedOk() { return (_parsed == 1); }

        private string _volumePropertyName;
        private string _costPropertyName;
        private string _materialPropertyName;

        public Dictionary<string, string> _materials = new Dictionary<string, string>();
        public List<IfcMaterialAssignment> _materialAssignments = new List<IfcMaterialAssignment>();

        public Ifc(string path, ref ObservableCollection<Node> nodes, ref int maxLevel,
            string volumePropertyName, string costPropertyName, string materialPropertyName )
        {
            _volumePropertyName = (volumePropertyName != null && volumePropertyName.Length > 0) ? 
                volumePropertyName.ToLower() : null;
            _costPropertyName = (costPropertyName != null && costPropertyName.Length > 0) ? 
                costPropertyName.ToLower() : null;
            _materialPropertyName = (materialPropertyName != null && materialPropertyName.Length > 0) ? 
                materialPropertyName : null;

            _parsed = -1;
            using (IfcStore model = IfcStore.Open(path))
            {
                var project = model.Instances.FirstOrDefault<IIfcProject>();
                ReadHierarchy(ref nodes, project, 1, ref maxLevel);
                _parsed = 1;
                _path = path;
            }
        }

        private void ReadHierarchy(ref ObservableCollection<Node> nodes, IIfcObjectDefinition o, 
            int level, ref int maxLevel)
        {
            if (level > maxLevel)
                maxLevel = level;

            string calculatedVolume = "";
            string volume = "";
            string cost = "";
            var oProduct = o as IIfcProduct;
            if (oProduct != null)
            {
                ReadProps(oProduct, out calculatedVolume, out volume, out cost);
            }
            Node node = new Node {
                Level = level, EntityLabel = o.EntityLabel, GlobalId = o.GlobalId, 
                Name = o.Name, TypeName = o.GetType().Name,
                CalculatedVolume = calculatedVolume, Cost = cost, Volume = volume, IsChecked=true
            };
            nodes.Add(node);
            ObservableCollection<Node> subnodes = new ObservableCollection<Node>();
            node.Nodes = subnodes;

            //only spatial elements can contain building elements 
            var oSpatialElement = o as IIfcSpatialStructureElement;
            if (oSpatialElement != null)
            {
                //using IfcRelContainedInSpatialElement to get contained elements 
                var containedElements = oSpatialElement.ContainsElements.SelectMany(rel => rel.RelatedElements);
                foreach (var element in containedElements)
                {
                    ReadProps(element, out calculatedVolume, out volume, out cost);
                    subnodes.Add( new Node { 
                        Level=level+1, EntityLabel=element.EntityLabel, GlobalId=element.GlobalId, 
                        Name=element.Name, TypeName=element.GetType().Name, CalculatedVolume = calculatedVolume, 
                        Volume = volume, Cost = cost, IsChecked=true } );
                    if (level >= maxLevel)
                        maxLevel = level+1;
                }
            }

            //using IfcRelAggregates to get spatial decomposition of spatial structure elements 
            foreach (var item in o.IsDecomposedBy.SelectMany(r => r.RelatedObjects))
            {
                ReadHierarchy(ref subnodes, item, level + 1, ref maxLevel);
            }
        } // End of ReadHierarchy

        private void ReadProps(IIfcProduct element, out string cVolume, out string volume, out string cost)
        {
            cVolume = "";
            volume = "";
            cost = "";
            double grossArea=-1;

            // Reading custom "volume" and "cost" properties
            if (_volumePropertyName != null || _costPropertyName != null)
            {
                var properties = element.IsDefinedBy.
                    Where(r => r.RelatingPropertyDefinition is IIfcPropertySet).
                    SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties).
                    OfType<IIfcPropertySingleValue>();
                foreach (var property in properties)
                {
                    // Materials - look in "property"->"NominalValue"
                    if (_volumePropertyName != null)
                    {
                        if (String.Equals(property.Name.ToString().ToLower(), _volumePropertyName.ToLower()))
                            volume = property.NominalValue.ToString();
                    }
                    if( _costPropertyName != null ) { 
                        if (String.Equals( property.Name.ToString().ToLower(), _costPropertyName.ToLower()))
                            cost = property.NominalValue.ToString();
                    }
                }
            }

            // Reading "calculated volume" property, material is 10
            // ResultsView: 2 => RelatingPropertyDefinition: Quantities
            try
            {
                var properties = element.IsDefinedBy.
                    Where(r => r.RelatingPropertyDefinition is IIfcElementQuantity).
                    SelectMany(r => ((IIfcElementQuantity)r.RelatingPropertyDefinition).PropertySetDefinitions).
                    OfType<IIfcElementQuantity>();
                foreach (var property in properties) {
                    foreach (var qp in property.Quantities) {
                        if( String.Equals( qp.GetType().Name, "IfcQuantityVolume" ) ) {
                            var p = qp as IIfcQuantityVolume;
                            if (String.Equals( p.Name.ToString().ToLower(), "grossvolume") )
                            {
                                cVolume = p.VolumeValue.ToString();
                                break;
                            }
                        }
                    }                    
                }
            } catch {
                ;
            }

            if (_materialPropertyName != null) {    // If material prop is given...
                var propSet = element.IsDefinedBy.
                    Where(r => r.RelatingPropertyDefinition is IIfcPropertySet).
                    SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).PropertySetDefinitions).
                    OfType<IIfcPropertySet>();
                try {
                    foreach (var prop in propSet) {
                        if (String.Equals(prop.Name.ToString().ToLower(), 
                            _materialPropertyName.ToLower())) {
                            // var x = element.Material.GetType();
                            // element.Material.ForLayerSet
                            // Xbim.Ifc4.MaterialResourse. vs Xbim.Ifc4.MaterialResourse.
                            var mprops = prop.HasProperties.Where(r => r is IIfcPropertySingleValue).
                                Select(r => (IIfcPropertySingleValue)r).OfType<IIfcPropertySingleValue>();
                            foreach (var mprop in mprops) {
                                var mCode = mprop.NominalValue.ToString();
                                if (!_materials.ContainsKey(mCode)) {
                                    _materials.Add(mCode, mprop.Name.ToString());
                                }
                                var ma = new IfcMaterialAssignment(
                                    mCode, element.GlobalId.ToString(), cVolume);
                                _materialAssignments.Add(ma);
                            }
                        }
                    }
                } catch {
                    ;
                }
            } else {    // Extracting materials by default method
                if (element.Material is Xbim.Ifc2x3.MaterialResource.IfcMaterialLayerSetUsage) {
                    try {
                        var mlayer =
                            (Xbim.Ifc2x3.MaterialResource.IfcMaterialLayerSetUsage)element.Material;
                        var layers = mlayer.ForLayerSet.MaterialLayers;
                        foreach (var x in layers) {
                            var mName = x.Material.Name.Value.ToString();
                            var mCode = mName;
                            var mThickness = x.LayerThickness.Value.ToString();
                            if (!_materials.ContainsKey(mCode)) {
                                _materials.Add(mCode, mName);
                            }
                            var ma = new IfcMaterialAssignment(
                                mCode, element.GlobalId.ToString(), mThickness);
                            _materialAssignments.Add(ma);
                        }
                    } catch {
                        ;
                    }
                }
            }
        } // End of readProps


        public int createWexbim( string wexbimPath)
        {
            int returnValue;

            try
            {
                using (IfcStore model = IfcStore.Open(_path))
                {
                    var context = new Xbim3DModelContext(model);
                    context.CreateContext();

                    // Creating a ".wexbim" one
                    using (FileStream wexbimFile = File.Create(wexbimPath))
                    {
                        using (BinaryWriter wexbimBinaryWriter = new BinaryWriter(wexbimFile))
                        {
                            model.SaveAsWexBim(wexbimBinaryWriter);
                            wexbimBinaryWriter.Close();
                        }
                        wexbimFile.Close();
                        returnValue = 0;
                    }
                }
            }
            catch (Exception e)
            {
                returnValue = -1;
            }

            return returnValue;
        }
    }

}



/*
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
                                delimiter + "Type" + delimiter + "VolPlan" + delimiter + "f_Model");
                            var project = model.Instances.FirstOrDefault<IIfcProject>();
                            PrintHierarchy(opFileStreamWriter, project, 1);
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
                    
                    var ifcToCoBieLiteUkExchanger = new IfcToCOBieLiteUkExchanger(model, facilities);
                    facilities = ifcToCoBieLiteUkExchanger.Convert();

                    var facilityType = facilities.FirstOrDefault();
                    if (facilityType != null)
                    {
                        facilityType.WriteJson(jsonFileName, true);
                        jsonCreated = true;
                        log("Success: " + jsonFileName + " created");
                        facilityType.WriteXml(xmlFileName, true);
                        xmlCreated = true;
                        log("Success: " + xmlFileName + " created");
                        string errMsg;
                        facilityType.WriteCobie("source.xls", out errMsg);
                        if (errMsg.Length > 0)
                            Console.WriteLine(errMsg);
                        else 
                            xlsCreated = true;
                    }
                    
                }
            }
            catch (Exception e)
            {
                string errorMessage = "An error occured!";
                if (!sourceFileOpened)
                {
                    errorMessage += "\n  - Failed to open the source file " + sourceFilePath;
                }
                if (!opFileCreated)
                {
                    errorMessage += "\n  - Failed to create the destination file " + opFileName;
                }
                if (!wexbimCreated)
                {
                    log("\n - Failed to generate a geometry file " + wexbimFileName);
                }
                if (!jsonCreated)
                {
                    log("\n - Failed to generate a json file " + jsonFileName);
                }
                if (!xmlCreated)
                {
                    log("\n - Failed to generate an xml file " + xmlFileName);
                }
                if (!xlsCreated)
                {
                    log("\n - Failed to generate an xls file " + xlsFileName);
                }
                log(errorMessage);
            }
        }

 
*/
