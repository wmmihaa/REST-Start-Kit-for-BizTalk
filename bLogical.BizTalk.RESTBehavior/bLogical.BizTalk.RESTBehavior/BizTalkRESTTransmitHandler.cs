using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Xml.Schema;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;

namespace bLogical.BizTalk.RESTBehavior
{
    public class BizTalkRESTTransmitHandlerExtensionElement : System.ServiceModel.Configuration.BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(BizTalkRESTTransmitHandlerEndpointBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new BizTalkRESTTransmitHandlerEndpointBehavior();
        }
    }
    public class BizTalkRESTTransmitHandlerEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new BizTalkRESTTransmitHandler());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {

        }

        public void Validate(ServiceEndpoint endpoint)
        {

        }
    }
    public class BizTalkRESTTransmitHandler : IClientMessageInspector
    {
        public static readonly XNamespace BizTalkWebHttpNs = "http://bLogical.RESTSchemas.BizTalkWebHttpRequest/1.0";
        public static readonly XName Request = BizTalkWebHttpNs + "bizTalkWebHttpRequest";
        public static readonly XName Header = BizTalkWebHttpNs + "header";
        public static readonly XName Param = BizTalkWebHttpNs + "param";
        public static readonly XName Body = BizTalkWebHttpNs + "body";

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            var requestBody = XElement.Load(request.GetReaderAtBodyContents());
            if (requestBody.Name != Request)
            {
                throw new XmlSchemaValidationException("Invalid request message. Expected " +
                Request + ", but got " + requestBody.Name + ".");
            }
            var bodyElement = requestBody.Element(Body);
            var requestMessageProperty = new HttpRequestMessageProperty
            {
                Method = requestBody.Attribute("Method").Value,
                SuppressEntityBody = bodyElement == null
            };
            
            var uriTemplate = new UriTemplate(requestBody.Attribute("UriTemplate").Value);

            request = Message.CreateMessage(request.Version, request.Headers.Action);

            request.Headers.To = uriTemplate.BindByName(channel.RemoteAddress.Uri,
                requestBody.Elements(Param).ToDictionary( e => e.Attribute("name").Value, e => e.Value));

            request.Properties[HttpRequestMessageProperty.Name] = requestMessageProperty;
            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            // do nothing
        }
    } 
}
