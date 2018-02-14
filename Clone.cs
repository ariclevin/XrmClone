using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;

using XrmClone.Logic;

namespace XrmClone
{
    public class Clone : Plugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Email"/> class.
        /// </summary>
        public Clone()
            : base(typeof(Clone))
        {
            this.RegisteredEvents.Add(new Tuple<int, string, string, Action<LocalPluginContext>>(40, "Create", "xrm_clonesetting", new Action<LocalPluginContext>(ExecutePostCloneSettingCreate)));
            this.RegisteredEvents.Add(new Tuple<int, string, string, Action<LocalPluginContext>>(40, "xrm_CloneRecord", "xrm_clonesetting", new Action<LocalPluginContext>(ExecutePostCloneEntityRecord)));
        }

        protected void ExecutePostCloneSettingCreate(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            string entityName = localContext.PluginExecutionContext.PrimaryEntityName;
            Guid entityId = localContext.PluginExecutionContext.PrimaryEntityId;

            ITracingService tracingService = localContext.TracingService;
            tracingService.Trace("Entered {0} Plugin Method", "ExecutePostCloneSettingCreate");

            using (CreateLogic logic = new CreateLogic(localContext.OrganizationService, tracingService))
            {
                logic.CreateCloneSettings(entityName, entityId);
            }
        }

        protected void ExecutePostCloneEntityRecord(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            // string entityName = localContext.PluginExecutionContext.PrimaryEntityName;
            Guid cloneSettingId = localContext.PluginExecutionContext.PrimaryEntityId;

            string entityName = localContext.PluginExecutionContext.InputParameters["EntityName"].ToString();
            Guid entityId = new Guid(localContext.PluginExecutionContext.InputParameters["EntityId"].ToString());

            ITracingService tracingService = localContext.TracingService;
            tracingService.Trace("Entered {0} Plugin Method", "ExecutePostCloneEntityRecord");

            using (CloneLogic logic = new CloneLogic(localContext.OrganizationService, localContext.TracingService))
            {
                logic.CloneEntityRecord(cloneSettingId, entityName, entityId);
            }
        }
    }
}
