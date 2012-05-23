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
        [Description("Represents a Uniform Resource Identifier (URI) template. The UriTemplate is optional, but needs to be set if named parameters are expected")]
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
            //if(1==2)
            //    string s = MessageHelper.MessageToString(ref message, WebContentFormat.Xml);
    
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

        private Message _ConvertToXmlMessage(Message message)
        {
            Message newMessage = null;
            try
            {
                MemoryStream ms = new MemoryStream();
                XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(ms);
                message.WriteMessage(writer);
                writer.Flush();
                string jsonString = Encoding.UTF8.GetString(ms.ToArray());

                XmlNodeConverter xmlNodeConverter = new XmlNodeConverter();
                XmlDocument myXmlNode = JsonConvert.DeserializeXmlNode(jsonString);
                MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(myXmlNode.InnerXml));
                stream.Position = 0;

                XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
                newMessage = Message.CreateMessage(reader, int.MaxValue, message.Version);
                newMessage.Properties.CopyProperties(message.Properties);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Unable to cast JSON encoded message to XML", ex);
            }
            return newMessage;
        }

        private Message ConvertToURIRequest(Message message)
        {
            Message newRequest = null;
            try
            {
                Trace.WriteLine("SelectOperation called with a GET Request.", "bLogical");
                string requestBody = CreateURIRequest(message);
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

        private string CreateURIRequest(Message message)
        {
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)message.Properties[HttpRequestMessageProperty.Name];

            XNamespace ns = string.Format("http://bLogical.RESTSchemas.{0}Request", httpProp.Method);
            XElement segmentsElement = new XElement("Segments");
            XElement xmlTree = new XElement(ns + string.Format("{0}Request",httpProp.Method), 
                new XAttribute(XNamespace.Xmlns + "ns0", ns.NamespaceName), 
                segmentsElement);

            if (this.uriTemplates != null) // UriTemplate exists. Eg /rest/firstname={fname}&lastname={lname}
            {
                bool templateMatch = false;
                foreach (var uriTemplate in this.uriTemplates)
                {
                    Uri baseUri = new Uri(message.Headers.To.ToString().Replace(message.Headers.To.AbsolutePath, string.Empty));
                    UriTemplateMatch results = uriTemplate.Match(baseUri, message.Headers.To);

                    if (results == null)
                        continue;

                    templateMatch = true;
                    foreach (string variableName in results.BoundVariables.Keys)
                    {
                        segmentsElement.Add(new XElement("Segment", new XAttribute("name", variableName), new XAttribute("value", results.BoundVariables[variableName])));
                    }

                    break;
                }
                if (!templateMatch)
                    throw new ApplicationException("Uri didn't match the template");

            }
            else // No uri template. Eg http://localhost/Orders/2012/10
            {
                string[] segments = message.Headers.To.ToString().Replace(_currentEndpoint.ListenUri.ToString(), string.Empty).Split('/');

                foreach (string val in segments)
                {
                    if(!string.IsNullOrEmpty(val))
                        segmentsElement.Add(new XElement("Segment", new XAttribute("value", val)));
                }

            }
            return xmlTree.ToString();          
        }
    }
}
