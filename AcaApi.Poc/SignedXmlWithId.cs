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

            // If the standard method doesn't work, try a more specific query for the known elements that use wsu:Id.
            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            namespaceManager.AddNamespace("urn", "urn:us:gov:treasury:irs:ext:aca:air:ty25"); // Assuming ty25, adjust if needed
            namespaceManager.AddNamespace("acaBusHeader", "urn:us:gov:treasury:irs:msg:acabusinessheader");

            string query = $"//*[@wsu:Id='{idValue}']";
            element = document.SelectSingleNode(query, namespaceManager) as XmlElement;

            // Ensure the found element is one of the types we expect to sign
            if (element != null && (element.LocalName == "Timestamp" || element.LocalName == "ACATransmitterManifestReqDtl" || element.LocalName == "ACABusinessHeader"))
            {
                return element;
            }

            return null;
        }
    }
}
