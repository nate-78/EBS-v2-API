using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Xml;

namespace AcaApi.Poc
{
    public class MessageInspector : IClientMessageInspector
    {
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            Console.WriteLine("Received SOAP response:");
            Console.WriteLine(reply.ToString());
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            Console.WriteLine("Sending SOAP request:");
            Console.WriteLine(request.ToString());
            return null;
        }
    }
}
