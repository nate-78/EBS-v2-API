using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AcaApi.Poc
{
    public class AcaTransmitterService
    {
        private readonly AppConfig _config;
        private static readonly HttpClient _httpClient = new HttpClient();

        public AcaTransmitterService(AppConfig config)
        {
            _config = config;
        }

        public async Task<string> TransmitAsync(string formDataFilePath, string manifestFilePath)
        {
            string contentId = "cid:" + Guid.NewGuid().ToString();
            
            // 1. Build the core SOAP envelope
            XDocument soapEnvelope = BuildSoapEnvelope(manifestFilePath, contentId);
            Console.WriteLine("SOAP Envelope built.");
            File.WriteAllText("soap_unsigned.xml", soapEnvelope.ToString());

            // 2. Apply WS-Security signature
            SignEnvelope(ref soapEnvelope);
            Console.WriteLine("SOAP Envelope signed.");
            File.WriteAllText("soap_signed.xml", soapEnvelope.ToString());

            // 3. Construct the MTOM message
            var requestContent = CreateMtomMessage(soapEnvelope, formDataFilePath, contentId);
            Console.WriteLine("MTOM message constructed.");

            // 4. Send the request
            Console.WriteLine($"Sending request to {_config.SubmissionEndpoint}...");
            var response = await SendRequestAsync(requestContent);
            Console.WriteLine("Request sent.");

            return response;
        }

        private async Task<string> SendRequestAsync(HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _config.SubmissionEndpoint);
            request.Content = content;
            request.Headers.Add("SOAPAction", "\"BulkRequestTransmitter\"");

            var response = await _httpClient.SendAsync(request);

            return await response.Content.ReadAsStringAsync();
        }

        private HttpContent CreateMtomMessage(XDocument soapEnvelope, string formDataFilePath, string contentId)
        {
            var boundary = "----=_Part_0_" + Guid.NewGuid().ToString().Replace("-", "");
            var multipartContent = new MultipartContent("related", boundary);
            
            // Add the SOAP part
            var soapContent = new StringContent(soapEnvelope.ToString(), System.Text.Encoding.UTF8, "application/xop+xml");
            soapContent.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("type", "\"text/xml\""));
            soapContent.Headers.Add("Content-ID", "<soap-envelope>");
            multipartContent.Add(soapContent);

            // Add the binary (form data) part
            var fileStream = new FileStream(formDataFilePath, FileMode.Open, FileAccess.Read);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.Add("Content-ID", $"<{contentId.Replace("cid:", "")}>");
            streamContent.Headers.Add("Content-Transfer-Encoding", "binary");
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            multipartContent.Add(streamContent);

            return multipartContent;
        }

        private XDocument BuildSoapEnvelope(string manifestFilePath, string contentId)
        {
            var manifest = XDocument.Load(manifestFilePath);
            
            // Define Namespaces
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace wsa = "http://www.w3.org/2005/08/addressing";
            XNamespace wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-ext-1.0.xsd";
            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            XNamespace acaBusHeader = "urn:us:gov:treasury:irs:msg:acabusinessheader";
            XNamespace acaSecHeader = "urn:us:gov:treasury:irs:msg:acasecurityheader";
            XNamespace acaMsg = "urn:us:gov:treasury:irs:msg:irsacabulkrequesttransmitter";
            XNamespace manifestNs = "urn:us:gov:treasury:irs:ext:aca:air:ty24";
             XNamespace xop = "http://www.w3.org/2004/08/xop/include";

            var soapEnvelope = new XDocument(
                new XElement(soap + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
                    new XElement(soap + "Header",
                        new XElement(wsa + "Action", "BulkRequestTransmitter"),
                        new XElement(wsa + "To", _config.SubmissionEndpoint),
                        new XElement(wsse + "Security",
                            new XElement(wsu + "Timestamp",
                                new XAttribute(wsu + "Id", "_1"),
                                new XElement(wsu + "Created", DateTime.UtcNow.ToString("o")),
                                new XElement(wsu + "Expires", DateTime.UtcNow.AddMinutes(5).ToString("o"))
                            )
                        ),
                        new XElement(acaSecHeader + "ACASecurityHeader",
                            new XElement("UserId", "your_asid_here")
                        ),
                        new XElement(acaBusHeader + "ACABusinessHeader",
                            new XElement("UniqueTransmissionId", Guid.NewGuid() + "::T"),
                            new XElement("Timestamp", DateTime.UtcNow.ToString("o"))
                        ),
                        new XElement(manifestNs + "ACATransmitterManifestReqDtl",
                            new XElement("PaymentYr", manifest.Root.Element("PaymentYr").Value),
                            new XElement("PriorYearDataInd", manifest.Root.Element("PriorYearDataInd").Value),
                            new XElement("EIN", manifest.Root.Element("TransmitterInfo").Element("EIN").Value),
                            new XElement("TransmissionTypeCd", "O"),
                            new XElement("TestFileCd", "T"),
                            new XElement("TransmitterNameGrp", new XElement("BusinessNameLine1Txt", manifest.Root.Element("CompanyInformation").Element("CompanyName").Value)),
                            new XElement("CompanyInformationGrp",
                                new XElement("CompanyNm", manifest.Root.Element("CompanyInformation").Element("CompanyName").Value),
                                new XElement("MailingAddressGrp", new XElement("USAddressGrp",
                                    new XElement("AddressLine1Txt", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value),
                                    new XElement("CityNm", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value),
                                    new XElement("USStateCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value),
                                    new XElement("USZIPCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value)
                                )),
                                new XElement("ContactPhoneNum", manifest.Root.Element("CompanyInformation").Element("ContactPhone").Value)
                            ),
                            new XElement("VendorInformationGrp",
                                new XElement("VendorNm", manifest.Root.Element("VendorInformation").Element("VendorName").Value),
                                new XElement("ContactNameGrp", new XElement("PersonFirstNm", "Jane"), new XElement("PersonLastNm", "Smith")),
                                new XElement("ContactPhoneNum", manifest.Root.Element("VendorInformation").Element("ContactPhone").Value)
                            ),
                            new XElement("TotalPayeeRecordCnt", manifest.Root.Element("TotalPayeeRecordCnt").Value),
                            new XElement("TotalPayerRecordCnt", manifest.Root.Element("TotalPayerRecordCnt").Value),
                            new XElement("SoftwareId", manifest.Root.Element("SoftwareId").Value),
                            new XElement("FormTypeCd", "1094C")
                        )
                    ),
                    new XElement(soap + "Body",
                        new XElement(acaMsg + "ACABulkRequestTransmitter",
                            new XElement("BulkExchangeFile",
                                new XElement(xop + "Include", new XAttribute("href", contentId))
                            )
                        )
                    )
                )
            );
            return soapEnvelope;
        }

        private void SignEnvelope(ref XDocument soapEnvelope)
        {
            Console.WriteLine("Signing SOAP Envelope...");
            var certificate = new X509Certificate2(_config.CertificatePath, _config.CertificatePassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            
            var xmlDoc = new XmlDocument();
            using (var xmlReader = soapEnvelope.CreateReader())
            {
                xmlDoc.Load(xmlReader);
            }

            var signedXml = new SignedXml(xmlDoc);
            signedXml.SigningKey = certificate.GetRSAPrivateKey();
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            var reference = new Reference();
            reference.Uri = "#_1";
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(reference);

            var keyInfo = new KeyInfo();
            var keyInfoData = new KeyInfoX509Data(certificate);
            keyInfo.AddClause(keyInfoData);
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();
            XmlElement signatureXml = signedXml.GetXml();

            var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-ext-1.0.xsd");
            var securityHeader = xmlDoc.SelectSingleNode("//wsse:Security", nsManager) as XmlElement;
            securityHeader.AppendChild(xmlDoc.ImportNode(signatureXml, true));

            using (var nodeReader = new XmlNodeReader(xmlDoc))
            {
                soapEnvelope = XDocument.Load(nodeReader);
            }
        }
    }
}