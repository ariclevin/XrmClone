using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XrmClone
{
    public static class Helper
    {
        public static bool isActivity(string entityName)
        {
            bool rc = false;
            switch (entityName)
            {
                case "activitypointer":
                case "activityparty":
                case "appointment":
                case "email":
                case "fax":
                case "letter":
                case "phonecall":
                case "recurringappointmentmaster":
                case "serviceappointment":
                case "task":
                    rc = true;
                    break;
                default:
                    rc = false;
                    break;
            }

            return rc;
        }

    }
}
