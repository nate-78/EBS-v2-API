using System;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace AcaApi.Poc
{
    public class SignedXmlWithId : SignedXml
    {
        public SignedXmlWithId(XmlDocument document) : base(document)
        {
        }

        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            // Check the standard ID behavior first.
            var element = base.GetIdElement(document, idValue);
            if (element != null)
            {
                return element;
            }

            // If the standard method doesn't work, try to find an element with a wsu:Id attribute.
            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            namespaceManager.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            element = document.SelectSingleNode($"//wsse:Security/wsu:Timestamp[@wsu:Id='{idValue}']", namespaceManager) as XmlElement;

            return element;
        }
    }
}
