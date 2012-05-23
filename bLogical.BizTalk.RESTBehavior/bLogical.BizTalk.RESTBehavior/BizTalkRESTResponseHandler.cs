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
    public class BizTalkRESTResponseHandler : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref System.ServiceModel.Channels.Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            Trace.WriteLine("AfterReceiveRequest called.","bLogical");
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];

            switch (httpProp.Method)
            {
                case "GET":
                    OperationContext.Current.Extensions.Add(new RequestContext { RequestHeader = request.Properties["httpRequest"] as HttpRequestMessageProperty });
                    break;
                case "DELETE":
                    break;    
                case "POST":
                    break;
                case "PUT":
                    break;
                default:
                    break;
            }

            // Debug
            string s = MessageHelper.CastMessageFormat(ref request, WebContentFormat.Xml);

            return null;
        }
        public void BeforeSendReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {
            Trace.WriteLine("BeforeSendReply called.", "bLogical");

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




/*
 public class BizTalkWebHttpMessageInspectorExtensionElement : System.ServiceModel.Configuration.BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(BizTalkWebHttpMessageInspectorEndpointBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new BizTalkWebHttpMessageInspectorEndpointBehavior();
        }
    }
    public class BizTalkWebHttpMessageInspectorEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {

        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new BizTalkWebHttpMessageInspector());

        }

        public void Validate(ServiceEndpoint endpoint)
        {

        }
    }
 
 */