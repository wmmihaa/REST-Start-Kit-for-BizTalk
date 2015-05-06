using System.Linq;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Collections.Generic;

namespace bLogical.BizTalk.RESTBehavior
{
    /// <summary>
    /// Defines the methods that enable custom inspection or modification of inbound and outbound application messages in service applications.
    /// </summary>
    public class BizTalkRESTResponseHandler : IDispatchMessageInspector
    {
        /// <summary>
        /// Adds the httpRequest header to the operation context
        /// </summary>
        /// <param name="request"></param>
        /// <param name="channel"></param>
        /// <param name="instanceContext"></param>
        /// <returns></returns>
        public object AfterReceiveRequest(ref System.ServiceModel.Channels.Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];

            OperationContext.Current.Extensions.Add(new RequestContext { RequestHeader = request.Properties["httpRequest"] as HttpRequestMessageProperty });

            return null;
        }
        /// <summary>
        /// Reads the httpRequest in the operation context and casts the respose to JSON 
        /// if the Accept header is set to application/json
        /// </summary>
        /// <param name="reply"></param>
        /// <param name="correlationState"></param>
        public void BeforeSendReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {
            var ctx = OperationContext.Current.Extensions.Find<RequestContext>();
            HttpResponseMessageProperty prop = new HttpResponseMessageProperty();
            reply.Properties.Add(HttpResponseMessageProperty.Name, prop);
            var httpStatus = 200;
            var httpStatusDescription = string.Empty;

            // Check status code
            var statusProperty = reply.Properties.FirstOrDefault<KeyValuePair<string, object>>(p => p.Key == MessageHelper.HttpStatusCode);
            if (statusProperty.Value != null)
            {
                httpStatus = int.Parse(statusProperty.Value.ToString());
                prop.StatusCode = (HttpStatusCode)httpStatus;
            }

            // Check status description
            var statusDescriptionProperty = reply.Properties.FirstOrDefault<KeyValuePair<string, object>>(p => p.Key == MessageHelper.HttpStatusDescription);
            if (statusDescriptionProperty.Value != null)
            {
                httpStatusDescription = statusDescriptionProperty.Value.ToString();
                prop.StatusDescription = httpStatusDescription;
            }

            if (ctx != null &&
                ctx.RequestHeader.Headers["Accept"] != null &&
                ctx.RequestHeader.Headers["Accept"].ToString().ToLower().Contains("application/json"))
            {
                XmlDictionaryReader dicReader = reply.GetReaderAtBodyContents();

                // Remove namespaces from json
                XmlDocument doc = new XmlDocument();
                doc.Load(dicReader);
                XElement xmlDocumentWithoutNs = RemoveAllNamespaces(XElement.Parse(doc.OuterXml));
                string xml = xmlDocumentWithoutNs.ToString();
                doc.LoadXml(xml);

                string jsonString = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.None, true);

                byte[] jsonReplyBytes = Encoding.UTF8.GetBytes(jsonString);
                XmlDictionaryReader newReplyBodyReader = JsonReaderWriterFactory.CreateJsonReader(jsonReplyBytes, XmlDictionaryReaderQuotas.Max);

                Message newReply = Message.CreateMessage(MessageVersion.None, null, newReplyBodyReader);
                newReply.Properties.Add("Content-Type", "application/json;charset=utf-8");
                newReply.Properties.Add("Accept", "application/json;charset=utf-8");

                // Set the outgoing Content-Type to application/json
                newReply.Properties.Add(HttpResponseMessageProperty.Name, prop);
                prop.Headers.Add("Content-Type", "application/json;charset=utf-8");
                prop.StatusCode = (HttpStatusCode)httpStatus;
                prop.StatusDescription = httpStatusDescription;

                WebBodyFormatMessageProperty bodyFormat = new WebBodyFormatMessageProperty(WebContentFormat.Json);
                newReply.Properties.Add(WebBodyFormatMessageProperty.Name, bodyFormat);

                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;

                reply = newReply;
            }

            OperationContext.Current.Extensions.Remove(ctx);
        }
        private static XElement RemoveAllNamespaces(XElement xmlDocument)
        {
            if (!xmlDocument.HasElements)
            {
                XElement xElement = new XElement(xmlDocument.Name.LocalName);
                xElement.Value = xmlDocument.Value;

                foreach (XAttribute attribute in xmlDocument.Attributes())
                    xElement.Add(attribute);

                return xElement;
            }
            return new XElement(xmlDocument.Name.LocalName, xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
        }
    }

    class RequestContext : IExtension<OperationContext>
    {
        public HttpRequestMessageProperty RequestHeader { get; set; }
        public void Attach(OperationContext owner)
        {

        }

        public void Detach(OperationContext owner)
        {

        }
    }

}
