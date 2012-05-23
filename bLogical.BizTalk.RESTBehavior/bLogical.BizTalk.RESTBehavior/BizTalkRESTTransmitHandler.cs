using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Xml.Schema;
using System.ServiceModel.Dispatcher;

namespace bLogical.BizTalk.RESTBehavior
{
    public class BizTalkWebHttpMessageInspector : IClientMessageInspector
    {
        private static readonly XNamespace BizTalkWebHttpNs = "http://microsoft.com/schemas/samples/biztalkwebhttp/1.0 [This link is external to TechNet Wiki. It will open in a new window.] ";
        private static readonly XName Request = BizTalkWebHttpNs + "bizTalkWebHttpRequest";
        private static readonly XName Header = BizTalkWebHttpNs + "header";
        private static readonly XName Param = BizTalkWebHttpNs + "param";
        private static readonly XName Body = BizTalkWebHttpNs + "body";

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
                Method = requestBody.Attribute("method").Value,
                SuppressEntityBody = bodyElement == null
            };
            foreach (var header in requestBody.Elements(Header))
            {
                requestMessageProperty.Headers.Add(
                header.Attribute("name").Value,
                header.Value);
            }
            var uriTemplate = new UriTemplate(requestBody.Attribute("uriTemplate").Value);
            request = bodyElement == null
                ? Message.CreateMessage(request.Version, request.Headers.Action)
                : Message.CreateMessage(request.Version, request.Headers.Action, bodyElement);
            request.Headers.To = uriTemplate.BindByName(channel.RemoteAddress.Uri,
                requestBody.Elements(Param).ToDictionary(
                e => e.Attribute("name").Value, e => e.Value));
            request.Properties[HttpRequestMessageProperty.Name] = requestMessageProperty;
            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            // do nothing
        }
    } 
}
