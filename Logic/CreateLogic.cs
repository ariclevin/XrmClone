using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace XrmClone.Logic
{
    public class CreateLogic : IDisposable
    {
        private IOrganizationService service;
        private ITracingService tracingService;

        public CreateLogic(IOrganizationService orgService)
        {
            service = orgService;
        }

        public CreateLogic(IOrganizationService orgService, ITracingService traceService)
        {
            service = orgService;
            tracingService = traceService;
        }

        public void CreateCloneSettings(string entityName, Guid entityId)
        {
            Entity cloneSetting = service.Retrieve(entityName, entityId, new ColumnSet(true));
            string requestedEntity = cloneSetting["xrm_entityname"].ToString();

            tracingService.Trace("Retrieving Entity Information");
            RetrieveEntityRequest request = new RetrieveEntityRequest()
            {
                LogicalName = requestedEntity,
                EntityFilters = EntityFilters.All,
                RetrieveAsIfPublished = true
            };
            RetrieveEntityResponse response = (RetrieveEntityResponse)service.Execute(request);

            tracingService.Trace("Creating Attributes");
            CreateAttributes(entityId, response.EntityMetadata.Attributes);

            tracingService.Trace("Creating One to Many Relationships");
            CreateOneToManyRelationships(entityId, response.EntityMetadata.OneToManyRelationships);

            tracingService.Trace("Creating Many to Many Relationships");
            CreateManyToManyRelationships(entityId, response.EntityMetadata.ManyToManyRelationships);

            string primaryIdAttribute = "", primaryNameAttribute = "";
            GetPrimaryAttributes(requestedEntity, out primaryIdAttribute, out primaryNameAttribute);
            UpdateCloneSetting(entityId, primaryIdAttribute, primaryNameAttribute);
        }


        private void CreateAttributes(Guid cloneSettingId, AttributeMetadata[] metadata)
        {
            foreach (AttributeMetadata attr in metadata)
            {
                string logicalName = attr.LogicalName;
                string attrType = attr.AttributeType.ToString();
                string displayName = attr.DisplayName.LocalizedLabels.Count > 0 ? attr.DisplayName.LocalizedLabels[0].Label : string.Empty;
                bool isPrimary = false;

                if (attr.IsPrimaryId.HasValue && attr.IsPrimaryId.Value == true)
                    isPrimary = true;
                else if (attr.IsPrimaryName.HasValue && attr.IsPrimaryName.Value == true)
                    isPrimary = true;

                // No need to create virtual attributes
                bool createAttribute = true;
                if (attrType.ToLower() == "virtual")
                    createAttribute = false;

                if (attrType.ToLower() == "string" && displayName == string.Empty)
                    createAttribute = false;
                
                if (createAttribute)
                    CreateAttribute(cloneSettingId, logicalName, displayName, attrType, isPrimary);
            }
        }

        private void CreateOneToManyRelationships(Guid cloneSettingId, OneToManyRelationshipMetadata[] metadata)
        {
            foreach (OneToManyRelationshipMetadata rel in metadata)
            {
                string schemaName = rel.SchemaName;
                string relatedEntityName = rel.ReferencingEntity;
                string relatedAttributeName = rel.ReferencingAttribute;

                string primaryIdAttribute = "", primaryNameAttribute = "";
                GetPrimaryAttributes(relatedEntityName, out primaryIdAttribute, out primaryNameAttribute);

                if (!Helper.isActivity(relatedEntityName))
                {
                    Guid relationshipId = CreateOneToManyRelationship(cloneSettingId, schemaName, relatedEntityName, relatedAttributeName, primaryIdAttribute, primaryNameAttribute);
                    SetState("xrm_clonerelationship", relationshipId, false);
                }
                    
            }
        }

        private void CreateManyToManyRelationships(Guid cloneSettingId, ManyToManyRelationshipMetadata[] metadata)
        {
            foreach (ManyToManyRelationshipMetadata rel in metadata)
            {
                string schemaName = rel.SchemaName;
                string relatedEntityName1 = rel.Entity1LogicalName;
                string relatedAttributeName1 = rel.Entity1IntersectAttribute;
                string relatedEntityName2 = rel.Entity2LogicalName;
                string relatedAttributeName2 = rel.Entity2IntersectAttribute;
                Guid relationshipId = CreateManyToManyRelationship(cloneSettingId, schemaName, relatedEntityName1, relatedAttributeName1, relatedEntityName2, relatedAttributeName2);
                SetState("xrm_clonerelationship", relationshipId, false);
            }
        }

        private void GetPrimaryAttributes(string entityName, out string primaryKeyFieldName, out string primaryFieldName)
        {
            RetrieveEntityRequest request = new RetrieveEntityRequest()
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Entity
            };

            RetrieveEntityResponse response = (RetrieveEntityResponse)service.Execute(request);
            primaryFieldName = response.EntityMetadata.PrimaryNameAttribute;
            primaryKeyFieldName = response.EntityMetadata.PrimaryIdAttribute;
        }

        private void CreateAttribute(Guid cloneSettingId, string logicalName, string displayName, string attrType, bool isPrimary)
        {
            Entity cloneAttribute = new Entity("xrm_cloneattribute");
            cloneAttribute.Attributes["xrm_clonesettingid"] = new EntityReference("xrm_clonesetting", cloneSettingId);
            cloneAttribute.Attributes["xrm_name"] = logicalName;
            cloneAttribute.Attributes["xrm_displayname"] = displayName;
            cloneAttribute.Attributes["xrm_attributetype"] = attrType;
            cloneAttribute.Attributes["xrm_isprimary"] = isPrimary;

            try
            {
                Guid cloneAttributeId = service.Create(cloneAttribute);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(String.Format("An error occurred in the {0} function of the {1} plug-in.", "CreateAttribute", this.GetType().ToString()), ex);
            }
        }

        private Guid CreateOneToManyRelationship(Guid cloneSettingId, string schemaName, string relatedEntityName, string relatedAttributeName, string primaryIdAttribute, string primaryNameAttribute)
        {
            Entity cloneRelationship = new Entity("xrm_clonerelationship"); // Clone Relationsip (used for 1-N Relationships)
            cloneRelationship.Attributes["xrm_clonesettingid"] = new EntityReference("xrm_clonesetting", cloneSettingId);
            cloneRelationship.Attributes["xrm_name"] = schemaName;
            cloneRelationship.Attributes["xrm_entityname"] = relatedEntityName;
            cloneRelationship.Attributes["xrm_attributename"] = relatedAttributeName;
            cloneRelationship.Attributes["xrm_primaryidattribute"] = primaryIdAttribute;
            cloneRelationship.Attributes["xrm_primarynameattribute"] = primaryNameAttribute;

            try
            {
                Guid cloneRelationshipId = service.Create(cloneRelationship);
                return cloneRelationshipId;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(String.Format("An error occurred in the {0} function of the {1} plug-in.", "CreateOneToManyRelationship", this.GetType().ToString()), ex);
            }
        }

        private Guid CreateManyToManyRelationship(Guid cloneSettingId, string schemaName, string relatedEntityName1, string relatedAttributeName1, string relatedEntityName2, string relatedAttributeName2)
        {
            Entity cloneRelationship = new Entity("xrm_clonennrelationship"); // Clone N-N Relationsip
            cloneRelationship.Attributes["xrm_clonesettingid"] = new EntityReference("xrm_clonesetting", cloneSettingId);
            cloneRelationship.Attributes["xrm_name"] = schemaName;
            cloneRelationship.Attributes["xrm_entity1name"] = relatedEntityName1;
            cloneRelationship.Attributes["xrm_attribute1name"] = relatedAttributeName1;
            cloneRelationship.Attributes["xrm_entity2name"] = relatedEntityName2;
            cloneRelationship.Attributes["xrm_attribute2name"] = relatedAttributeName2;
            
            try
            {
                Guid cloneRelationshipId = service.Create(cloneRelationship);
                return cloneRelationshipId;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(String.Format("An error occurred in the {0} function of the {1} plug-in.", "CreateManyToManyRelationship", this.GetType().ToString()), ex);
            }

        }

        private void UpdateCloneSetting(Guid cloneSettingId, string primaryIdAttribute, string primaryNameAttribute)
        {
            Entity cloneSetting = new Entity("xrm_clonesetting");
            cloneSetting.Id = cloneSettingId;
            cloneSetting.Attributes["xrm_primaryidattribute"] = primaryIdAttribute;
            cloneSetting.Attributes["xrm_primarynameattribute"] = primaryNameAttribute;

            try
            {
                service.Update(cloneSetting);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(String.Format("An error occurred in the {0} function of the {1} plug-in.", "UpdateCloneSetting", this.GetType().ToString()), ex);
            }

        }

        public void SetState(string entityname, Guid entityid, bool isActive)
        {
            EntityReference moniker = new EntityReference();
            moniker.LogicalName = entityname;
            moniker.Id = entityid;

            SetStateRequest request = new SetStateRequest();
            request.EntityMoniker = moniker;

            if (isActive == true)
            {
                request.State = new OptionSetValue(0); // 0
                request.Status = new OptionSetValue(1); // 1

            }
            else
            {
                request.State = new OptionSetValue(1); // 1
                request.Status = new OptionSetValue(2); // 2
            }

            try
            {
                SetStateResponse response = (SetStateResponse)service.Execute(request);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                string errMessage = "";
                if (ex.Message.StartsWith("SecLib::AccessCheckEx"))
                    errMessage = "Your current security settings do not allow you to deactivate this record.";
                else
                    errMessage = ex.Message;

                throw new InvalidPluginExecutionException(errMessage);
            }
        }

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
