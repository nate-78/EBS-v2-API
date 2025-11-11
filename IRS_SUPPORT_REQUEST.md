# IRS AIR Help Desk Support Request

## Contact Information

**AIR Help Desk:**
- **Domestic (Toll-Free):** 1-866-937-4130
- **International:** 470-769-5100 (not toll-free)
- **Hours:** Standard business hours (confirm when calling)

**AIR Webpage:**
- https://www.irs.gov/e-file-providers/air/affordable-care-act-information-return-air-program

**Note:** The previous email address (airmailbox@irs.gov) was discontinued as of June 12, 2024, and all support requests should now go through the phone numbers above.

---

## Our Information

**Company:** Employer Benefits Services LLC
**Transmitter Control Code (TCC):** BB05S
**Application System ID (ASID):** 1BB05S01
**Software ID:** 25A0023165
**Testing Environment:** AATS (ACA Assurance Testing System)
**Tax Year:** 2025
**Form Type:** 1094-C/1095-C

---

## Issue Summary

We are consistently receiving **TPE1101** error ("invalid or outdated XML namespaces") from the AATS system when submitting test transmissions via the A2A channel. We have reviewed Publication 5258 (AIR Submission Composition and Reference Guide) Section 3 and made numerous corrections to align with IRS specifications, but the error persists.

**Error Code:** TPE1101
**Error Message:** "Our system detected invalid or outdated XML namespaces in your message. Please review the XML standards outlined in Section 3 of Publication 5258, AIR Submission Composition and Reference Guide..."

---

## Background and Actions Taken

We have systematically addressed multiple issues found through comparison with IRS documentation and provided examples:

### Fixes Already Implemented:
1. ✅ Added WS-Addressing headers (Action and To)
2. ✅ Corrected SOAP structure (Body as sibling of Header, not child)
3. ✅ Added required ASID to ACASecurityHeader/UserId
4. ✅ Fixed namespace URIs to use correct `ty25` for tax year 2025
5. ✅ Removed ContactNameGrp from CompanyInformationGrp (not in schema)
6. ✅ Added VendorNm to VendorInformationGrp
7. ✅ Fixed FormTypeCd from "1094/1095C" to "1094C"
8. ✅ Fixed PersonFirstNm/LastNm in VendorInformationGrp to use irs namespace
9. ✅ **Restructured XML Signature** to sign entire envelope with URI="" instead of specific elements
10. ✅ **Reordered Security element** so Timestamp comes BEFORE Signature (matching IRS example)
11. ✅ Changed signature KeyInfo to use X509Data/X509Certificate (not BinarySecurityToken)

### Current SOAP Structure:
Our SOAP message now matches the IRS soap_signed.xml example in most respects:
- WS-Security 1.0 specification compliance
- Timestamp before Signature in Security element
- Single Reference with URI="" signing entire envelope
- X509Certificate in KeyInfo/X509Data
- All required manifest elements present with correct namespaces

---

## Specific Questions for AIR Help Desk

### Question 1: Namespace Declaration Strategy

Publication 5258, Section 3.1 states:
> "Transmitters can assign their own prefix to each namespace, but should make sure that the usage matches the definition throughout the document."

However, we notice a difference between our implementation and the IRS soap_signed.xml example:

**Our Current Approach:**
```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:air="urn:us:gov:treasury:irs:ext:aca:air:ty25"
               xmlns:irs="urn:us:gov:treasury:irs:common"
               xmlns:acaBusHeader="urn:us:gov:treasury:irs:msg:acabusinessheader">
  <soap:Header>
    <acaBusHeader:ACABusinessHeader>
      <air:UniqueTransmissionId>...</air:UniqueTransmissionId>
      <irs:Timestamp>...</irs:Timestamp>
    </acaBusHeader:ACABusinessHeader>
  </soap:Header>
</soap:Envelope>
```

**IRS Example (soap_signed.xml):**
```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Header>
    <ACABusinessHeader xmlns="urn:us:gov:treasury:irs:msg:acabusinessheader">
      <UniqueTransmissionId xmlns="urn:us:gov:treasury:irs:ext:aca:air:ty25">...</UniqueTransmissionId>
      <Timestamp xmlns="urn:us:gov:treasury:irs:common">...</Timestamp>
    </ACABusinessHeader>
  </soap:Header>
</soap:Envelope>
```

**Question:** Does the AATS validator accept namespace prefixes declared at the Envelope level (our approach), or does it require inline default namespace declarations on each element (IRS example approach)? Both are semantically equivalent XML, but we want to confirm the validator's requirements.

---

### Question 2: Timestamp Id Attribute Namespace

We notice a difference in how the Timestamp Id attribute is declared:

**Our Current Implementation:**
```xml
<wsu:Timestamp wsu:Id="_1">
  <wsu:Created>2025-11-11T16:56:57Z</wsu:Created>
  <wsu:Expires>2025-11-11T17:01:57Z</wsu:Expires>
</wsu:Timestamp>
```

**IRS Example:**
```xml
<Timestamp Id="_1" xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
  <Created>2025-09-30T18:59:48Z</Created>
  <Expires>2025-09-30T19:04:48Z</Expires>
</Timestamp>
```

**Question:** Should the Id attribute on the Timestamp element be:
- Unprefixed (`Id="_1"`) - meaning it's a local attribute not in any namespace, OR
- Prefixed (`wsu:Id="_1"`) - meaning it's in the wsu namespace?

According to XML namespace specification, these are technically different. Does the validator have a preference?

---

### Question 3: Schema Version Requirements

**Question:** Are there specific schema version attributes or references we should include in our SOAP message for Tax Year 2025? We are using the namespace `urn:us:gov:treasury:irs:ext:aca:air:ty25` but want to confirm we're using the correct and most recent schema version.

---

### Question 4: Validation of Our Current Output

We have attached our current SOAP request (see generated_request.txt). This represents our best effort to comply with all requirements in Publication 5258 and match the IRS soap_signed.xml example.

**Question:** Could you please validate this SOAP structure and identify specifically what is triggering the TPE1101 error? Any guidance on what element, attribute, or namespace declaration is incorrect would be extremely helpful.

---

## Attachments

**File:** generated_request.txt (located in project root: /mnt/c/owensdev-git/EBS-v2-API/AcaApi.Poc/generated_request.txt)

This file contains:
1. The complete multipart MIME message with GZIP compression headers
2. The SOAP envelope with all headers (WS-Addressing, WS-Security, ACA headers)
3. The SOAP body with MTOM XOP reference
4. The Form Data File attachment (1094-C/1095-C XML)

**Key sections to review:**
- Line 6: Complete SOAP Envelope (currently one long line - may need to be formatted for readability)
- The SOAP envelope uses namespace prefixes declared at the Envelope level
- All required manifest elements are present
- Signature signs entire envelope with URI=""
- Timestamp comes before Signature in Security element

---

## What We Need

1. **Confirmation** on whether namespace prefixes declared at Envelope level are acceptable, or if we must use inline default namespaces

2. **Clarification** on the Timestamp Id attribute (prefixed vs unprefixed)

3. **Specific feedback** on what in our current SOAP message is triggering TPE1101

4. **Schema version** requirements for TY2025, if any

---

## Additional Context

We are a software development team building an ACA compliance solution. We have successfully resolved 10+ structural issues through careful comparison with IRS documentation and examples, but the TPE1101 error persists despite our SOAP structure now closely matching the IRS soap_signed.xml example.

The documentation states that namespace prefixes are allowed, but we want to ensure our implementation matches the validator's expectations before proceeding with either:
- Continuing with our current prefixed namespace approach, OR
- Refactoring to use inline default namespaces to exactly match the IRS example

Any guidance you can provide would be greatly appreciated and will help us successfully complete AATS testing.

---

## Technical Notes

**SOAP Formatting:** The SOAP envelope in generated_request.txt appears as one long line (line 6) due to how it's serialized. This is valid XML but may be difficult to read. If you need a formatted version for analysis, we can provide one or you can use an XML formatter/prettifier tool.

**Certificate Information:** The X.509 certificate included in the SOAP message is our valid IdenTrust IGC Device CA 2 certificate for api.ezebshub.com, issued September 11, 2025, expiring September 11, 2026.

**Test Data:** We are using test EINs and SSNs as specified in Publication 5164 (Test Package) for AATS testing.

---

## Preferred Response Method

Please contact us at: [INSERT YOUR CONTACT EMAIL/PHONE]

We are available for a conference call if that would help expedite resolution.

Thank you for your assistance.

---

## Document Checklist for IRS Support Call

When calling the AIR Help Desk, have ready:
- [x] This support request document (IRS_SUPPORT_REQUEST.md)
- [x] Current SOAP output (generated_request.txt)
- [x] TCC: BB05S
- [x] ASID: 1BB05S01
- [x] Software ID: 25A0023165
- [x] Error code: TPE1101
- [x] IRS example file: soap_signed.xml (for comparison)
- [x] Documentation analysis: DOCUMENTATION_FINDINGS.md
