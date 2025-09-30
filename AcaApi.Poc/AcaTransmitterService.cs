using AcaClient;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AcaApi.Poc
{
    public class AcaTransmitterService
    {
        private readonly AppConfig _config;

        public AcaTransmitterService(AppConfig config)
        {
            _config = config;
        }

        public async Task<ACABulkRequestTransmitterResponseType> TransmitAsync(string formDataFilePath, string manifestFilePath)
        {
            Console.WriteLine("Creating WCF client with Custom Binding...");

            // 1. Create the binding elements
            var messageEncoding = new MtomMessageEncodingBindingElement(MessageVersion.Soap11WSAddressing10, Encoding.UTF8);
            var gzipEncoding = new GzipMessageEncoderBindingElement(messageEncoding);
            var security = SecurityBindingElement.CreateCertificateOverTransportBindingElement(MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10);
            var transport = new HttpsTransportBindingElement();

            // 2. Create the custom binding
            var binding = new CustomBinding(gzipEncoding, security, transport);

            // 3. Create the endpoint
            var endpoint = new EndpointAddress(_config.SubmissionEndpoint);

            // 4. Create the client
            var client = new BulkRequestTransmitterPortTypeClient(binding, endpoint);

            // 5. Set client certificate
            var certificate = new X509Certificate2(_config.CertificatePath, _config.CertificatePassword, X509KeyStorageFlags.MachineKeySet);
            client.ClientCredentials.ClientCertificate.Certificate = certificate;

            // 6. Load manifest and form data
            var manifest = XDocument.Load(manifestFilePath);
            var formData = await File.ReadAllBytesAsync(formDataFilePath);
            var checksum = SHA256.Create().ComputeHash(formData);

            // 7. Construct the request object
            var securityHeader = new TransmitterACASecurityHeaderType { Item = "your_asid_here" };
            var businessHeader = new ACABulkBusinessHeaderRequestType
            {
                UniqueTransmissionId = Guid.NewGuid() + "::T",
                Timestamp = DateTime.UtcNow
            };
            var manifestDetail = new ACATrnsmtManifestReqDtlType
            {
                PaymentYr = manifest.Root.Element("PaymentYr").Value,
                PriorYearDataInd = (DigitBooleanType)Enum.Parse(typeof(DigitBooleanType), "Item" + manifest.Root.Element("PriorYearDataInd").Value),
                EIN = manifest.Root.Element("TransmitterInfo").Element("EIN").Value,
                TransmissionTypeCd = TransmissionTypeCdType.O,
                TestFileCd = "T",
                TransmitterNameGrp = new BusinessNameType
                {
                    BusinessNameLine1Txt = manifest.Root.Element("CompanyInformation").Element("CompanyName").Value
                },
                CompanyInformationGrp = new CompanyInformationGrpType
                {
                    CompanyNm = manifest.Root.Element("CompanyInformation").Element("CompanyName").Value,
                    MailingAddressGrp = new BusinessAddressGrpType
                    {
                        Item = new USAddressGrpType
                        {
                            AddressLine1Txt = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("AddressLine1").Value,
                            CityNm = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("City").Value,
                            USStateCd = (AcaClient.StateType)Enum.Parse(typeof(AcaClient.StateType), manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("State").Value),
                            USZIPCd = manifest.Root.Element("CompanyInformation").Element("MailingAddress").Element("Zip").Value
                        }
                    },
                    ContactPhoneNum = manifest.Root.Element("CompanyInformation").Element("ContactPhone").Value
                },
                VendorInformationGrp = new VendorInformationGrpType
                {
                    VendorCd = "I", // Default to 'I' for Independent
                    ContactNameGrp = new OtherCompletePersonNameType
                    {
                        PersonFirstNm = "Jane",
                        PersonLastNm = "Smith"
                    },
                    ContactPhoneNum = manifest.Root.Element("VendorInformation").Element("ContactPhone").Value
                },
                TotalPayeeRecordCnt = manifest.Root.Element("TotalPayeeRecordCnt").Value,
                TotalPayerRecordCnt = manifest.Root.Element("TotalPayerRecordCnt").Value,
                SoftwareId = manifest.Root.Element("SoftwareId").Value,
                FormTypeCd = (FormNameType)Enum.Parse(typeof(FormNameType), "Item" + manifest.Root.Element("FormType").Value.Replace("/", "")),
                BinaryFormatCd = BinaryFormatCodeType.applicationxml,
                ChecksumAugmentationNum = Convert.ToBase64String(checksum),
                AttachmentByteSizeNum = formData.Length.ToString(),
                DocumentSystemFileNm = Path.GetFileName(formDataFilePath)
            };
            var bulkRequest = new ACABulkRequestTransmitterType
            {
                BulkExchangeFile = formData
            };

            Console.WriteLine("Sending request via WCF client...");
            var response = await client.BulkRequestTransmitterAsync(securityHeader, null, businessHeader, manifestDetail, bulkRequest);

            return response.ACABulkRequestTransmitterResponse;
        }
    }
}