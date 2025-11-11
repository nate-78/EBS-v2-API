# IRS Documentation Analysis - Deep Dive Results

## Documents Reviewed
- **p5258.txt** - AIR Submission Composition and Reference Guide (Primary technical reference)
- **p5164.txt** - Test Package for Electronic Filers (Processing Year 2025)
- **p5165.txt** - Guide for Electronically Filing (Processing Year 2026 - most current, updated 07/11/2025)
- **p5308.txt** - Automated Enrollment Guide (Not relevant to our namespace issue)

---

## Key Findings

### 1. Namespace Usage - OFFICIAL GUIDANCE (p5258.txt Section 3.1)

**Lines 634-636 - THE CRITICAL STATEMENT:**
> "**Note: Transmitters can assign their own prefix to each namespace**, but should make sure that the usage matches the definition throughout the document. **Namespaces and prefix must be declared at the beginning of the payload and manifest files** in order for AIR to process the transmissions properly."

**What this means:**
✅ Using namespace prefixes (like `air:`, `irs:`, `acaBusHeader:`) is **EXPLICITLY ALLOWED**
✅ We CAN assign our own prefixes to namespaces
⚠️ Critical requirement: Namespaces must be declared at **the beginning** of payload and manifest files

**Lines 631-632:**
> "Namespaces may change between versions of IRS-published schemas. **The most recent IRS published schemas must always be used.**"

---

### 2. TPE1101 Error Definition (p5258.txt, lines 10033-10081)

**Fault Code:** TPE1101
**Service Leg:** Request
**Reason:** Invalid/Incorrect Namespace
**Channels:** A2A, UI

**Error Messages:**
1. Generic: "Our system detected invalid or outdated XML namespaces in your message. Please review the XML standards outlined in Section 3 of Publication 5258..."

2. Manifest-specific: "[TPE1101] Our system detected invalid or outdated XML namespaces **in the Manifest file**. Please review the XML standards outlined in Section 3..."

**Implication:** The error specifically mentions "the Manifest file" - this is the SOAP Header portion, not the Form Data File attachment.

---

### 3. Namespace Table (p5258.txt Table 3-1, lines 642-720)

**Tax Year Pattern:**
`urn:us:gov:treasury:irs:ext:aca:air:tyYY`

Where **tyYY** = "ty" + last 2 digits of tax year
- Example: **ty21** for 2021
- For 2025: **ty25** ✅ (We are using this correctly)

**Note (line 718-719):**
> "To account for prior, current and for future Tax Year (TY) and Filing Season (FS) updates - Insert the associated 'Tax Year (i.e. tyYY for example ty21)'"

**Our Current Usage:** `urn:us:gov:treasury:irs:ext:aca:air:ty25` ✅ CORRECT

---

### 4. Form Data File Example (p5258.txt, lines 769-779)

**They DO use namespace prefixes in their example:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<n1:Form109495BTransmittalUpstream
    xmlns="urn:us:gov:treasury:irs:ext:aca:air:tyYY"
    xmlns:irs="urn:us:gov:treasury:irs:common"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:n1="urn:us:gov:treasury:irs:msg:form1094-1095Btransmitterupstreammessage"
    xsi:schemaLocation="...">
<Form1094BUpstreamDetail recordType="String" lineNum="0">
    <SubmissionId>1</SubmissionId>
    <irs:TaxYr>2021</irs:TaxYr>
```

**Note:** This example uses BOTH a default namespace AND prefixed namespaces (`n1:`, `irs:`).

---

### 5. SOAP Message Structure Example (p5258.txt, lines 9758-9789)

```xml
<soap:Envelope>
  <soap:Header>
    ...
  </soap:Header>
  <soap:Body>
    <urn4:ACABulkRequestTransmitter version="1.0">
      <urn1:BulkExchangeFile>
        <inc:Include href="cid:1095BCTransBaseAttachment.xml"
                     xmlns:inc="http://www.w3.org/2004/08/xop/include"/>
      </urn1:BulkExchangeFile>
    </urn4:ACABulkRequestTransmitter>
  </soap:Body>
</soap:Envelope>
```

**Observation:** This example shows:
- Namespace prefixes being used (`urn4:`, `urn1:`, `inc:`)
- **version="1.0"** on `ACABulkRequestTransmitter` (contradicts our earlier fix where we removed it)

---

### 6. XML Signature Guidance (p5258.txt, lines 3014-3192)

**Old Example in Documentation** (may be outdated):
- Shows signing specific elements (Timestamp, ACATransmitterManifestReqDtl, ACABusinessHeader)
- Each with its own `URI="#id"` reference

**Our IRS Example (soap_signed.xml)**:
- Signs entire envelope with `URI=""`
- Single Reference element

**Discrepancy:** The documentation appears to show an older signing approach. Our soap_signed.xml example (which we presume is more current) uses envelope signing.

---

## What We DON'T Know

### Critical Unknowns:

1. **Namespace Declaration Location**
   - IRS example declares namespaces inline on each element (default xmlns)
   - We declare all namespaces at Envelope level with prefixes
   - Both are technically valid XML
   - Documentation says "declared at the beginning" - does this mean Envelope level or inline on first use?

2. **Schema Version Requirements**
   - What exact schema version should we be using for TY2025?
   - Are there schema version attributes we need to include?

3. **Timestamp Id Attribute**
   - IRS uses unprefixed `Id="_1"`
   - We use prefixed `wsu:Id="_1"`
   - XML spec says unprefixed attributes are local to element (not in namespace)
   - Does IRS validator care about this distinction?

4. **version Attribute on ACABulkRequestTransmitter**
   - Example in p5258 shows `version="1.0"`
   - IRS soap_signed.xml does NOT have this attribute
   - Which is correct?

---

## Comparison: Our Output vs IRS Requirements

### ✅ What We're Doing Right

1. **Tax Year Namespace**: Using `ty25` correctly for 2025
2. **Namespace Prefixes**: Allowed per documentation
3. **Required Elements**: All manifest elements present
4. **Signature Algorithm**: RSA-SHA256 as required
5. **WS-Addressing Headers**: Action and To present
6. **Security Structure**: Timestamp before Signature
7. **Envelope Signing**: Now signing entire envelope with URI=""
8. **Certificate Format**: X509Certificate in KeyInfo

### ⚠️ Potential Issues

1. **Namespace Declaration Strategy**
   - **Us:** All namespaces declared at Envelope level with prefixes
   - **IRS Example:** Inline default namespaces on each element
   - **Status:** Both valid XML, but validator may expect specific pattern

2. **Timestamp Id Attribute Prefix**
   - **Us:** `wsu:Id="_1"`
   - **IRS:** `Id="_1"` (no prefix)
   - **Status:** Semantically different per XML namespace rules

3. **Element Namespace Scope**
   - **Us:** Every element uses explicit prefix
   - **IRS:** Elements use default namespace from parent
   - **Status:** Semantically equivalent but textually different

---

## Hypothesis: Why We're Still Getting TPE1101

Based on documentation review, the most likely causes are:

### Theory 1: Strict XML Pattern Matching (HIGH PROBABILITY)
The IRS validator may be doing **strict text-based XML validation** rather than semantic namespace validation. If their validator expects:
```xml
<ACABusinessHeader xmlns="urn:us:gov:treasury:irs:msg:acabusinessheader">
```

But we send:
```xml
<acaBusHeader:ACABusinessHeader>
```

Even though these are semantically identical (both create an ACABusinessHeader element in the same namespace), a strict pattern matcher would reject ours.

### Theory 2: Timestamp Id Attribute (MEDIUM PROBABILITY)
The difference between `Id="_1"` and `wsu:Id="_1"` is significant in XML namespace terms:
- `Id` (no prefix) = attribute NOT in any namespace, local to element
- `wsu:Id` = attribute IN the wsu namespace

The IRS validator may specifically look for an unprefixed `Id` attribute.

### Theory 3: Schema Version Mismatch (LOW PROBABILITY)
We may be missing schema version attributes or using outdated schema versions. However, documentation doesn't clearly specify version requirements for TY2025.

---

## Recommended Next Steps

### Option 1: Contact IRS Support (RECOMMENDED FIRST STEP)
**Rationale:** Before major refactoring, get authoritative guidance

**What to ask:**
1. "Does the validator accept namespace prefixes declared at Envelope level, or must we use inline default namespaces on each element?"
2. "Should the Timestamp Id attribute be `Id` or `wsu:Id`?"
3. "Is there a schema version we should reference?"
4. "Can you validate our current SOAP structure and identify what specifically triggers TPE1101?"

**Attach:** Our current generated_request.txt

### Option 2: Try Minimal Change - Timestamp Id (QUICK TEST)
**Change:** `wsu:Id="_1"` → `Id="_1"`
**Effort:** 5 minutes
**Risk:** Very low
**Value:** If this is the issue, instant fix. If not, we've eliminated one variable.

### Option 3: Full Namespace Refactoring (LAST RESORT)
**Only if:** IRS confirms we must match their exact pattern
**Effort:** 4-6 hours
**Risk:** High (potential for new bugs)
**Approach:** Rewrite to use inline default namespaces instead of prefixes

---

## Documentation Gaps & Inconsistencies Found

1. **p5258.txt Signature Example** (lines 3014-3192) shows signing specific elements, but soap_signed.xml shows envelope signing
2. **ACABulkRequestTransmitter version attribute** - present in p5258 example, absent in soap_signed.xml
3. **No explicit guidance** on whether namespace prefixes should be declared at Envelope level or inline
4. **No schema version requirements** clearly stated for TY2025

---

## Conclusion

The IRS documentation **explicitly allows** namespace prefixes and says transmitters can assign their own. However, the TPE1101 error persists, suggesting the validator may have stricter requirements than the documentation indicates.

**Most likely root cause:** XML pattern matching expecting inline default namespaces rather than prefixed namespaces, even though both are semantically valid.

**Best path forward:** Contact IRS support with our current output before doing major refactoring. The inconsistencies in their documentation and the mismatch between documentation examples and soap_signed.xml suggest we need authoritative clarification.
