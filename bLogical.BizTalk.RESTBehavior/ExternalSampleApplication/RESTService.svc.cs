using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.ServiceModel.Web;

namespace ExternalSampleApplication
{
    [ServiceContract]
    public class RESTService 
    {
        [OperationContract]
        [WebGet(UriTemplate="/Events/{id}")]
        public Event GetEvent(string id)
        {
            return new Event { Date = "2012-05-01", Id = id, Name = "Some Event" };
        }
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/Events")]
        public string AddEvent(Event e)
        {
            return "Person been added";
        }
        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/Events/{id}")]
        public void DeleteEvent(string id)
        {
            //return "Person been deleted";
        }
    }
    [DataContract]
    public class Person
    { 
        [DataMember]
        public string FirstName { get; set; }
        [DataMember]
        public string LastName { get; set; }
    }
}
