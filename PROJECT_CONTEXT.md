# EBS-v2 API Project Context

This document serves as a central reference for the EBS-v2 API rewrite project. It contains the project plan, technical specifications, and key decisions to ensure alignment between all parties.

## 1. Project Synopsis

The primary goal of this project is to rewrite the core functionality of the legacy .NET applications found in the `EBS-Legacy` repository. The new system will automate the process of submitting Affordable Care Act (ACA) 1094-C and 1095-C forms to the IRS on behalf of EBS clients.

The rewrite will be split into two new repositories:
- **`EBS-v2-API`**: A modern .NET API to handle all backend logic.
- **`EBS-v2-UI`**: A modern web front-end for user interaction.

This project focuses on the **`EBS-v2-API`**.

## 2. High-Level Project Plan

The project will be executed in the following phases:

1.  **Phase 0: Proof-of-Concept (POC):** De-risk the project by building a minimal .NET application to prove successful communication with the IRS A2A SOAP service, focusing on the `WS-Security` certificate authentication.
2.  **Phase 1: Core Logic & Validation:** Re-implement the business logic for reading and validating the client data spreadsheet.
3.  **Phase 2: PDF & XML Generation:** Implement logic for creating printable 1095-C PDF forms and the submission-ready XML data file.
4.  **Phase 3: API Scaffolding:** Build the primary API endpoints for file uploads, asynchronous job management, and retrieving results.
5.  **Phase 4: Full Integration:** Integrate the successful POC logic into the main API to handle live submissions and status checks.

## 3. Proof-of-Concept (POC) Detailed Plan

The immediate next step is to execute Phase 0.

1.  **Project Setup:** Create a new .NET 8 Console Application named `AcaApi.Poc` in the `EBS-v2-API` repository.
2.  **WSDL Import:** Use `dotnet-svcutil` to process the provided WSDL files, auto-generating the C# SOAP client classes.
3.  **App Configuration:** Use an `appsettings.json` file to manage configuration details like endpoint URLs and the certificate path/password.
4.  **Message Assembly:** Develop a `Transmitter` class to load sample data and assemble the SOAP request with an MTOM attachment.
5.  **WS-Security Implementation:** Develop a `SecurityHeader` class to load the X.509 certificate and perform the required XML digital signatures on the SOAP message.
6.  **Execution & Verification:** The main program will send a test submission to the IRS AATS endpoint and print the full request and response for verification.

## 4. Key Technical Specifications

*   **API Technology:** .NET 8 (LTS)
*   **Database:** Relational (Azure SQL proposed)
*   **Protocol:** SOAP over HTTPS
*   **Message Format:** SOAP 1.1 with MTOM attachments
*   **Authentication:** WS-Security using X.509 Certificates to sign message parts.
*   **Key Documents:**
    *   **XSDs:** `EBS-Legacy\_documents\1094-1095 XML and XSDs`
    *   **WSDLs:** `EBS-Legacy\_documents\wsdl`
    *   **Business Rules:** `EBS-Legacy\_documents\irs-aca-business-rules`
    *   **IRS Pub 5165:** Primary guide for A2A communication.


