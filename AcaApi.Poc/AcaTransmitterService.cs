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
            var tcc = _config.Tcc;
            var submissionEndpoint = _config.SubmissionEndpoint;

            var cid = Guid.NewGuid().ToString();
            (var soapEnvelope, var populatedFormDataBytes) = await BuildSoapEnvelopeFromTemplates(manifest, formDataFilePath, cid, tcc);

            var signedEnvelope = SignSoapEnvelope(soapEnvelope, certificate);

            await CompressAndSendRequestAsync(signedEnvelope, populatedFormDataBytes, Path.GetFileName(formDataFilePath), cid, submissionEndpoint);
        }

        private async Task<(XDocument, byte[])> BuildSoapEnvelopeFromTemplates(XDocument manifest, string formDataFilePath, string cid, string tcc)
        {
            // Step 1: Populate the Form Data from the template
            var populatedFormDataBytes = await PopulateFormData(formDataFilePath, manifest);

            // Step 2: Calculate checksum on the *populated* data
            var checksum = SHA256.Create().ComputeHash(populatedFormDataBytes);
            var checksumHex = BitConverter.ToString(checksum).Replace("-", "");

                        // Step 3: Define namespaces to exactly match IRS examples
            XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            XNamespace xop = "http://www.w3.org/2004/08/xop/include";
            
            var paymentYr = "2025"; // Per user instruction, hardcode for AATS
            var taxYearShort = paymentYr.Substring(2);

            // Match prefixes from p5258.txt
            XNamespace acaSoapReq = "urn:us:gov:treasury:irs:srv:gettransmitterbulkrequest";
            XNamespace acaBodyReq = "urn:us:gov:treasury:irs:msg:irsacabulkrequesttransmitter";
            XNamespace acaBusHeader = "urn:us:gov:treasury:irs:msg:acabusinessheader";
            XNamespace acaSecHdr = "urn:us:gov:treasury:irs:msg:acasecurityheader";
            XNamespace irs = "urn:us:gov:treasury:irs:common";
            XNamespace air = $"urn:us:gov:treasury:irs:ext:aca:air:ty{taxYearShort}";


            var ein = manifest.Root.Element("TransmitterInfo").Element("EIN").Value;
            var companyName = manifest.Root.Element("CompanyInformation").Element("CompanyName").Value;

            // Step 4: Construct SOAP Envelope with correct checksum
            var soapEnvelope = new XDocument(
                new XElement(soapenv + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soapenv", soapenv),
                    new XAttribute(XNamespace.Xmlns + "air", air),
                    new XAttribute(XNamespace.Xmlns + "irs", irs),
                    new XAttribute(XNamespace.Xmlns + "acaBusHeader", acaBusHeader),
                    new XAttribute(XNamespace.Xmlns + "acaBodyReq", acaBodyReq),
                    new XElement(soapenv + "Header",
                        new XElement(wsse + "Security",
                            new XAttribute(XNamespace.Xmlns + "wsse", wsse),
                            new XAttribute(XNamespace.Xmlns + "wsu", wsu),
                            new XElement(wsu + "Timestamp",
                                new XElement(wsu + "Created", DateTime.UtcNow.ToString("o")),
                                new XElement(wsu + "Expires", DateTime.UtcNow.AddMinutes(5).ToString("o"))
                            )
                        ),
                        new XElement(acaBusHeader + "ACABusinessHeader",
                            new XAttribute(wsu + "Id", "ACABusinessHeader"),
                            new XElement(air + "UniqueTransmissionId", $"{Guid.NewGuid()}:SYS12:{tcc}::T"),
                            new XElement(irs + "Timestamp", DateTime.UtcNow.ToString("o"))
                        ),
                        new XElement(air + "ACATransmitterManifestReqDtl",
                            new XAttribute(wsu + "Id", "ACATransmitterManifest"),
                            new XElement(air + "PaymentYr", paymentYr),
                            new XElement(air + "PriorYearDataInd", "0"),
                            new XElement(irs + "EIN", ein.Replace("-", "")),
                            new XElement(air + "TransmissionTypeCd", "O"),
                            new XElement(air + "TestFileCd", "T"),
                            new XElement(air + "TransmitterNameGrp",
                                new XElement(air + "BusinessNameLine1Txt", companyName)
                            ),
                            new XElement(air + "CompanyInformationGrp",
                                new XElement(air + "CompanyNm", companyName),
                                new XElement(air + "MailingAddressGrp",
                                    new XElement(air + "USAddressGrp",
                                        new XElement(air + "AddressLine1Txt", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value),
                                        new XElement(irs + "CityNm", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value),
                                        new XElement(air + "USStateCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value),
                                        new XElement(irs + "USZIPCd", manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value)
                                    )
                                ),
                                new XElement(air + "ContactNameGrp",
                                    new XElement(air + "PersonFirstNm", "John"),
                                    new XElement(air + "PersonLastNm", "Doe")
                                ),
                                new XElement(air + "ContactPhoneNum", manifest.Root.Element("CompanyInformation").Element("ContactPhone").Value)
                            ),
                            new XElement(air + "VendorInformationGrp",
                                new XElement(air + "VendorCd", "I"),
                                new XElement(air + "ContactNameGrp",
                                    new XElement(air + "PersonFirstNm", "Jane"),
                                    new XElement(air + "PersonLastNm", "Smith")
                                ),
                                new XElement(air + "ContactPhoneNum", "800-555-1212")
                            ),
                            new XElement(air + "TotalPayeeRecordCnt", manifest.Root.Element("TotalPayeeRecordCnt").Value),
                            new XElement(air + "TotalPayerRecordCnt", "1"),
                            new XElement(air + "SoftwareId", manifest.Root.Element("SoftwareId").Value),
                            new XElement(air + "FormTypeCd", "1094/1095C"),
                            new XElement(irs + "BinaryFormatCd", "application/xml"),
                            new XElement(irs + "ChecksumAugmentationNum", checksumHex),
                            new XElement(irs + "AttachmentByteSizeNum", populatedFormDataBytes.Length.ToString()),
                            new XElement(air + "DocumentSystemFileNm", Path.GetFileName(formDataFilePath))
                        ),
                        new XElement(acaSecHdr + "ACASecurityHeader",
                             new XAttribute(XNamespace.Xmlns + "acaSecHdr", acaSecHdr)
                        ),
                        new XElement(soapenv + "Body",
                            new XElement(acaBodyReq + "ACABulkRequestTransmitter",
                                new XAttribute("version", "1.0"),
                                new XElement(irs + "BulkExchangeFile",
                                    new XElement(xop + "Include",
                                        new XAttribute(XNamespace.Xmlns + "xop", xop),
                                        new XAttribute("href", $"cid:{cid}")
                                    )
                                )
                            )
                        )
                    )
                )
            );
            return (soapEnvelope, populatedFormDataBytes);
        }

        private async Task<byte[]> PopulateFormData(string formDataFilePath, XDocument manifest)
        {
            string formDataXml = await File.ReadAllTextAsync(formDataFilePath);

            var companyName = manifest.Root.Element("CompanyInformation").Element("CompanyName").Value;
            var ein = manifest.Root.Element("TransmitterInfo").Element("EIN").Value.Replace("-", "");
            var address = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value;
            var city = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value;
            var state = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value;
            var zip = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value;

            formDataXml = formDataXml.Replace("%%COMPANY_NAME%%", companyName);
            formDataXml = formDataXml.Replace("%%NAME_CONTROL%%", companyName.Substring(0, 4).ToUpper());
            formDataXml = formDataXml.Replace("%%EMPLOYER_EIN%%", ein);
            formDataXml = formDataXml.Replace("%%ADDRESS%%", address);
            formDataXml = formDataXml.Replace("%%CITY%%", city);
            formDataXml = formDataXml.Replace("%%STATE%%", state);
            formDataXml = formDataXml.Replace("%%ZIP%%", zip);
            formDataXml = formDataXml.Replace("%%EMPLOYEE_FIRST_NAME%%", "John");
            formDataXml = formDataXml.Replace("%%EMPLOYEE_LAST_NAME%%", "Doe");
            formDataXml = formDataXml.Replace("%%EMPLOYEE_SSN%%", "000000001");

            return Encoding.UTF8.GetBytes(formDataXml);
        }

        private XDocument SignSoapEnvelope(XDocument soapEnvelope, X509Certificate2 certificate)
        {
            XNamespace wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

            // Convert XDocument to XmlDocument
            XmlDocument xmlDoc = new XmlDocument();
            using (var xmlReader = soapEnvelope.CreateReader())
            {
                xmlDoc.Load(xmlReader);
            }

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("wsse", wsse.NamespaceName);
            namespaceManager.AddNamespace("wsu", wsu.NamespaceName);

            var securityElement = xmlDoc.SelectSingleNode("//wsse:Security", namespaceManager) as XmlElement;
            if (securityElement == null) throw new Exception("Security element not found.");

            var timestampElement = securityElement.SelectSingleNode("wsu:Timestamp", namespaceManager) as XmlElement;
            if (timestampElement == null) throw new Exception("Timestamp element not found for signing.");

            string timestampId = "TS-" + Guid.NewGuid().ToString("D");
            timestampElement.SetAttribute("Id", wsu.NamespaceName, timestampId);

            // 1. Create BinarySecurityToken
            string certId = "X509-" + Guid.NewGuid().ToString("D");
            var binarySecurityToken = xmlDoc.CreateElement("wsse", "BinarySecurityToken", wsse.NamespaceName);
            binarySecurityToken.SetAttribute("Id", wsu.NamespaceName, certId);
            binarySecurityToken.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            binarySecurityToken.SetAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary");
            binarySecurityToken.InnerText = Convert.ToBase64String(certificate.RawData);

            // 2. Set up the SignedXml object
            SignedXml signedXml = new SignedXmlWithId(xmlDoc);
            signedXml.SigningKey = certificate.GetRSAPrivateKey();
            signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/2001/10/xml-exc-c14n#";
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

            // 3. Create References to sign
            Reference referenceTimestamp = new Reference($"#{timestampId}");
            referenceTimestamp.AddTransform(new System.Security.Cryptography.Xml.XmlDsigEnvelopedSignatureTransform());
            referenceTimestamp.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(referenceTimestamp);

            Reference referenceManifest = new Reference("#ACATransmitterManifest");
            referenceManifest.AddTransform(new XmlDsigExcC14NTransform());
            referenceManifest.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(referenceManifest);

            Reference referenceBusinessHeader = new Reference("#ACABusinessHeader");
            referenceBusinessHeader.AddTransform(new XmlDsigExcC14NTransform());
            referenceBusinessHeader.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(referenceBusinessHeader);

            // 4. Create KeyInfo pointing to BinarySecurityToken
            KeyInfo keyInfo = new KeyInfo();
            var securityTokenReference = xmlDoc.CreateElement("wsse", "SecurityTokenReference", wsse.NamespaceName);
            var referenceElement = xmlDoc.CreateElement("wsse", "Reference", wsse.NamespaceName);
            referenceElement.SetAttribute("URI", $"#{certId}");
            referenceElement.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            securityTokenReference.AppendChild(referenceElement);
            keyInfo.AddClause(new KeyInfoNode(securityTokenReference));
            signedXml.KeyInfo = keyInfo;

            // 5. Compute signature and assemble the security header
            signedXml.ComputeSignature();
            XmlElement signatureElement = signedXml.GetXml();

            securityElement.PrependChild(signatureElement);
            securityElement.PrependChild(binarySecurityToken);

            using (var nodeReader = new System.Xml.XmlNodeReader(xmlDoc))
            {
                return XDocument.Load(nodeReader);
            }
        }

        private async Task CompressAndSendRequestAsync(XDocument signedEnvelope, byte[] formDataBytes, string formDataFileName, string cid, string submissionEndpoint)
        {
            Console.WriteLine("Compressing and sending request...");

            var boundary = $"----=_Part_{Guid.NewGuid()}";

            byte[] requestBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, 1024, true))
                {
                    // Write SOAP part
                    writer.WriteLine($"--{boundary}");
                    writer.WriteLine("Content-Type: application/xop+xml; charset=UTF-8; type=\"text/xml\"");
                    writer.WriteLine("Content-Transfer-Encoding: 8bit");
                    writer.WriteLine($"Content-ID: <soapui-body-content@soapui.org>");
                    writer.WriteLine();
                    signedEnvelope.Save(writer, SaveOptions.DisableFormatting);
                    writer.WriteLine();

                    // Write Attachment part
                    writer.WriteLine($"--{boundary}");
                    writer.WriteLine("Content-Type: application/xml");
                    writer.WriteLine("Content-Transfer-Encoding: binary");
                    writer.WriteLine($"Content-ID: <{cid}>");
                    writer.WriteLine($"Content-Disposition: attachment; name=\"{formDataFileName}\"");
                    writer.WriteLine();
                    writer.Flush(); 
                    
                    memoryStream.Write(formDataBytes, 0, formDataBytes.Length);
                    writer.WriteLine();
                    writer.WriteLine($"--{boundary}--");
                }
                requestBytes = memoryStream.ToArray();
            }


            byte[] compressedBytes;
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionMode.Compress))
                {
                    gzipStream.Write(requestBytes, 0, requestBytes.Length);
                }
                compressedBytes = outputStream.ToArray();
            }

            var request = new HttpRequestMessage(HttpMethod.Post, submissionEndpoint);
            request.Content = new ByteArrayContent(compressedBytes);
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse($"multipart/related; boundary=\"{boundary}\"; type=\"application/xop+xml\"; start-info=\"text/xml\"");

            request.Content.Headers.ContentEncoding.Add("gzip");
            request.Headers.Add("SOAPAction", "BulkRequestTransmitter");

            Console.WriteLine("Sending SOAP request:\n" + Encoding.UTF8.GetString(requestBytes));
            
            // File.WriteAllBytes("generated_request.txt", requestBytes);
            // Console.WriteLine("\n****** REQUEST SAVED TO generated_request.txt FOR REVIEW ******\n");

            var response = await _httpClient.SendAsync(request);

            Console.WriteLine($"\nResponse Status Code: {response.StatusCode}");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Response Content:\n" + responseContent);
            // await Task.CompletedTask;
        }
    }
}