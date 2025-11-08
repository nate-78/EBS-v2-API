# IRS ACA A2A API Proof-of-Concept

This project is a proof-of-concept demonstrating how to successfully communicate with the IRS Affordable Care Act (ACA) Application-to-Application (A2A) web service.

## The Challenge: GZIP, MTOM, and WS-Security

The primary technical challenge in communicating with the IRS A2A service is its unique combination of security and encoding requirements, which are not easily met by standard .NET libraries like WCF (Windows Communication Foundation).

The service requires the client to send a SOAP 1.1 message that is simultaneously:
1.  **Signed** using WS-Security with an X.509 certificate.
2.  **Compressed** using GZIP.
3.  **Encoded** using MTOM (Message Transmission Optimization Mechanism) for the attached form data.

Initial attempts using a standard WCF client with a custom binding failed. The WCF channel stack could not correctly orchestrate the signing, compression, and encoding in the precise manner the IRS service expects. This resulted in persistent transport-level errors, most notably a `FaultException` indicating that the message must be sent with GZIP compression.

## The Solution: Manual SOAP Request Construction

To overcome the limitations of the WCF framework, we abandoned the auto-generated client and implemented a manual, low-level approach to building and sending the SOAP request. This gives us complete and explicit control over every part of the message construction and transmission process.

The solution involves the following steps, executed in order:

1.  **Build the SOAP Envelope**: The entire SOAP 1.1 message, including all headers and the body, is constructed manually as an XML document using `System.Xml.Linq.XDocument`.
2.  **Sign the Message**: The SOAP message is signed according to WS-Security standards. This is handled by the `System.Security.Cryptography.Xml.SignedXml` class. A custom helper class was created to handle a specific nuance of the IRS's XML schema.
3.  **Compress the Message**: The final, signed XML document is converted to a byte array and compressed using `System.IO.Compression.GZipStream`.
4.  **Send the Request**: The compressed byte array is sent as the body of an HTTP POST request using `System.Net.Http.HttpClient`. The necessary HTTP headers (`Content-Type: text/xml`, `Content-Encoding: gzip`, `SOAPAction`) are set manually.

This approach completely bypasses the WCF channel stack and resolves the GZIP/MTOM/Security integration issue.

### Key Files

The implementation of this manual approach is primarily contained in the following files:

-   `AcaApi.Poc/AcaTransmitterService.cs`: This is the core file containing the logic. The `TransmitAsync` method orchestrates the entire process, calling helper methods within the same file to build, sign, and send the request.
-   `AcaApi.Poc/SignedXmlWithId.cs`: This is a small helper class that inherits from `SignedXml`. It was created to correctly resolve the `wsu:Id` attribute on the `<Timestamp>` element during the signing process, which the base `SignedXml` class cannot do on its own.

Care should be taken when modifying these files, as they are critical to the successful transmission of data to the IRS.
