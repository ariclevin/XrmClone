using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XrmClone.Common
{
    public enum LogEntrySource
    {
        Attribute = 1,
        OneToManyRelationship = 2,
        ManyToManyRelationship = 3
    }

    public enum Status
    {
        Success = 1,
        Failure = 2
    }

    public enum LogEntryType
    {
        Verbose = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    public class LogEntity
    {
        public LogEntity(string entityName, string displayName, Guid primaryEntityId, string primaryEntityName)
        {
            LogEntityId = new Guid();
            EntityName = entityName;
            DisplayName = displayName;
            PrimaryId = primaryEntityId;
            PrimaryName = primaryEntityName;
        }

        public Guid LogEntityId { get; set; }
        public string EntityName { get; set; }
        public string DisplayName { get; set; }
        public Guid PrimaryId { get; set; }
        public string PrimaryName { get; set; }
    }

    public class LogEntry
    {
        public LogEntrySource EntrySource { get; set; }
        public LogEntryType EntryType { get; set; }
        public string Name { get; set; }

        public string Description { get; set; }

        public Status LogEntryStatus { get; set; } 

    }
}
