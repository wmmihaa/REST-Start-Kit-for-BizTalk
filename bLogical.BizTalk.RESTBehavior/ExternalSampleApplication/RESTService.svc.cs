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
        [WebGet(UriTemplate = "/Events/{id}")]
        public Event GetEvent(string id)
        {
            return new Event { Date = "2012-05-01", Id = id, Name = "Some Event" };
        }
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "")]
        public string AddEvent(Event e)
        {
            return "Event has been added";
        }
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/Events/1")]
        public string AddEvent1(Event e)
        {
            return "Event has been added to 1";
        }
        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/Events/{id}")]
        public void DeleteEvent(string id)
        {
            //return "Person been deleted";
        }
    }
    [DataContract(Namespace = "http://RESTDEMO.Event")]
    public class Event
    {
        [DataMember(Order = 0)]
        public string Id { get; set; }
        [DataMember(Order = 1)]
        public string Date { get; set; }
        [DataMember(Order = 2)]
        public string Name { get; set; }
    }

}
