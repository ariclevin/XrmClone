using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace XrmClone.Logic
{
    public class CloneLogic : IDisposable
    {
        private IOrganizationService service;
        private ITracingService tracingService;

        enum RelationshipStatusReason : int
        {
            Ignore = 1,
            Inactive = 2,
            Duplicate = 3,
            Reassociate = 4
        }

        public CloneLogic(IOrganizationService orgService)
        {
            service = orgService;
        }

        public CloneLogic(IOrganizationService orgService, ITracingService traceService)
        {
            service = orgService;
            tracingService = traceService;
        }

        public void CloneEntityRecord(Guid cloneSettingId, string entityName, Guid entityId)
        {
            Entity cloneSetting = service.Retrieve("xrm_clonesetting", cloneSettingId, new ColumnSet(true));
            string prefix = cloneSetting.Contains("xrm_prefix") ? cloneSetting["xrm_prefix"].ToString() : string.Empty;
            string suffix = cloneSetting.Contains("xrm_suffix") ? cloneSetting["xrm_suffix"].ToString() : string.Empty;

            string primaryIdAttribute = cloneSetting.Contains("xrm_primaryidattribute") ? cloneSetting["xrm_primaryidattribute"].ToString() : string.Empty;
            string primaryNameAttribute = cloneSetting.Contains("xrm_primarynameattribute") ? cloneSetting["xrm_primarynameattribute"].ToString() : string.Empty;

            Entity source = service.Retrieve(entityName, entityId, new ColumnSet(true));
            string primaryNameAttributeValue = source.Contains(primaryNameAttribute) ? source[primaryNameAttribute].ToString() : string.Empty;
            if (!string.IsNullOrEmpty(primaryNameAttributeValue))
                primaryNameAttributeValue = string.Format("{0}{1}{2}", prefix, primaryNameAttributeValue, suffix);
            else
                primaryNameAttributeValue = string.Format("{0}{1}", prefix, suffix);

            Guid targetId = ClonePrimaryEntity(source, cloneSettingId, primaryIdAttribute, primaryNameAttribute, primaryNameAttributeValue);
            CloneRelatedEntities(cloneSettingId, entityName, entityId, targetId);
        }

        private Guid ClonePrimaryEntity(Entity source, Guid cloneSettingId, string primaryIdAttribute, string primaryNameAttribute, string primaryNameAttributeValue)
        {
            EntityCollection attributes = RetrieveAttributes(cloneSettingId);
            List<string> list = attributes.Entities.Where(item => item.Contains("xrm_name")).Select(a => a["xrm_name"].ToString()).ToList<string>();

            Entity target = new Entity(source.LogicalName);

            foreach (KeyValuePair<string, object> attribute in source.Attributes)
            {
                tracingService.Trace("Copying Attribute: {0}", attribute.Key);
                if (attribute.Key == primaryIdAttribute)
                {

                }
                else if (attribute.Key == primaryNameAttribute)
                {
                    target[primaryNameAttribute] = primaryNameAttributeValue;
                }
                else
                {
                    if (list.Contains(attribute.Key))
                    {
                        // This is used to deal with issue of account and contact address1/address2 id fields
                        if (!attribute.Key.EndsWith("addressid"))
                        {
                            target[attribute.Key] = attribute.Value;
                        }
                    }
                }
            }

            try
            {
                tracingService.Trace("Preparing to copy entity");
                Guid targetId = service.Create(target);
                return targetId;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the ClonePrimaryEntity function of the plug-in: " + ex.Message, ex);
            }
        }

        private void CloneRelatedEntities(Guid cloneSettingId, string entityName, Guid sourceId, Guid targetId)
        {
            EntityCollection relations = RetrieveRelationships(cloneSettingId);
            if (relations.Entities.Count > 0)
            {
                foreach(Entity relation in relations.Entities)
                {
                    string relationshipName = relation.Contains("xrm_name") ? relation["xrm_name"].ToString() : string.Empty;
                    string relatedEntityName = relation.Contains("xrm_entityname") ? relation["xrm_entityname"].ToString() : string.Empty;
                    string relatedAttributeName = relation.Contains("xrm_attributename") ? relation["xrm_attributename"].ToString() : string.Empty;
                    string primaryIdAttribute = relation.Contains("xrm_primaryidattribute") ? relation["xrm_primaryidattribute"].ToString() : string.Empty;
                    string primaryNameAttribute = relation.Contains("xrm_primarynameattribute") ? relation["xrm_primarynameattribute"].ToString() : string.Empty;
                    RelationshipStatusReason status = (RelationshipStatusReason)relation["statuscode"];

                    switch (relationshipName)
                    {
                        case "Account_CustomerAddress":
                        case "Contact_CustomerAddress":
                        case "Lead_addresses":
                            // CloneRelatedAddress();
                            break;
                        default:
                            if (!string.IsNullOrEmpty(primaryNameAttribute))
                            {
                                EntityCollection relatedEntities = RetrieveRelatedEntityRecords(relatedEntityName, primaryIdAttribute, relatedAttributeName, sourceId);
                                if (relatedEntities.Entities.Count > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine("Cloning entity " + relatedEntityName);
                                    foreach (Entity relatedEntity in relatedEntities.Entities)
                                    {
                                        switch (status)
                                        {
                                            case RelationshipStatusReason.Duplicate:
                                                CloneRelatedEntity(relatedEntity, targetId, primaryIdAttribute, entityName, relatedAttributeName);
                                                break;
                                            case RelationshipStatusReason.Reassociate:
                                                AssociateRecord(relationshipName, entityName, targetId, relatedEntityName, relatedEntity.Id);
                                                break;
                                        }
                                        
                                        
                                    }
                                }
                            }
                            break;
                    }

                }
            }
        }

        private Guid CloneRelatedEntity(Entity source, Guid targetId, string primaryIdAttribute, string primaryEntityName, string relatedAttributeName)
        {
            Entity target = new Entity(source.LogicalName);

            foreach (KeyValuePair<string, object> attribute in source.Attributes)
            {
                if (attribute.Key == primaryIdAttribute)
                {

                }
                else if (attribute.Key == relatedAttributeName)
                {
                    target[relatedAttributeName] = new EntityReference(primaryEntityName, targetId);
                }
                else
                {
                    target[attribute.Key] = attribute.Value;
                }
            }

            try
            {
                tracingService.Trace("Preparing to copy entity {0}", source.LogicalName);
                service.Create(target);
                return targetId;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the ClonePrimaryEntity function of the plug-in: " + ex.Message, ex);
            }
        }

        private void AssociateRecord(string relationshipName, string primaryEntityName, Guid primaryEntityId, string relatedEntityName, Guid relatedEntityId)
        {
            EntityReferenceCollection collection = new EntityReferenceCollection();
            collection.Add(new EntityReference(relatedEntityName, relatedEntityId));

            Relationship relationship = new Relationship(relationshipName);

            try
            {
                service.Associate(primaryEntityName, primaryEntityId, relationship, collection);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the AssociateRecord function of the plug-in: " + ex.Message, ex);
            }

        }


        #region RetrieveMultiple functions

        private EntityCollection RetrieveAttributes(Guid cloneSettingId)
        {
            QueryExpression query = new QueryExpression("xrm_cloneattribute")
            {
                ColumnSet = new ColumnSet("xrm_name", "xrm_displayname"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("xrm_clonesettingid", ConditionOperator.Equal, cloneSettingId),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                }
            };

            try
            {
                EntityCollection results = service.RetrieveMultiple(query);
                return results;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the RetrieveRelatedEntityRecords function of the plug-in: " + ex.Message, ex);
            }
        }

        private EntityCollection RetrieveRelationships(Guid cloneSettingId)
        {
            QueryExpression query = new QueryExpression("xrm_clonerelationship")
            {
                ColumnSet = new ColumnSet("xrm_name", "xrm_entityname", "xrm_attributename", "xrm_primaryidattribute", "xrm_primarynameattribute", "statuscode"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("xrm_clonesettingid", ConditionOperator.Equal, cloneSettingId),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                }
            };

            try
            {
                EntityCollection results = service.RetrieveMultiple(query);
                return results;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the RetrieveRelatedEntityRecords function of the plug-in: " + ex.Message, ex);
            }
        }

        private EntityCollection RetrieveRelatedEntityRecords(string relatedEntityName, string primaryIdAttribute, string relatedAttributeName, Guid relatedEntityId)
        {
            QueryExpression query = new QueryExpression(relatedEntityName)
            {
                ColumnSet = new ColumnSet(primaryIdAttribute),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(relatedAttributeName, ConditionOperator.Equal, relatedEntityId)
                        // new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                }
            };

            try
            {
                EntityCollection results = service.RetrieveMultiple(query);
                return results;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the RetrieveRelatedEntityRecords function of the plug-in: " + ex.Message, ex);
            }
        }


        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ExpenditureLogic() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
