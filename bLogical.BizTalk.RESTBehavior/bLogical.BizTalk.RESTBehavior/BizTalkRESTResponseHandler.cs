using System;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Xml;
using System.ServiceModel;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.Serialization.Json;
using System.ServiceModel.Web;

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

            if (ctx != null && 
                ctx.RequestHeader.Headers["Accept"] != null && 
                ctx.RequestHeader.Headers["Accept"].ToString().ToLower().Contains("application/json"))
            {
                XmlDictionaryReader dicReader = reply.GetReaderAtBodyContents();
                XmlDocument doc = new XmlDocument();
                doc.Load(dicReader);
                string jsonString = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.None, true);

                byte[] jsonReplyBytes = Encoding.UTF8.GetBytes(jsonString);
                XmlDictionaryReader newReplyBodyReader = JsonReaderWriterFactory.CreateJsonReader(jsonReplyBytes, XmlDictionaryReaderQuotas.Max);
                Message newReply = Message.CreateMessage(MessageVersion.None, null, newReplyBodyReader);
                newReply.Properties.Add("Content-Type", "application/json;charset=utf-8");

                WebBodyFormatMessageProperty bodyFormat = new WebBodyFormatMessageProperty(WebContentFormat.Json);
                newReply.Properties.Add(WebBodyFormatMessageProperty.Name, bodyFormat);

                WebOperationContext.Current.OutgoingResponse.Format = WebMessageFormat.Json;

                reply = newReply;
            }
            
            OperationContext.Current.Extensions.Remove(ctx);
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
