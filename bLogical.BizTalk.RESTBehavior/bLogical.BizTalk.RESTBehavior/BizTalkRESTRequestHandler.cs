using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json.Converters;
using System.Configuration;
using System.ServiceModel.Configuration;

namespace bLogical.BizTalk.RESTBehavior
{
    /// <summary>
    /// Represents a configuration element that contains sub-elements that specify behavior extensions, which enable the user to customize service or endpoint behaviors.
    /// </summary>
    public class BizTalkRESTRequestHandlerExtensionElement : BehaviorExtensionElement
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
    /// <summary>
    /// Enables the Web programming model for a service. Adds the BizTalkRESTRequestHandler Operation Selector along
    /// with the BizTalkRESTResponseHandler (MessageInspector)
    /// </summary>
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
    /// <summary>
    /// The operation selector that supports the Web programming model.
    /// </summary>
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

        /// <summary>
        /// This where all starts. Depending on the HTTP verb the following actions is preformed:
        /// GET|DELETE: 
        /// A new BizTalkWebHttpRequest message is generated from the URI parameters, and passed on 
        /// to BizTalk with the content type set to HTTP POST.
        /// 
        /// POST|PUT: 
        /// Nothing is done, unless the incoming content type is set to application/json, in which case 
        /// the incoming JSON message is casted to an XML message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="uriMatched"></param>
        /// <returns></returns>
        protected override string SelectOperation(ref Message message, out bool uriMatched)
        {
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)message.Properties[HttpRequestMessageProperty.Name];

            if (httpProp.Method == "GET" || httpProp.Method == "DELETE")
                message = ConvertToURIRequest(message);
            else if ((httpProp.Method == "POST" || httpProp.Method == "PUT") &&
                ((HttpRequestMessageProperty)message.Properties["httpRequest"]).Headers.ToString().ToLower().Contains("application/json"))
            {
                var via = message.Properties.Via;
                var type = via.Segments.Last().ToString();
                MemoryStream ms = new MemoryStream();
                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(ms);
                message.WriteMessage(writer);
                writer.Flush();
                string xmlString = Encoding.UTF8.GetString(ms.ToArray());

                xmlString = xmlString.Replace("<root", string.Format("<{0}", type));
                xmlString = xmlString.Replace("root>", string.Format("{0}>", type));

                MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xmlString));
                XmlReader reader = XmlReader.Create(stream);
                Message newMessage = Message.CreateMessage(reader, int.MaxValue, MessageVersion.None);
                newMessage.Properties.CopyProperties(message.Properties);
                message = newMessage;
            }

            uriMatched = true;

            // The TwoWayMethod is the only method exposed from BizTalk
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
