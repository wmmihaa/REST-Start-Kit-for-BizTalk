using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.IO;
using System.Xml;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json.Converters;
using System.Configuration;
using System.ComponentModel;

namespace bLogical.BizTalk.RESTBehavior
{
    public class BizTalkRESTRequestHandlerExtensionElement : System.ServiceModel.Configuration.BehaviorExtensionElement
    {
        [ConfigurationProperty("uriTemplates", DefaultValue = "", IsRequired = false)]
        public string UriTemplates
        {
            get
            {
                return (string)base["uriTemplates"];
            }
            set
            {
                base["uriTemplates"] = value;
            }
        }

        public override Type BehaviorType
        {
            get { return typeof(BizTalkRESTRequestHandlerBehavior); }
        }

        protected override object CreateBehavior()
        {
            BizTalkRESTRequestHandlerBehavior bizTalkWebHttpBehavior = new BizTalkRESTRequestHandlerBehavior();

            if (!string.IsNullOrEmpty(this.UriTemplates))
            {
                string[] uritemplateStrings = this.UriTemplates.Split('|');

                foreach (string uritemplateString in uritemplateStrings)
                {
                    bizTalkWebHttpBehavior.uriTemplates.Add(new UriTemplate(uritemplateString));
                }

            }
            return bizTalkWebHttpBehavior;
        }
    }
    public class BizTalkRESTRequestHandlerBehavior : WebHttpBehavior
    {
        public BizTalkRESTRequestHandlerBehavior()
        {
            this.uriTemplates = new List<UriTemplate>();
        }
        public List<UriTemplate> uriTemplates { get; set; }
        protected override WebHttpDispatchOperationSelector GetOperationSelector(ServiceEndpoint endpoint)
        {
            return new BizTalkRESTRequestHandler(endpoint) { uriTemplates = this.uriTemplates };
        }
        public override void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new BizTalkRESTResponseHandler());
            base.ApplyDispatchBehavior(endpoint, endpointDispatcher);
        }
    }
    public class BizTalkRESTRequestHandler : WebHttpDispatchOperationSelector
    {
        public List<UriTemplate> uriTemplates { get; set; }
        ServiceEndpoint _currentEndpoint = null;
        public BizTalkRESTRequestHandler(ServiceEndpoint endpoint)
            : base(endpoint)
        {
            _currentEndpoint = endpoint;
        }

        public override UriTemplate GetUriTemplate(string operationName)
        {
            UriTemplate result = base.GetUriTemplate(operationName);
            return result;
        }

        protected override string SelectOperation(ref Message message, out bool uriMatched)
        {
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)message.Properties[HttpRequestMessageProperty.Name];

            if(httpProp.Method=="GET" || httpProp.Method=="DELETE")
                message = ConvertToURIRequest(message);
            else if ((httpProp.Method == "POST" || httpProp.Method == "PUT") && ((HttpRequestMessageProperty)message.Properties["httpRequest"]).Headers.ToString().ToLower().Contains("application/json"))
            {
                message = ConvertToXmlMessage(message);
            }

            uriMatched = true;
            return "TwoWayMethod";
        }

        public Message ConvertToXmlMessage(Message message)
        {
            MemoryStream ms = new MemoryStream();
            XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(ms);
            message.WriteMessage(writer);
            writer.Flush();
            string jsonString = Encoding.UTF8.GetString(ms.ToArray());

            XmlNodeConverter xmlNodeConverter = new XmlNodeConverter();
            XmlDocument myXmlNode = JsonConvert.DeserializeXmlNode(jsonString);
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(myXmlNode.InnerXml));

            XmlReader reader = XmlReader.Create(stream);
            Message newMessage = Message.CreateMessage(reader, int.MaxValue, MessageVersion.None);
            newMessage.Properties.CopyProperties(message.Properties);
            return newMessage;
        }

        private Message ConvertToURIRequest(Message message)
        {
            Message newRequest = null;
            try
            {
                string requestBody = MessageHelper.CreateURIRequest(message, this.uriTemplates, _currentEndpoint.ListenUri);

                MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
                XmlReader reader = XmlReader.Create(ms);

                newRequest = Message.CreateMessage(MessageVersion.None, "GetRequest", reader);

                HttpRequestMessageProperty httpRequestMessageProperty = new HttpRequestMessageProperty();
                httpRequestMessageProperty.Method = "POST";
                httpRequestMessageProperty.QueryString = string.Empty;
                httpRequestMessageProperty.SuppressEntityBody = false;
                httpRequestMessageProperty.Headers.Add("SOAPAction", "GetRequest");
                httpRequestMessageProperty.Headers.Add("Content-Type", "text/xml; charset=utf-8");

                foreach (var property in message.Properties)
                    newRequest.Properties.Add(property.Key, property.Value);

                newRequest.Headers.CopyHeadersFrom(message);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Unable convert incomming URI to request message", ex);
            }
            return newRequest;
        }

        
    }
}
