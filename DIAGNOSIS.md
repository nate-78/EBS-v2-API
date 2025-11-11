# Comprehensive SOAP Message Diagnosis

## Comparison: Our Output vs IRS Example (soap_signed.xml)

---

## 🔴 CRITICAL ISSUES

### 1. **MISSING WS-Addressing Headers**
**IRS Example:**
```xml
<soap:Header>
  <Action xmlns="http://www.w3.org/2005/08/addressing">BulkRequestTransmitter</Action>
  <To xmlns="http://www.w3.org/2005/08/addressing">https://la.www4.irs.gov/airp/aca/a2a/1095BC_Transmission_AATS</To>
  <Security ...>
```

**Our Output:**
```xml
<soap:Header>
  <wsse:Security ...>
```

**Issue:** We are completely missing the WS-Addressing `<Action>` and `<To>` header elements. These must come BEFORE the Security element.

---

### 2. **Namespace Declaration Strategy**
**IRS Example:**
```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <!-- Only soap namespace at envelope level -->
  <!-- Other namespaces declared inline where first used -->
```

**Our Output:**
```xml
<soap:Envelope xmlns:soap="..."
               xmlns:air="..."
               xmlns:irs="..."
               xmlns:acaBusHeader="..."
               xmlns:acaBodyReq="..."
               xmlns:wsu="...">
  <!-- All namespaces declared at envelope level, then used as prefixes -->
```

**Issue:** IRS declares namespaces inline on elements, not as prefixes at Envelope level. Each element declares its own default xmlns.

---

### 3. **Signature Structure - COMPLETELY DIFFERENT**
**IRS Example:**
```xml
<Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
  <SignedInfo>
    <Reference URI="">  <!-- Empty URI signs entire envelope -->
      <Transforms>
        <Transform Algorithm="enveloped-signature" />
        <Transform Algorithm="exc-c14n#" />
      </Transforms>
    </Reference>
  </SignedInfo>
  <KeyInfo>
    <X509Data>
      <X509Certificate>...</X509Certificate>  <!-- Certificate directly in KeyInfo -->
    </X509Data>
  </KeyInfo>
</Signature>
```

**Our Output:**
```xml
<wsse:BinarySecurityToken Id="X509-..." ...>cert_data</wsse:BinarySecurityToken>
<Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
  <SignedInfo>
    <Reference URI="#TS-...">...</Reference>  <!-- Signs specific Timestamp -->
    <Reference URI="#ACATransmitterManifest">...</Reference>  <!-- Signs Manifest -->
    <Reference URI="#ACABusinessHeader">...</Reference>  <!-- Signs BusinessHeader -->
  </SignedInfo>
  <KeyInfo>
    <wsse:SecurityTokenReference>
      <wsse:Reference URI="#X509-..." />  <!-- References BinarySecurityToken -->
    </wsse:SecurityTokenReference>
  </KeyInfo>
</Signature>
```

**Issue:**
- IRS signs the **entire envelope** with `URI=""`
- We sign **specific parts** with `URI="#id"`
- IRS includes certificate in `X509Data/X509Certificate`
- We use `BinarySecurityToken` + `SecurityTokenReference`

---

### 4. **Security Element Structure**
**IRS Example:**
```xml
<Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
  <Timestamp Id="_1" xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
    <Created>...</Created>
    <Expires>...</Expires>
  </Timestamp>
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    ...
  </Signature>
</Security>
```

**Our Output:**
```xml
<wsse:Security xmlns:wsse="..." xmlns:wsu="...">
  <wsse:BinarySecurityToken Id="X509-..." ...>cert</wsse:BinarySecurityToken>
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    ...
  </Signature>
  <wsu:Timestamp Id="TS-...">
    <wsu:Created>...</wsu:Created>
    <wsu:Expires>...</wsu:Expires>
  </wsu:Timestamp>
</wsse:Security>
```

**Issue:**
- Order: IRS has Timestamp BEFORE Signature, we have it AFTER
- IRS uses simple Id="_1", we use Id="TS-<guid>"
- We have BinarySecurityToken, IRS doesn't (cert is in Signature/KeyInfo instead)

---

## ⚠️ MODERATE ISSUES

### 5. **Element Namespace Usage**
**IRS Example:**
```xml
<ACABusinessHeader xmlns="urn:us:gov:treasury:irs:msg:acabusinessheader">
  <UniqueTransmissionId xmlns="urn:us:gov:treasury:irs:ext:aca:air:ty25">...</UniqueTransmissionId>
  <Timestamp xmlns="urn:us:gov:treasury:irs:common">...</Timestamp>
</ACABusinessHeader>
```

**Our Output:**
```xml
<acaBusHeader:ACABusinessHeader wsu:Id="ACABusinessHeader">
  <air:UniqueTransmissionId>...</air:UniqueTransmissionId>
  <irs:Timestamp>...</irs:Timestamp>
</acaBusHeader:ACABusinessHeader>
```

**Issue:** IRS uses default xmlns on parent, we use prefixed namespaces

---

### 6. **Missing wsu:Id Attributes in IRS Example**
**IRS Example:**
```xml
<ACABusinessHeader xmlns="...">  <!-- NO wsu:Id -->
```

**Our Output:**
```xml
<acaBusHeader:ACABusinessHeader wsu:Id="ACABusinessHeader">  <!-- Has wsu:Id -->
```

**Issue:** IRS example doesn't show wsu:Id on ACABusinessHeader or ACATransmitterManifestReqDtl, but we added them for signing. Need to verify if these are needed.

---

### 7. **ContactNameGrp in CompanyInformationGrp**
**IRS Example:**
```xml
<CompanyInformationGrp>
  <CompanyNm>...</CompanyNm>
  <MailingAddressGrp>...</MailingAddressGrp>
  <ContactPhoneNum>...</ContactPhoneNum>  <!-- NO ContactNameGrp -->
</CompanyInformationGrp>
```

**Our Output:**
```xml
<air:CompanyInformationGrp>
  <air:CompanyNm>...</air:CompanyNm>
  <air:MailingAddressGrp>...</air:MailingAddressGrp>
  <air:ContactNameGrp>  <!-- We HAVE ContactNameGrp -->
    <air:PersonFirstNm>John</air:PersonFirstNm>
    <air:PersonLastNm>Doe</air:PersonLastNm>
  </air:ContactNameGrp>
  <air:ContactPhoneNum>...</air:ContactPhoneNum>
</air:CompanyInformationGrp>
```

**Issue:** We have ContactNameGrp in CompanyInformationGrp, IRS doesn't. Need to check schema.

---

### 8. **VendorNm Element**
**IRS Example:**
```xml
<VendorInformationGrp>
  <VendorNm>Gemini Software</VendorNm>  <!-- HAS VendorNm -->
  <ContactNameGrp>...</ContactNameGrp>
  <ContactPhoneNum>...</ContactPhoneNum>
</VendorInformationGrp>
```

**Our Output:**
```xml
<air:VendorInformationGrp>
  <air:VendorCd>I</air:VendorCd>
  <!-- NO VendorNm -->
  <air:ContactNameGrp>...</air:ContactNameGrp>
  <air:ContactPhoneNum>...</air:ContactPhoneNum>
</air:VendorInformationGrp>
```

**Issue:** IRS has VendorNm, we don't. We only have VendorCd.

---

### 9. **FormTypeCd Value**
**IRS Example:**
```xml
<FormTypeCd>1094C</FormTypeCd>
```

**Our Output:**
```xml
<air:FormTypeCd>1094/1095C</air:FormTypeCd>
```

**Issue:** IRS uses "1094C", we use "1094/1095C"

---

### 10. **PersonFirstNm/LastNm Namespace**
**IRS Example:**
```xml
<ContactNameGrp>
  <PersonFirstNm xmlns="urn:us:gov:treasury:irs:common">Jane</PersonFirstNm>
  <PersonLastNm xmlns="urn:us:gov:treasury:irs:common">Smith</PersonLastNm>
</ContactNameGrp>
```

**Our Output:**
```xml
<air:ContactNameGrp>
  <air:PersonFirstNm>Jane</air:PersonFirstNm>
  <air:PersonLastNm>Smith</air:PersonLastNm>
</air:ContactNameGrp>
```

**Issue:** IRS uses explicit irs:common xmlns, we use air namespace

---

## 📝 MINOR ISSUES

### 11. **ACABulkRequestTransmitter version attribute**
**IRS Example:**
```xml
<ACABulkRequestTransmitter xmlns="...">  <!-- NO version attribute -->
```

**Our Output:**
```xml
<acaBodyReq:ACABulkRequestTransmitter version="1.0">
```

**Issue:** We add version="1.0", IRS doesn't show it

---

## 🎯 PRIORITY FIX ORDER

1. **Add WS-Addressing headers** (Action, To) - CRITICAL for routing
2. **Fix Signature structure** - Sign entire envelope with URI="", not specific parts
3. **Fix Security element order** - Timestamp before Signature
4. **Change namespace strategy** - Use inline xmlns, not prefixes
5. **Remove ContactNameGrp** from CompanyInformationGrp (if not in schema)
6. **Add VendorNm** to VendorInformationGrp
7. **Fix FormTypeCd** value
8. **Fix PersonFirstNm/LastNm** to use irs:common namespace
9. **Remove wsu:Id** from non-Timestamp elements (if not needed)
10. **Remove version attribute** from ACABulkRequestTransmitter

---

## 📊 Summary

**Critical Issues:** 4 (WS-Addressing, Signature, Security order, Namespace strategy)
**Moderate Issues:** 6 (Element namespaces, wsu:Id usage, ContactNameGrp, VendorNm, FormTypeCd, PersonNames)
**Minor Issues:** 1 (version attribute)

**Most Likely Cause of TPE1101:** Missing WS-Addressing headers and incorrect namespace declaration strategy.
