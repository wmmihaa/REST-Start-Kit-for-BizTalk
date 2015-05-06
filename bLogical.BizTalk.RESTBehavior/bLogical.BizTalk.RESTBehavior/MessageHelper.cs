using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Channels;
using System.IO;
using System.Xml;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;

namespace bLogical.BizTalk.RESTBehavior
{
    internal class MessageHelper
    {
        public static readonly string PropertyNamespace = "https://bLogical.RESTSchemas.PropertySchema";
        public static readonly string HttpStatusCode = PropertyNamespace + "#HTTPStatusCode";
        public static readonly string HttpStatusDescription = PropertyNamespace + "#HTTPStatusDescription";

        public static readonly XNamespace BizTalkWebHttpNs = "http://bLogical.RESTSchemas.BizTalkWebHttpRequest/1.0";
        public static readonly XName Request = BizTalkWebHttpNs + "bizTalkWebHttpRequest";
        public static readonly XName Header = BizTalkWebHttpNs + "header";
        public static readonly XName Params = BizTalkWebHttpNs + "params";
        public static readonly XName Param = BizTalkWebHttpNs + "param";
        public static readonly XName Body = BizTalkWebHttpNs + "body";

        public static string CastMessageFormat(ref Message message, WebContentFormat messageFormat)
        {
            MemoryStream ms = new MemoryStream();
            XmlDictionaryWriter writer = null;
            switch (messageFormat)
            {
                case WebContentFormat.Default:
                case WebContentFormat.Xml:
                    writer = XmlDictionaryWriter.CreateTextWriter(ms);
                    break;
                case WebContentFormat.Json:
                    writer = JsonReaderWriterFactory.CreateJsonWriter(ms);
                    break;
                case WebContentFormat.Raw:
                    // special case for raw, easier implemented separately 
                    return MessageHelper.ReadRawBody(ref message);
            }

            message.WriteMessage(writer);
            writer.Flush();
            string messageBody = Encoding.UTF8.GetString(ms.ToArray());

            // now that the message was read, it needs to be recreated. 
            ms.Position = 0;

            // if the message body was modified, needs to reencode it, as show below 
            // ms = new MemoryStream(Encoding.UTF8.GetBytes(messageBody)); 

            XmlDictionaryReader reader;
            if (messageFormat == WebContentFormat.Json)
            {
                reader = JsonReaderWriterFactory.CreateJsonReader(ms, XmlDictionaryReaderQuotas.Max);
            }
            else
            {
                reader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
            }

            Message newMessage = Message.CreateMessage(reader, int.MaxValue, message.Version);
            newMessage.Properties.CopyProperties(message.Properties);
            message = newMessage;

            return messageBody;
        }
        public static string ReadRawBody(ref Message message)
        {
            XmlDictionaryReader bodyReader = message.GetReaderAtBodyContents();
            bodyReader.ReadStartElement("Binary");
            byte[] bodyBytes = bodyReader.ReadContentAsBase64();
            string messageBody = Encoding.UTF8.GetString(bodyBytes);

            // Now to recreate the message 
            MemoryStream ms = new MemoryStream();
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(ms);
            writer.WriteStartElement("Binary");
            writer.WriteBase64(bodyBytes, 0, bodyBytes.Length);
            writer.WriteEndElement();
            writer.Flush();
            ms.Position = 0;
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(ms, XmlDictionaryReaderQuotas.Max);
            Message newMessage = Message.CreateMessage(reader, int.MaxValue, message.Version);
            newMessage.Properties.CopyProperties(message.Properties);
            message = newMessage;

            return messageBody;
        }
        public static string CreateURIRequest(Message message, List<UriTemplate> uriTemplates, Uri listenUri)
        {
            HttpRequestMessageProperty httpProp = (HttpRequestMessageProperty)message.Properties[HttpRequestMessageProperty.Name];

            XElement root = new XElement(Request,
                new XAttribute(XNamespace.Xmlns + "ns0", BizTalkWebHttpNs.NamespaceName),
                new XAttribute("method", httpProp.Method));

            XElement paramsElement = new XElement(Params);
            root.Add(paramsElement);

            if (uriTemplates != null && uriTemplates.Count > 0) // UriTemplate exists. Eg /rest/firstname={fname}&lastname={lname}
            {
                bool templateMatch = false;
                foreach (var uriTemplate in uriTemplates)
                {
                    Uri baseUri = new Uri(message.Headers.To.ToString().Replace(message.Headers.To.AbsolutePath, string.Empty));
                    UriTemplateMatch results = uriTemplate.Match(listenUri, message.Headers.To);

                    if (results == null)
                        continue;

                    templateMatch = true;
                    foreach (string variableName in results.BoundVariables.Keys)
                    {
                        XElement paramElement = new XElement(BizTalkWebHttpNs + "param", new XAttribute("name", variableName));
                        paramElement.Value = results.BoundVariables[variableName];
                        paramsElement.Add(paramElement);
                    }

                    break;
                }
                if (!templateMatch)
                    throw new ApplicationException("Uri didn't match the template");
            }
            else // No uri template. Eg http://localhost/Orders/2012/10
            {
                string[] segments = message.Headers.To.ToString().Replace(listenUri.ToString(), string.Empty).Split('/');

                foreach (string val in segments)
                {
                    if (!string.IsNullOrEmpty(val))
                        paramsElement.Add(new XElement(Param).Value = val);
                }
            }
            return root.ToString();
        }
    }
}
