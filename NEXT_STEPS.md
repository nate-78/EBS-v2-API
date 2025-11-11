# Next Steps - IRS Support Contact

## Summary

I've created a comprehensive support request document for contacting the IRS AIR Help Desk. All necessary files are ready and up-to-date.

---

## Documents Created

### 1. **IRS_SUPPORT_REQUEST.md** (Main document)
- Location: `/mnt/c/owensdev-git/EBS-v2-API/IRS_SUPPORT_REQUEST.md`
- Contains: Complete support request with 4 specific questions for IRS
- Includes: All our contact info (TCC, ASID, Software ID)
- Details: Summary of 10 fixes already implemented
- Ready to: Use as script when calling or send as reference

### 2. **generated_request.txt** (Current SOAP output)
- Location: `/mnt/c/owensdev-git/EBS-v2-API/AcaApi.Poc/generated_request.txt`
- Status: ✅ Already updated with latest test run output
- Contains: Complete multipart MIME message with SOAP envelope and form data
- Note: SOAP XML is on line 6 (one long line - this is valid but hard to read)

### 3. **DOCUMENTATION_FINDINGS.md** (Analysis)
- Location: `/mnt/c/owensdev-git/EBS-v2-API/DOCUMENTATION_FINDINGS.md`
- Contains: Deep dive analysis of p5258, p5164, p5165
- Shows: What we're doing right vs. potential issues
- Useful: For reference during IRS call if they ask technical questions

---

## How to Contact IRS AIR Help Desk

### Phone Numbers (Primary Contact Method)
- **Domestic (Toll-Free):** 1-866-937-4130
- **International:** 470-769-5100 (not toll-free)
- **Hours:** Standard business hours (EST/EDT)

### What to Say
When you call, say something like:

> "Hello, I'm calling about TPE1101 errors we're receiving from the AATS system when submitting ACA 1094-C/1095-C forms via the A2A channel. Our TCC is BB05S and our ASID is 1BB05S01. We've made extensive corrections based on Publication 5258 but the error persists. We have specific questions about namespace declaration requirements and would like to validate our current SOAP structure."

### Information to Have Ready
- **TCC:** BB05S
- **ASID:** 1BB05S01
- **Software ID:** 25A0023165
- **Error Code:** TPE1101
- **Tax Year:** 2025
- **Form Type:** 1094-C/1095-C
- **Testing Environment:** AATS

---

## The 4 Key Questions to Ask

### Question 1: Namespace Declaration Strategy
Does the validator accept namespace prefixes declared at Envelope level (our approach), or must we use inline default namespaces on each element (IRS example approach)?

**Why this matters:** This is the most likely cause of TPE1101. Both are valid XML but the validator may be strict.

### Question 2: Timestamp Id Attribute
Should the Timestamp Id attribute be `Id="_1"` (unprefixed) or `wsu:Id="_1"` (prefixed)?

**Why this matters:** XML spec treats these differently. Unprefixed = local attribute, Prefixed = namespace attribute.

### Question 3: Schema Version
Are there specific schema version attributes we should include for TY2025?

**Why this matters:** Ensures we're using the correct/latest schema version.

### Question 4: Validate Our Output
Can you validate our generated_request.txt and tell us specifically what triggers TPE1101?

**Why this matters:** Direct answer would save us significant development time.

---

## What to Expect

**Scenario 1: They need to escalate**
- They may need to pass your question to a technical specialist
- Be prepared to email generated_request.txt to them
- They might ask for a callback number

**Scenario 2: They have immediate guidance**
- They may tell you namespace prefixes at Envelope level are fine → we'd look elsewhere
- They may tell you to match IRS example exactly → we'd do full namespace refactoring
- They may spot something specific in our SOAP structure → quick fix

**Scenario 3: They need more information**
- Have the documents ready (IRS_SUPPORT_REQUEST.md, generated_request.txt)
- Reference Publication 5258, Section 3.1 (namespace guidance)
- Mention you've compared against soap_signed.xml example

---

## After the Call

### If they say "Namespace prefixes are fine"
1. We need to investigate other potential causes
2. Possible next step: Try the Timestamp Id fix (`Id` instead of `wsu:Id`)
3. May need to look at other XML structure differences

### If they say "Must match IRS example exactly"
1. We'll need to refactor namespace strategy
2. Switch from prefixed namespaces to inline default namespaces
3. Estimated effort: 4-6 hours
4. This would be the major refactoring we've been avoiding

### If they find a specific issue
1. Implement the fix they identify
2. Run tests again
3. Hopefully resolve TPE1101

---

## Additional Resources

**IRS Website:**
- https://www.irs.gov/e-file-providers/air/affordable-care-act-information-return-air-program

**QuickAlerts (IRS Email/SMS Notifications):**
- Sign up for AIR system updates and known issues
- Found on the AIR program website

**If Email Becomes Available:**
- The old airmailbox@irs.gov was discontinued June 2024
- Currently phone support only
- They may provide a different email during the call

---

## Preparation Checklist

Before calling:
- [ ] Review IRS_SUPPORT_REQUEST.md
- [ ] Have generated_request.txt location ready
- [ ] Have your contact information ready (for callback if needed)
- [ ] Note the 4 key questions above
- [ ] Have TCC (BB05S) and ASID (1BB05S01) written down
- [ ] Be ready to describe what fixes we've already made (10 items listed in support request)
- [ ] Have this NEXT_STEPS.md file open for reference

---

## Timeline Recommendation

**Best time to call:** Early morning (first hour of their business day) - typically less busy

**What if you can't get through:**
- Try different times of day
- Be prepared for longer wait times during tax season
- Consider calling early in the week (Monday/Tuesday)

---

## Success Criteria

A successful call would result in:
1. Clear answer on namespace declaration strategy (prefixed vs inline)
2. Understanding of what specifically triggers TPE1101 in our message
3. Actionable next steps (specific code changes needed)
4. Timeline for resolution

---

Good luck with the call! The support request document is comprehensive and shows we've done our homework. The IRS Help Desk should appreciate the level of detail and analysis we've already completed.
