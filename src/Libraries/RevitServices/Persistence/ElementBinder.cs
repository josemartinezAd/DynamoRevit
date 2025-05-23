﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Dynamo.Engine;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Nodes.ZeroTouch;
using Dynamo.Graph.Workspaces;
using DynamoServices;
using ProtoCore;
using RevitServices.Elements;
using Microsoft.CSharp;
using Newtonsoft.Json;

namespace RevitServices.Persistence
{
    /// <summary>
    /// Holds a  representation of a Revit ID that supports serialisation
    /// </summary>
    public class SerializableId
    {
        public String StringID { get; set; }
        public long IntID { get; set; }

        public override bool Equals(object other)
        {
            var sID = other as SerializableId;
            if (sID == null)
            {
                return false;
            }

            return (IntID.Equals(sID.IntID) && StringID.Equals(sID.StringID));

        }

        public override int GetHashCode()
        {
            return (StringID == null ? 0 : StringID.GetHashCode()) ^ (IntID.GetHashCode());
        }
    }


    //@TODO: This could be used to hold all the serializableIds
    public class MultipleSerializableId
    {
        public List<String> StringIDs { get; set; }
        public List<long> IntIDs { get; set; }

        public MultipleSerializableId()
        {
            InitializeDataMembers();
        }

        public MultipleSerializableId(IEnumerable<Element> elements)
        {
            InitializeDataMembers();

            foreach (Element element in elements)
            {
                StringIDs.Add(element.UniqueId);
                IntIDs.Add(element.Id.Value);
            }
        }

        public MultipleSerializableId(List<String> stringIDs, List<long> intIDs)
        {
            StringIDs = stringIDs;
            IntIDs = intIDs;
        }

        private void InitializeDataMembers()
        {
            StringIDs = new List<String>();
            IntIDs = new List<long>();
        }

        public override bool Equals(object other)
        {
            var mult = other as MultipleSerializableId;
            if (mult == null)
            {
                return false;
            }

            return this.IntIDs.SequenceEqual(mult.IntIDs) && this.StringIDs.SequenceEqual(mult.StringIDs);


        }

        public override int GetHashCode()
        {

            //concat the strings and int ids into one string and get hashcode and xor
            //a multiserializableID with same IDs in different order will return not equal  
            return (StringIDs == null ? 0 : StringIDs.Aggregate((i, j) => i + " " + j).GetHashCode()) ^
             (IntIDs == null ? 0 : IntIDs.Select(x => x.ToString()).Aggregate((i, j) => i + " " + j).GetHashCode());

        }

        /// <summary>
        /// this method tests if this multiSerializableId is contained in another
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public virtual bool isSubset(MultipleSerializableId other)
        {
            return !this.StringIDs.Except(other.StringIDs).Any();
        }

    }
        /// <summary>
        /// Class for handling unique ids in a typesafe ammner
        /// </summary>
        public class ElementUUID
    {
        public String UUID { get; set; }

        public ElementUUID()
        {
            UUID = "";
        }

        public ElementUUID(string uuid)
        {
            this.UUID = uuid;
        }

    }


    /// <summary>
    /// Tools to handle the binding and interaction 
    /// </summary>
    public class ElementBinder
    {
        private const string REVIT_TRACE_ID = "{0459D869-0C72-447F-96D8-08A7FB92214B}-REVIT";

        public static bool IsEnabled = false;

        /// <summary>
        /// Get an Element unique Identifier from trace
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static ElementUUID GetElementUUIDFromTrace(Document document)
        {
            //Get the element ID that was cached in the callsite
            string traceString = TraceUtils.GetTraceData(REVIT_TRACE_ID);

            if(String.IsNullOrEmpty(traceString))
                return null;

            SerializableId id = null;
            try
            {
                id =  JsonConvert.DeserializeObject<SerializableId>(traceString);
            }
            catch
            {
                //do nothing 
            }
            
            if (id == null)
            return null; //There was no usable data in the trace cache

            var traceDataUuid = id.StringID;
            return new ElementUUID(traceDataUuid);
        }

        /// <summary>
        /// Get the collection of ElementIDs from Trace
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static List<ElementUUID> GetElementUUIDsFromTrace(Document document)
        {
            //Get the element ID that was cached in the callsite
            string traceString = TraceUtils.GetTraceData(REVIT_TRACE_ID);

            if (String.IsNullOrEmpty(traceString))
                return null;

            MultipleSerializableId multi = null;
            try
            {
                multi = JsonConvert.DeserializeObject<MultipleSerializableId>(traceString);
            }
            catch
            {
                //do nothing 
            }

            if (multi != null)
            {
                List<ElementUUID> uuids = new List<ElementUUID>();
                foreach (var uuid in multi.StringIDs)
                    uuids.Add(new ElementUUID(uuid));

                return uuids;
            }

            SerializableId single = null;
            try
            {
                single = JsonConvert.DeserializeObject<SerializableId>(traceString);
            }
            catch
            {
                //do nothing 
            }

            if (single != null)
            {
                var traceDataUuid = single.StringID;
                List<ElementUUID> uuids = new List<ElementUUID>()
                    {
                        new ElementUUID(traceDataUuid)
                    };
                return uuids;
            }

            //No usable data was found
            return null;

        }


        /// <summary>
        /// Get the elementId associated with a UUID, possibly expensive
        /// </summary>
        /// <param name="document"></param>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public static ElementId GetIdForUUID(Document document, ElementUUID uuid)
        {
            Element e = document.GetElement(uuid.UUID);
            if (e != null)
                return e.Id;
            return null;
        }


        /// <summary>
        /// Set the element associated with the current operation from trace
        /// null if there is no object, or it's of the wrong type etc.
        /// </summary>
        /// <param name="element">The element to store in trace</param>
        public static void SetElementForTrace(Element element)
        {
            SetElementForTrace(element.Id, new ElementUUID(element.UniqueId));
        }

        /// <summary>
        /// Set a list of elements for trace
        /// </summary>
        /// <param name="elements"></param>
        public static void SetElementsForTrace(IEnumerable<Element> elements)
        {
            if (!IsEnabled) return;

            MultipleSerializableId ids = new MultipleSerializableId(elements);

            var serializedTraceData = JsonConvert.SerializeObject(ids);

            TraceUtils.SetTraceData(REVIT_TRACE_ID, serializedTraceData);

        }

        /// <summary>
        /// Set the element associated with the current operation from trace
        /// null if there is no object, or it's of the wrong type etc.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void SetElementForTrace(ElementId elementId, ElementUUID elementUUID)
        {
            if (!IsEnabled) return;

            SerializableId id = new SerializableId();
            id.IntID = elementId.Value;
            id.StringID = elementUUID.UUID;

            // if we're mutating the current Element id, that means we need to 
            // clean up the old object

            var serializedTraceData = JsonConvert.SerializeObject(id);
            // Set the element ID cached in the callsite
            TraceUtils.SetTraceData(REVIT_TRACE_ID, serializedTraceData);
        }

        /// <summary>
        /// Get the element associated with the current operation from trace
        /// null if there is no object, or it's of the wrong type etc.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetElementFromTrace<T>(Document document)
            where T : Autodesk.Revit.DB.Element
        {
            var elementUUID = GetElementUUIDFromTrace(document);

            if (elementUUID == null)
                return null;

            T ret;

            if (Elements.ElementUtils.TryGetElement(document, elementUUID.UUID, out ret))
                return ret;
            else
                return null;
        }

        /// <summary>
        /// Get a list of elements associated with the current operation from trace
        /// </summary>
        /// <param name="elements"></param>
        public static IEnumerable<T> GetElementsFromTrace<T>(Document document)
            where T : Autodesk.Revit.DB.Element
        {
            if (!IsEnabled)
            {
                return null;
            }

            var uuids = GetElementUUIDsFromTrace(document);
            if (uuids == null)
            {
                return null;
            }

            List<T> elements = new List<T>(uuids.Count);
            foreach (var uuid in uuids)
            {
                T ret;
                if (Elements.ElementUtils.TryGetElement(document, uuid.UUID, out ret))
                {
                    elements.Add(ret);
                }
            }

            return elements;
        }

        /// <summary>
        /// Delete a possibly outdated Revit Element and set new element for trace.  
        /// This method should be called if the element could not be mutated on a 
        /// second run and the old value must be destroyed.  
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void CleanupAndSetElementForTrace(Document document, Element newElement)
        {
            if (!IsEnabled) return;

            // if the element id has changed on a subsequent run, that means we
            // couldn't mutate the element - hence we need to delete the old
            // element
            var oldId = GetElementUUIDFromTrace(document);
            if (oldId != null && oldId.UUID != newElement.UniqueId)
            {
                DocumentManager.Instance.DeleteElement(oldId);
            }

            SetElementForTrace(newElement);
        }

        /// <summary>
        /// Raw method for setting data into the trace cache, the user of this method is reponsible for handling
        /// the interpretation of the data
        /// </summary>
        /// <param name="data"></param>
        public static void SetRawDataForTrace(string data)
        {
            if (!IsEnabled) return;
            TraceUtils.SetTraceData(REVIT_TRACE_ID, data);
        }

        /// <summary>
        /// Raw method for getting data from the trace cache, the user is responsible for handling the interpretation
        /// of the data
        /// </summary>
        public static string GetRawDataFromTrace()
        {
            return TraceUtils.GetTraceData(REVIT_TRACE_ID);
        }

        /// <summary>
        /// Get the Element of type T and the raw traceData of type K from Thread Local Storage.
        /// Requires that K inherits from SerializableId so the element can be retrieved from the Revit Document
        /// </summary>
        /// <returns></returns>
        public static Tuple<TElement, TId> GetElementAndTraceData<TElement, TId>(Document document)
            where TElement : Autodesk.Revit.DB.Element
            where TId: SerializableId
        {
            var traceString = ElementBinder.GetRawDataFromTrace();
            if (String.IsNullOrEmpty(traceString))
                return null;

            TId tid = null;
            try
            {
                tid = JsonConvert.DeserializeObject<TId>(traceString);
            }
            catch
            {
                //do nothing 
            }

            if (tid == null)
                return null;

            var elementId = tid.IntID;
            var uuid = tid.StringID;

            var element = default(TElement);
            
            // if we can't get the element, return null
            if (!document.TryGetElement(uuid,out element))
                return null;

            return new Tuple<TElement, TId>(element, tid);
        }
        /// <summary>
        /// This function gets the nodes which are binding with the elements which have the
        /// given element IDs
        /// </summary>
        /// <param name="ids">The given element IDs</param>
        /// <param name="workspace">The workspace model for the nodes</param>
        /// <param name="engine">The engine controller</param>
        /// <returns>the related nodes</returns>
        public static IEnumerable<NodeModel> GetNodesFromElementIds(IEnumerable<ElementId> ids,
            WorkspaceModel workspace, EngineController engine)
        {
            List<NodeModel> nodes = new List<NodeModel>();
            if (!ids.Any())
                return nodes.AsEnumerable();

            RuntimeCore runtimeCore = null;
            if (engine != null && (engine.LiveRunnerCore != null))
                runtimeCore = engine.LiveRunnerRuntimeCore;

            if (runtimeCore == null)
                return null;

            // Selecting all nodes that are either a DSFunction,
            // a DSVarArgFunction or a CodeBlockNodeModel into a list.
            var nodeGuids = workspace.Nodes.Where((n) =>
            {
                return (n is DSFunction
                        || (n is DSVarArgFunction)
                        || (n is CodeBlockNodeModel));
            }).Select((n) => n.GUID);

            var nodeTraceDataList = runtimeCore.RuntimeData.GetCallsitesForNodes(nodeGuids, runtimeCore.DSExecutable);

            bool areElementsFoundForThisNode;
            foreach (Guid guid in nodeTraceDataList.Keys)
            {
                areElementsFoundForThisNode = false;
                foreach (CallSite cs in nodeTraceDataList[guid])
                {
                    foreach (CallSite.SingleRunTraceData srtd in cs.TraceData)
                    {
                        List<string> traceData = srtd.RecursiveGetNestedData();

                        foreach (string thingy in traceData)
                        {
                            if (String.IsNullOrEmpty(thingy))
                                continue;

                            SerializableId sid = null;
                            try
                            {
                                sid = JsonConvert.DeserializeObject<SerializableId>(thingy);
                            }
                            catch
                            {
                                //do nothing 
                            }

                            if (sid != null)
                            {
                                foreach (var id in ids)
                                {
                                    if (sid.IntID == id.Value)
                                    {
                                        areElementsFoundForThisNode = true;
                                        break;
                                    }
                                }

                                if (areElementsFoundForThisNode)
                                {
                                    NodeModel inm =
                                        workspace.Nodes.Where((n) => n.GUID == guid).FirstOrDefault();
                                    nodes.Add(inm);
                                    break;
                                }
                            }
                        }

                        if (areElementsFoundForThisNode)
                            break;
                    }

                    if (areElementsFoundForThisNode)
                        break;
                }
            }

            return nodes.AsEnumerable();
        }

        /// <summary>
        /// Sets the freeze state of the elements associated with that node.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="node">The node.</param>
        /// <param name="engine">The engine.</param>
        public static void SetElementFreezeState(WorkspaceModel workspace, NodeModel node, EngineController engine)
        {
            RuntimeCore runtimeCore = null;
            if (engine != null && (engine.LiveRunnerCore != null))
                runtimeCore = engine.LiveRunnerRuntimeCore;

            if (runtimeCore == null || node == null)
                return;

            // Selecting all nodes that are either a DSFunction,
            // a DSVarArgFunction or a CodeBlockNodeModel into a list.
            var nodeGuids = workspace.Nodes.Where((n) =>
            {
                return (n is DSFunction
                        || (n is DSVarArgFunction)
                        || (n is CodeBlockNodeModel));
            }).Where((n) => n.GUID == node.GUID).Select((x) => x.GUID);

            var nodeTraceDataList = runtimeCore.RuntimeData.GetCallsitesForNodes(nodeGuids, runtimeCore.DSExecutable);
            foreach (Guid guid in nodeTraceDataList.Keys)
            {
                foreach (CallSite cs in nodeTraceDataList[guid])
                {
                    foreach (CallSite.SingleRunTraceData srtd in cs.TraceData)
                    {
                        List<string> traceData = srtd.RecursiveGetNestedData();

                        foreach (string thingy in traceData)
                        {
                            if (String.IsNullOrEmpty(thingy))
                                continue;

                            SerializableId sid = null;
                            try
                            {
                                sid = JsonConvert.DeserializeObject<SerializableId>(thingy);
                            }
                            catch
                            {
                                //do nothing 
                            }

                            if (sid != null)
                            {
                                setEachElementFreezeState(node.IsFrozen, sid.IntID);

                            }

                            MultipleSerializableId msid = null;
                            try
                            {
                                msid = JsonConvert.DeserializeObject<MultipleSerializableId>(thingy);
                            }
                            catch
                            {
                                //do nothing 
                            }

                            if (msid != null)
                            {
                                msid.IntIDs.ForEach(x => setEachElementFreezeState(node.IsFrozen, x));
                            }
                        }
                    }
                }
            }
        }

        private static void setEachElementFreezeState(bool frozen, long elementId)
        {
            //Get the Autodesk.Revit.Element.
            Element el;
            DocumentManager.Instance.CurrentDBDocument.TryGetElement(new ElementId(elementId),
                out el);

            //Get the Revit Element wrapper.
            if (el != null)
            {
                dynamic elem =
                    ElementIDLifecycleManager<long>.GetInstance().GetFirstWrapper(el.Id.Value);
                if (elem != null)
                {
                    elem.IsFrozen = frozen;
                }
            }
        }
    }

}
