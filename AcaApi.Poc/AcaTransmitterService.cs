using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
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

        public async Task TransmitAsync(string formDataFilePath, string manifestFilePath)
        {
            var manifest = XDocument.Load(manifestFilePath);
            var certificate = new X509Certificate2(_config.CertificatePath, _config.CertificatePassword, X509KeyStorageFlags.MachineKeySet);

            var soapEnvelope = await BuildSoapEnvelopeFromTemplates(manifest, formDataFilePath);

            var signedEnvelope = SignSoapEnvelope(soapEnvelope, certificate);

            await CompressAndSendRequestAsync(signedEnvelope);
        }

        private async Task<XDocument> BuildSoapEnvelopeFromTemplates(XDocument manifest, string formDataFilePath)
        {
            // 1. Read and populate Form Data template
            var formDataTemplate = await File.ReadAllTextAsync("AcaApi.Poc/Correct-FormData-Template.xml");

            var companyName = manifest.Root.Element("CompanyInformation").Element("CompanyName").Value;
            var ein = manifest.Root.Element("TransmitterInfo").Element("EIN").Value;
            var nameControl = companyName.Length > 4 ? companyName.Substring(0, 4) : companyName;

            var populatedFormData = formDataTemplate
                .Replace("%%COMPANY_NAME%%", companyName)
                .Replace("%%NAME_CONTROL%%", nameControl.ToUpper())
                .Replace("%%EMPLOYER_EIN%%", ein.Replace("-", ""))
                .Replace("%%ADDRESS%%", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value)
                .Replace("%%CITY%%", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value)
                .Replace("%%STATE%%", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value)
                .Replace("%%ZIP%%", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value)
                .Replace("%%EMPLOYEE_FIRST_NAME%%", "Gavin")
                .Replace("%%EMPLOYEE_LAST_NAME%%", "Gavin")
                .Replace("%%EMPLOYEE_SSN%%", "000000401");

            var formDataBytes = Encoding.UTF8.GetBytes(populatedFormData);
            var checksum = SHA256.Create().ComputeHash(formDataBytes);

            // 2. Define namespaces for SOAP envelope
            XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace wsa = "http://www.w3.org/2005/08/addressing";
            XNamespace wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            XNamespace irsMSG = "urn:us:gov:treasury:irs:msg:irsacabulkrequesttransmitter";
            XNamespace irs = "urn:us:gov:treasury:irs:common";
            XNamespace irsHDR = "urn:us:gov:treasury:irs:msg:acabusinessheader";
            XNamespace irsTY = "urn:us:gov:treasury:irs:ext:aca:air:ty25";

            // 3. Construct SOAP Envelope directly
            var soapEnvelope = new XDocument(
                new XElement(s + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "s", s),
                    new XElement(s + "Header",
                        new XElement(wsa + "Action", "BulkRequestTransmitter", new XAttribute(s + "mustUnderstand", "1")),
                        new XElement(wsse + "Security",
                            new XAttribute(s + "mustUnderstand", "1"),
                            new XElement(wsu + "Timestamp",
                                new XElement(wsu + "Created", DateTime.UtcNow.ToString("o")),
                                new XElement(wsu + "Expires", DateTime.UtcNow.AddMinutes(5).ToString("o"))
                            )
                        ),
                        new XElement(irsHDR + "ACABusinessHeader",
                                                    new XElement("UniqueTransmissionId", $"{Guid.NewGuid()}:SYS12:{_config.Tcc}::T"),
                                                    new XElement("Timestamp", DateTime.UtcNow.ToString("o")),
                                                    new XElement(irsTY + "ACATransmitterManifestReqDtl",
                                                        new XElement(irsTY + "PaymentYr", "2025"),
                                                        new XElement(irsTY + "PriorYearDataInd", "0"),
                                                        new XElement(irs + "EIN", ein.Replace("-", "")),
                                                        new XElement(irsTY + "TransmissionTypeCd", "O"),
                                                        new XElement(irsTY + "TestFileCd", "T"),
                                                        new XElement(irsTY + "TransmitterNameGrp",
                                                            new XElement(irsTY + "BusinessNameLine1Txt", companyName)
                                                        ),
                                                        new XElement(irsTY + "CompanyInformationGrp",
                                                            new XElement(irsTY + "CompanyNm", companyName),
                                                            new XElement(irsTY + "MailingAddressGrp",
                                                                new XElement(irsTY + "USAddressGrp",
                                                                    new XElement(irsTY + "AddressLine1Txt", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value),
                                                                    new XElement(irs + "CityNm", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value),
                                                                    new XElement(irsTY + "USStateCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value),
                                                                    new XElement(irs + "USZIPCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value)
                                                                )
                                                            ),
                                                            new XElement(irsTY + "ContactNameGrp",
                                                                new XElement(irsTY + "PersonFirstNm", "John"),
                                                                new XElement(irsTY + "PersonLastNm", "Doe")
                                                            ),
                                                            new XElement(irsTY + "ContactPhoneNum", manifest.Root.Element("CompanyInformation").Element("ContactPhone").Value)
                                                        ),
                                                        new XElement(irsTY + "VendorInformationGrp",
                                                            new XElement(irsTY + "VendorCd", "I"),
                                                            new XElement(irsTY + "ContactNameGrp",
                                                                new XElement(irsTY + "PersonFirstNm", "Jane"),
                                                                new XElement(irsTY + "PersonLastNm", "Smith")
                                                            ),
                                                            new XElement(irsTY + "ContactPhoneNum", "800-555-1212")
                                                        ),
                                                        new XElement(irsTY + "TotalPayeeRecordCnt", manifest.Root.Element("TotalPayeeRecordCnt").Value),
                                                        new XElement(irsTY + "TotalPayerRecordCnt", "1"),
                                                        new XElement(irsTY + "SoftwareId", manifest.Root.Element("SoftwareId").Value),
                                                        new XElement(irsTY + "FormTypeCd", "1094/1095C"),
                                                        new XElement(irs + "BinaryFormatCd", "application/xml"),
                                                        new XElement(irs + "ChecksumAugmentationNum", Convert.ToBase64String(checksum)),
                                                        new XElement(irs + "AttachmentByteSizeNum", formDataBytes.Length.ToString()),
                                                        new XElement(irsTY + "DocumentSystemFileNm", Path.GetFileName(formDataFilePath))
                                                    )
                                                ),
                    new XElement(s + "Body",
                        new XElement(irsMSG + "ACABulkRequestTransmitter",
                            new XAttribute("version", "1.0"),
                            new XElement(irs + "BulkExchangeFile", Convert.ToBase64String(formDataBytes))
                        )
                    )
                )
            ));
            return soapEnvelope;
        }

        private XDocument SignSoapEnvelope(XDocument soapEnvelope, X509Certificate2 certificate)
        {
            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            var timestamp = soapEnvelope.Descendants(wsu + "Timestamp").FirstOrDefault();
            if (timestamp == null) throw new Exception("Timestamp element not found for signing.");

            string timestampId = "TS-" + Guid.NewGuid().ToString("D");
            timestamp.Add(new XAttribute(wsu + "Id", timestampId));

            var xmlDoc = new System.Xml.XmlDocument();
            using (var xmlReader = soapEnvelope.CreateReader())
            {
                xmlDoc.Load(xmlReader);
            }

            SignedXml signedXml = new SignedXmlWithId(xmlDoc);
            signedXml.SigningKey = certificate.GetRSAPrivateKey();

            signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/2001/10/xml-exc-c14n#";
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

            Reference reference = new Reference();
            reference.Uri = "#" + timestampId;
            reference.AddTransform(new System.Security.Cryptography.Xml.XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new System.Security.Cryptography.Xml.XmlDsigExcC14NTransform());
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            
            signedXml.AddReference(reference);

            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();

            XmlElement signatureElement = signedXml.GetXml();

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            var securityElement = xmlDoc.SelectSingleNode("//wsse:Security", namespaceManager) as XmlElement;
            securityElement.AppendChild(signatureElement);

            using (var nodeReader = new System.Xml.XmlNodeReader(xmlDoc))
            {
                return XDocument.Load(nodeReader);
            }
        }

        private async Task CompressAndSendRequestAsync(XDocument signedEnvelope)
        {
            Console.WriteLine("Compressing and sending request...");

            byte[] xmlBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new System.Xml.XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    signedEnvelope.WriteTo(writer);
                }
                xmlBytes = memoryStream.ToArray();
            }

            byte[] compressedBytes;
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionMode.Compress))
                {
                    gzipStream.Write(xmlBytes, 0, xmlBytes.Length);
                }
                compressedBytes = outputStream.ToArray();
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _config.SubmissionEndpoint);
            request.Content = new ByteArrayContent(compressedBytes);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
            request.Content.Headers.ContentEncoding.Add("gzip");
            request.Headers.Add("SOAPAction", "BulkRequestTransmitter");

            Console.WriteLine("Sending SOAP request:\n" + signedEnvelope.ToString());
            var response = await _httpClient.SendAsync(request);

            Console.WriteLine($"\nResponse Status Code: {response.StatusCode}");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Response Content:\n" + responseContent);
        }
    }
}