/*
 This code originates from Nitin Mehrotra.
 http://social.technet.microsoft.com/wiki/contents/articles/2474.invoke-restful-web-services-with-biztalk-server-2010.aspx
 */
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
using System.IO;
using System.Xml;
using System.ServiceModel.Configuration;

namespace bLogical.BizTalk.RESTBehavior
{
    /// <summary>
    /// Represents a configuration element that contains sub-elements that specify behavior extensions, which enable the user to customize service or endpoint behaviors.
    /// </summary>
    public class BizTalkRESTTransmitHandlerExtensionElement : BehaviorExtensionElement
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
    /// <summary>
    /// Implements methods that can be used to extend run-time behavior for an endpoint in either a service or client application.
    /// Adds the BizTalkRESTTransmitHandler behavior
    /// </summary>
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
    /// <summary>
    /// Defines a message inspector object that can be added to the MessageInspectors collection to view or modify messages.
    /// </summary>
    public class BizTalkRESTTransmitHandler : IClientMessageInspector
    {
        public static readonly XNamespace BizTalkWebHttpNs = "http://bLogical.RESTSchemas.BizTalkWebHttpRequest/1.0";
        public static readonly XName Request = BizTalkWebHttpNs + "bizTalkWebHttpRequest";
        public static readonly XName Header = BizTalkWebHttpNs + "headers";
        public static readonly XName Param = BizTalkWebHttpNs + "params";
        public static readonly XName Body = BizTalkWebHttpNs + "body";
        public static readonly string OPERATION = "http://schemas.microsoft.com/BizTalk/2003/system-properties#Operation";
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            var requestBody = XElement.Load(request.GetReaderAtBodyContents());
            if (request.Headers.Action == "POST" || request.Headers.Action == "PUT")
            {
                MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(requestBody.ToString()));
                XmlReader reader = XmlReader.Create(ms);

                request = Message.CreateMessage(request.Version, request.Headers.Action, reader);

                var httpRequestMessageProperty = new HttpRequestMessageProperty
                {
                    Method = request.Headers.Action,
                    QueryString=string.Empty,
                    SuppressEntityBody = false,
                };

                httpRequestMessageProperty.Headers.Add("Content-Type", "application/xml; charset=utf-8");
                httpRequestMessageProperty.Headers.Add("Accept", "application/xml; charset=utf-8");

                foreach (var property in request.Properties)
                    request.Properties.Add(property.Key, property.Value);

                request.Headers.To = channel.RemoteAddress.Uri;
                return null;

            }
            if (request.Headers.Action == "GET" || request.Headers.Action == "DELETE")
            {
                if (requestBody.Name != Request)
                {
                    throw new XmlSchemaValidationException("Invalid request message. Expected " +
                    Request + ", but got " + requestBody.Name + ".");
                }
                var requestMessageProperty = new HttpRequestMessageProperty
                {
                    Method = requestBody.Attribute("method") == null ? request.Headers.Action : requestBody.Attribute("method").Value,
                    SuppressEntityBody = true
                };
                foreach (var header in requestBody.Elements(Header).Elements())
                {
                    requestMessageProperty.Headers.Add(
                        header.Attribute("name").Value,
                        header.Value);
                }
                var uriTemplate = new UriTemplate(requestBody.Attribute("uriTemplate").Value);

                request = Message.CreateMessage(MessageVersion.None, request.Headers.Action+"Action");

                var bodyElement = requestBody.Element(Body);

                request = bodyElement == null
                    ? Message.CreateMessage(request.Version, request.Headers.Action)
                    : Message.CreateMessage(request.Version, request.Headers.Action, bodyElement.ToString());


                Dictionary<string,string> dic = requestBody.Elements(BizTalkWebHttpNs + "params").Elements().ToDictionary(e => e.Attribute("name").Value, e => e.Value);

                request.Headers.To = uriTemplate.BindByName(channel.RemoteAddress.Uri,dic);
                request.Properties[HttpRequestMessageProperty.Name] = requestMessageProperty;
            }
            return null;
            
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            // do nothing
        }
    } 
}
